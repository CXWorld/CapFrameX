// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// Copyright (C) LibreHardwareMonitor and Contributors.
// All Rights Reserved.

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using LibreHardwareMonitor.Interop;
using Serilog;
using Windows.Win32.Storage.Nvme;

#pragma warning disable CS1591 // file exempt from XML documentation

namespace LibreHardwareMonitor.Hardware.Storage;

public class NVMeSmart : IDisposable
{
    private const int CacheLifetimeMilliseconds = 5000;
    private const int FailureBackoffMilliseconds = 5000;
    private const int InitialReadWaitMilliseconds = 1500;

    private static readonly long CacheLifetimeTicks = MillisecondsToStopwatchTicks(CacheLifetimeMilliseconds);
    private static readonly long FailureBackoffTicks = MillisecondsToStopwatchTicks(FailureBackoffMilliseconds);

    private readonly int _driveNumber;
    private readonly ManualResetEvent _firstHealthReadCompleted = new(false);
    private readonly SafeHandle _handle;
    private readonly AutoResetEvent _healthReadRequested = new(false);
    private readonly object _healthSync = new();
    private readonly Thread _healthWorker;

    private Storage.NVMeHealthInfo _cachedHealthInfo;
    private long _cacheTimestamp;
    private bool _closeRequested;
    private bool _hasCachedHealthInfo;
    private bool _healthReadInProgress;
    private bool _healthReadPending;
    private long _lastHealthReadAttempt;

    internal NVMeSmart(StorageInfo storageInfo, bool enableHealthReader = true)
    {
        _driveNumber = storageInfo.Index;
        NVMeDrive = null;
        string name = storageInfo.Name;

        // Test Windows generic driver protocol.
        if (NVMeDrive == null)
        {
            _handle = NVMeWindows.IdentifyDevice(storageInfo);
            if (_handle != null)
            {
                NVMeDrive = new NVMeWindows(storageInfo.DeviceId);
            }
        }

        // Test Samsung protocol.
        if (NVMeDrive == null && name.IndexOf("Samsung", StringComparison.OrdinalIgnoreCase) > -1)
        {
            _handle = NVMeSamsung.IdentifyDevice(storageInfo);
            if (_handle != null)
            {
                NVMeDrive = new NVMeSamsung();
                if (!NVMeDrive.IdentifyController(_handle, out _))
                {
                    NVMeDrive = null;
                }
            }
        }

        // Test Intel protocol.
        if (NVMeDrive == null && name.IndexOf("Intel", StringComparison.OrdinalIgnoreCase) > -1)
        {
            _handle = NVMeIntel.IdentifyDevice(storageInfo);
            if (_handle != null)
            {
                NVMeDrive = new NVMeIntel();
            }
        }

        // Test Intel raid protocol.
        if (NVMeDrive == null && name.IndexOf("Intel", StringComparison.OrdinalIgnoreCase) > -1)
        {
            _handle = NVMeIntelRst.IdentifyDevice(storageInfo);
            if (_handle != null)
            {
                NVMeDrive = new NVMeIntelRst();
            }
        }

        if (enableHealthReader && IsValid && NVMeDrive != null)
        {
            _healthWorker = CreateHealthWorker();
            _healthWorker.Start();
        }
    }

    internal NVMeSmart(int driveNumber, SafeHandle handle, INVMeDrive nvmeDrive)
    {
        _driveNumber = driveNumber;
        _handle = handle;
        NVMeDrive = nvmeDrive;

        if (IsValid && NVMeDrive != null)
        {
            _healthWorker = CreateHealthWorker();
            _healthWorker.Start();
        }
    }

    public bool IsValid
    {
        get
        {
            return _handle is { IsInvalid: false };
        }
    }

    internal INVMeDrive NVMeDrive { get; }

    public void Dispose()
    {
        Close();
    }

    private static string GetString(byte[] s)
    {
        return Encoding.ASCII.GetString(s).Trim('\t', '\n', '\r', ' ', '\0');
    }

    private static short KelvinToCelsius(ushort k)
    {
        return (short)(k > 0 ? k - 273 : short.MinValue);
    }

    private static short KelvinToCelsius(byte[] k)
    {
        return KelvinToCelsius(BitConverter.ToUInt16(k, 0));
    }

    public void Close()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected void Dispose(bool disposing)
    {
        if (!disposing)
            return;

        lock (_healthSync)
        {
            if (_closeRequested)
                return;

            _closeRequested = true;
            _healthReadPending = false;
        }

        if (_healthWorker != null)
        {
            if (NVMeDrive is ICancellableNVMeDrive cancellableDrive)
                cancellableDrive.CancelPendingIo();

            _healthReadRequested.Set();
            _healthWorker.Join(InitialReadWaitMilliseconds);
        }

        if (_handle is { IsClosed: false })
        {
            _handle.Close();
        }

        if (_healthWorker == null || !_healthWorker.IsAlive)
        {
            _healthReadRequested.Dispose();
            _firstHealthReadCompleted.Dispose();
        }
    }

    public Storage.NVMeInfo GetInfo()
    {
        if (_handle?.IsClosed != false)
            return null;

        bool valid = false;
        var data = new NVME_IDENTIFY_CONTROLLER_DATA();
        if (NVMeDrive != null)
            valid = NVMeDrive.IdentifyController(_handle, out data);

        if (!valid)
            return null;

        return new NVMeInfo(_driveNumber, data);
    }

    public Storage.NVMeHealthInfo GetHealthInfo()
    {
        RequestHealthInfo();
        return TryGetHealthInfo(out Storage.NVMeHealthInfo health, out _) ? health : null;
    }

    internal Storage.NVMeHealthInfo GetInitialHealthInfo()
    {
        RequestHealthInfo(force: true);
        _firstHealthReadCompleted.WaitOne(InitialReadWaitMilliseconds);
        return TryGetHealthInfo(out Storage.NVMeHealthInfo health, out _) ? health : null;
    }

    internal void RequestHealthInfo()
    {
        RequestHealthInfo(force: false);
    }

    internal bool TryGetHealthInfo(out Storage.NVMeHealthInfo health, out TimeSpan age)
    {
        lock (_healthSync)
        {
            if (!_hasCachedHealthInfo)
            {
                health = null;
                age = TimeSpan.MaxValue;
                return false;
            }

            health = _cachedHealthInfo;
            age = StopwatchTicksToTimeSpan(Stopwatch.GetTimestamp() - _cacheTimestamp);
            return true;
        }
    }

    private void RequestHealthInfo(bool force)
    {
        bool requestRead = false;

        lock (_healthSync)
        {
            if (_closeRequested || _healthWorker == null || _healthReadInProgress || _healthReadPending)
                return;

            long now = Stopwatch.GetTimestamp();
            bool cacheIsFresh = _hasCachedHealthInfo && now - _cacheTimestamp < CacheLifetimeTicks;
            bool failureBackoffActive = !_hasCachedHealthInfo && _lastHealthReadAttempt != 0 && now - _lastHealthReadAttempt < FailureBackoffTicks;

            if (!force && (cacheIsFresh || failureBackoffActive))
                return;

            _healthReadPending = true;
            requestRead = true;
        }

        if (requestRead)
            _healthReadRequested.Set();
    }

    private Storage.NVMeHealthInfo ReadHealthInfo()
    {
        if (_handle?.IsClosed != false)
            return null;

        bool valid = false;
        var data = new NVME_HEALTH_INFO_LOG();
        if (NVMeDrive != null)
            valid = NVMeDrive.HealthInfoLog(_handle, out data);

        if (!valid)
            return null;

        return new NVMeHealthInfo(data);
    }

    private Thread CreateHealthWorker()
    {
        return new Thread(HealthReadLoop)
        {
            IsBackground = true,
            Name = $"NVMe health reader {_driveNumber}"
        };
    }

    private void HealthReadLoop()
    {
        while (true)
        {
            _healthReadRequested.WaitOne();

            lock (_healthSync)
            {
                if (_closeRequested)
                    return;

                _healthReadPending = false;
                _healthReadInProgress = true;
                _lastHealthReadAttempt = Stopwatch.GetTimestamp();
            }

            Storage.NVMeHealthInfo health = null;

            try
            {
                health = ReadHealthInfo();
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "NVMe health read for drive {DriveNumber} failed.", _driveNumber);
            }

            lock (_healthSync)
            {
                _healthReadInProgress = false;

                if (health != null && !_closeRequested)
                {
                    _cachedHealthInfo = health;
                    _cacheTimestamp = Stopwatch.GetTimestamp();
                    _hasCachedHealthInfo = true;
                }
            }

            _firstHealthReadCompleted.Set();
        }
    }

    private static long MillisecondsToStopwatchTicks(int milliseconds)
    {
        return (long)Math.Ceiling(milliseconds * (double)Stopwatch.Frequency / 1000);
    }

    private static TimeSpan StopwatchTicksToTimeSpan(long ticks)
    {
        return TimeSpan.FromSeconds(ticks / (double)Stopwatch.Frequency);
    }

    private class NVMeInfo : Storage.NVMeInfo
    {
        public NVMeInfo(int index, NVME_IDENTIFY_CONTROLLER_DATA data)
        {
            Index = index;
            VID = data.VID;
            SSVID = data.SSVID;
            Serial = GetString(data.SN.ToArray());
            Model = GetString(data.MN.ToArray());
            Revision = GetString(data.FR.ToArray());
            IEEE = data.IEEE.ToArray();
            TotalCapacity = BitConverter.ToUInt64(data.TNVMCAP.ToArray(), 0); // 128bit little endian
            UnallocatedCapacity = BitConverter.ToUInt64(data.UNVMCAP.ToArray(), 0);
            ControllerId = data.CNTLID;
            NumberNamespaces = data.NN;
        }
    }

    private class NVMeHealthInfo : Storage.NVMeHealthInfo
    {
        public NVMeHealthInfo(NVME_HEALTH_INFO_LOG log)
        {
            Temperature = KelvinToCelsius(log.Temperature.ToArray());
            AvailableSpare = log.AvailableSpare;
            AvailableSpareThreshold = log.AvailableSpareThreshold;
            PercentageUsed = log.PercentageUsed;
            DataUnitRead = BitConverter.ToUInt64(log.DataUnitRead.ToArray(), 0);
            DataUnitWritten = BitConverter.ToUInt64(log.DataUnitWritten.ToArray(), 0);
            HostReadCommands = BitConverter.ToUInt64(log.HostReadCommands.ToArray(), 0);
            HostWriteCommands = BitConverter.ToUInt64(log.HostWrittenCommands.ToArray(), 0);
            ControllerBusyTime = BitConverter.ToUInt64(log.ControllerBusyTime.ToArray(), 0);
            PowerCycle = BitConverter.ToUInt64(log.PowerCycle.ToArray(), 0);
            PowerOnHours = BitConverter.ToUInt64(log.PowerOnHours.ToArray(), 0);
            UnsafeShutdowns = BitConverter.ToUInt64(log.UnsafeShutdowns.ToArray(), 0);
            MediaErrors = BitConverter.ToUInt64(log.MediaErrors.ToArray(), 0);
            ErrorInfoLogEntryCount = BitConverter.ToUInt64(log.ErrorInfoLogEntryCount.ToArray(), 0);
            WarningCompositeTemperatureTime = log.WarningCompositeTemperatureTime;
            CriticalCompositeTemperatureTime = log.CriticalCompositeTemperatureTime;

            TemperatureSensors = new short[8];
            TemperatureSensors[0] = KelvinToCelsius(log.TemperatureSensor1);
            TemperatureSensors[1] = KelvinToCelsius(log.TemperatureSensor2);
            TemperatureSensors[2] = KelvinToCelsius(log.TemperatureSensor3);
            TemperatureSensors[3] = KelvinToCelsius(log.TemperatureSensor4);
            TemperatureSensors[4] = KelvinToCelsius(log.TemperatureSensor5);
            TemperatureSensors[5] = KelvinToCelsius(log.TemperatureSensor6);
            TemperatureSensors[6] = KelvinToCelsius(log.TemperatureSensor7);
            TemperatureSensors[7] = KelvinToCelsius(log.TemperatureSensor8);
        }
    }
}
