// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// Copyright (C) LibreHardwareMonitor and Contributors.
// All Rights Reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using CapFrameX.Monitoring.Contracts;
using LibreHardwareMonitor.Interop;
using Windows.Win32;
using Windows.Win32.Storage.FileSystem;
using Windows.Win32.System.Ioctl;

namespace LibreHardwareMonitor.Hardware.Storage;

/// <summary>
/// Base class for all storage devices, providing the drive performance sensors that are
/// independent of the underlying bus.
/// </summary>
public abstract class AbstractStorage : Hardware
{
    private const double BytesPerGigabyte = 1_000_000_000d;

    private readonly PerformanceValue _perfRead = new();
    private readonly PerformanceValue _perfTotal = new();
    private readonly PerformanceValue _perfWrite = new();
    private readonly StorageInfo _storageInfo;

    private ISensorConfig _sensorConfig;
    private long _lastReadCount;
    private long _lastTime;
    private long _lastWriteCount;
    private Sensor _sensorDiskReadActivity;
    private Sensor _sensorDiskReadRate;
    private Sensor _sensorDiskTotalActivity;
    private Sensor _sensorDiskWriteActivity;
    private Sensor _sensorDiskWriteRate;
    private Sensor _usageSensor;

    internal AbstractStorage(StorageInfo storageInfo, string name, string firmwareRevision, string id, int index, ISettings settings)
        : base(name, new Identifier(id, index.ToString(CultureInfo.InvariantCulture)), settings)
    {
        _storageInfo = storageInfo;
        FirmwareRevision = firmwareRevision;
        Index = index;

        string[] logicalDrives = WindowsStorage.GetLogicalDrives(index);
        var driveInfoList = new List<DriveInfo>(logicalDrives.Length);

        foreach (string logicalDrive in logicalDrives)
        {
            try
            {
                var di = new DriveInfo(logicalDrive);
                if (di.TotalSize > 0)
                    driveInfoList.Add(new DriveInfo(logicalDrive));
            }
            catch (ArgumentException)
            { }
            catch (IOException)
            { }
            catch (UnauthorizedAccessException)
            { }
        }

        DriveInfos = driveInfoList.ToArray();
    }

    /// <summary>
    /// Gets the logical drives that reside on this storage device.
    /// </summary>
    public DriveInfo[] DriveInfos { get; }

    /// <summary>
    /// Gets the firmware revision reported by the device.
    /// </summary>
    public string FirmwareRevision { get; }

    /// <inheritdoc />
    public override HardwareType HardwareType => HardwareType.Storage;

    /// <summary>
    /// Gets the zero-based index of the physical drive.
    /// </summary>
    public int Index { get; }

    internal void SetSensorConfig(ISensorConfig sensorConfig)
    {
        _sensorConfig = sensorConfig;
    }

    /// <inheritdoc />
    public override void Close()
    {
        _storageInfo.Handle?.Close();
        base.Close();
    }

    /// <summary>
    /// Creates the storage implementation that matches the bus type of the given device.
    /// </summary>
    /// <param name="deviceId">The device path of the physical drive.</param>
    /// <param name="driveNumber">The number of the physical drive.</param>
    /// <param name="diskSize">The size of the drive in bytes.</param>
    /// <param name="scsiPort">The SCSI port the drive is attached to.</param>
    /// <param name="settings">Additional settings passed by the <see cref="IComputer" />.</param>
    /// <returns>The storage instance, or <see langword="null" /> if the device is not supported.</returns>
    public static AbstractStorage CreateInstance(string deviceId, uint driveNumber, ulong diskSize, int scsiPort, ISettings settings)
    {
        StorageInfo info = WindowsStorage.GetStorageInfo(deviceId, driveNumber);
        if (info == null || info.Removable || info.BusType is STORAGE_BUS_TYPE.BusTypeVirtual or STORAGE_BUS_TYPE.BusTypeFileBackedVirtual)
            return null;

        info.DiskSize = diskSize;
        info.DeviceId = deviceId;
        info.Handle = PInvoke.CreateFile(deviceId,
                                         (uint)FileAccess.ReadWrite,
                                         FILE_SHARE_MODE.FILE_SHARE_READ | FILE_SHARE_MODE.FILE_SHARE_WRITE,
                                         null,
                                         FILE_CREATION_DISPOSITION.OPEN_EXISTING,
                                         FILE_FLAGS_AND_ATTRIBUTES.FILE_ATTRIBUTE_NORMAL,
                                         null);
        info.Scsi = $@"\\.\SCSI{scsiPort}:";

        //fallback, when it is not possible to read out with the nvme implementation,
        //try it with the sata smart implementation
        if (info.BusType == STORAGE_BUS_TYPE.BusTypeNvme)
        {
            AbstractStorage x = NVMeGeneric.CreateInstance(info, settings);
            if (x != null)
                return x;
        }

        return info.BusType is STORAGE_BUS_TYPE.BusTypeAta or STORAGE_BUS_TYPE.BusTypeSata or STORAGE_BUS_TYPE.BusTypeNvme
            ? AtaStorage.CreateInstance(info, settings)
            : StorageGeneric.CreateInstance(info, settings);
    }

    /// <summary>
    /// Creates the sensors of the device. Overrides add their device-specific sensors and call the base implementation.
    /// </summary>
    protected virtual void CreateSensors()
    {
        if (DriveInfos.Length > 0)
        {
            _usageSensor = new Sensor("Drive Used Space", 0, SensorType.Load, this, _settings)
            { PresentationSortKey = $"{Index}_3_0" };
            ActivateSensor(_usageSensor);
        }

        _sensorDiskReadActivity = new Sensor("Drive Read Activity", 31, SensorType.Load, this, _settings)
        { PresentationSortKey = $"{Index}_2_0" };
        ActivateSensor(_sensorDiskReadActivity);

        _sensorDiskWriteActivity = new Sensor("Drive Write Activity", 32, SensorType.Load, this, _settings)
        { PresentationSortKey = $"{Index}_2_1" };
        ActivateSensor(_sensorDiskWriteActivity);

        _sensorDiskTotalActivity = new Sensor("Drive Total Activity", 33, SensorType.Load, this, _settings)
        { PresentationSortKey = $"{Index}_2_2" };
        ActivateSensor(_sensorDiskTotalActivity);

        _sensorDiskReadRate = new Sensor("Drive Read Rate", 34, SensorType.Throughput, this, _settings)
        { PresentationSortKey = $"{Index}_0_0" };
        ActivateSensor(_sensorDiskReadRate);

        _sensorDiskWriteRate = new Sensor("Drive Write Rate", 35, SensorType.Throughput, this, _settings)
        { PresentationSortKey = $"{Index}_0_1" };
        ActivateSensor(_sensorDiskWriteRate);
    }

    /// <summary>
    /// Reads the device-specific sensors of the device.
    /// </summary>
    protected abstract void UpdateSensors();

    /// <summary>
    /// Normalises any storage sensor label so the entire Storage subsystem surfaces sensors with a
    /// uniform "Drive " prefix. Used by both the SMART attribute path (ATAStorage) and the NVMe
    /// AddSensor helper (NVMeGeneric). Names that already start with "Drive " are passed through
    /// unchanged; any "Disk " prefix (from <see cref="SmartNames.DiskShift" /> etc.) is rewritten to
    /// "Drive " so flat OSD lists use one consistent vocabulary.
    /// </summary>
    /// <param name="sensorName">The sensor label to normalise.</param>
    /// <returns>The label carrying the uniform "Drive " prefix.</returns>
    protected static string WithDrivePrefix(string sensorName)
    {
        if (string.IsNullOrEmpty(sensorName))
            return sensorName;
        if (sensorName.StartsWith("Drive ", StringComparison.Ordinal))
            return sensorName;
        if (sensorName.StartsWith("Disk ", StringComparison.Ordinal))
            return "Drive " + sensorName.Substring("Disk ".Length);
        return "Drive " + sensorName;
    }

    /// <inheritdoc />
    public override void Update()
    {
        // Update statistics.
        if (_storageInfo != null && ShouldEvaluatePerformanceSensors())
        {
            try
            {
                UpdatePerformanceSensors();
            }
            catch
            {
                // Ignored.
            }
        }

        if (ShouldEvaluateDeviceSensors())
            UpdateSensors();

        if (_usageSensor != null && ShouldEvaluateSensor(_usageSensor))
        {
            long totalSize = 0;
            long totalFreeSpace = 0;

            for (int i = 0; i < DriveInfos.Length; i++)
            {
                if (!DriveInfos[i].IsReady)
                    continue;

                try
                {
                    totalSize += DriveInfos[i].TotalSize;
                    totalFreeSpace += DriveInfos[i].TotalFreeSpace;
                }
                catch (IOException)
                { }
                catch (UnauthorizedAccessException)
                { }
            }

            if (totalSize > 0)
                _usageSensor.Value = 100.0f - (100.0f * totalFreeSpace / totalSize);
            else
                _usageSensor.Value = null;
        }
    }

    private bool ShouldEvaluateDeviceSensors()
    {
        if (_sensorConfig == null)
            return true;

        bool evaluate = false;

        // Evaluate every identifier without short-circuiting. GetSensorEvaluate deliberately
        // returns true once for discovery, so short-circuiting would spread discovery over
        // several ticks and issue one unnecessary SMART request per sensor.
        foreach (ISensor sensor in _active)
        {
            if (IsBaseSensor(sensor))
                continue;

            evaluate |= _sensorConfig.GetSensorEvaluate(sensor.Identifier.ToString());
        }

        return evaluate;
    }

    private bool ShouldEvaluatePerformanceSensors()
    {
        if (_sensorConfig == null)
            return true;

        bool evaluate = false;
        evaluate |= ShouldEvaluateSensor(_sensorDiskReadActivity);
        evaluate |= ShouldEvaluateSensor(_sensorDiskWriteActivity);
        evaluate |= ShouldEvaluateSensor(_sensorDiskTotalActivity);
        evaluate |= ShouldEvaluateSensor(_sensorDiskReadRate);
        evaluate |= ShouldEvaluateSensor(_sensorDiskWriteRate);
        return evaluate;
    }

    private bool ShouldEvaluateSensor(ISensor sensor)
    {
        return sensor != null && (_sensorConfig == null || _sensorConfig.GetSensorEvaluate(sensor.Identifier.ToString()));
    }

    private bool IsBaseSensor(ISensor sensor)
    {
        return ReferenceEquals(sensor, _usageSensor) ||
               ReferenceEquals(sensor, _sensorDiskReadActivity) ||
               ReferenceEquals(sensor, _sensorDiskWriteActivity) ||
               ReferenceEquals(sensor, _sensorDiskTotalActivity) ||
               ReferenceEquals(sensor, _sensorDiskReadRate) ||
               ReferenceEquals(sensor, _sensorDiskWriteRate);
    }

    private unsafe void UpdatePerformanceSensors()
    {
        DISK_PERFORMANCE diskPerformance = new();

        uint bytesReturned;
        if (!PInvoke.DeviceIoControl(_storageInfo.Handle, PInvoke.IOCTL_DISK_PERFORMANCE, null, 0, &diskPerformance, (uint)sizeof(DISK_PERFORMANCE), &bytesReturned, null))
        {
            return;
        }

        _perfRead.Update(diskPerformance.ReadTime, diskPerformance.QueryTime);
        _sensorDiskReadActivity.Value = (float)_perfRead.Result;

        _perfWrite.Update(diskPerformance.WriteTime, diskPerformance.QueryTime);
        _sensorDiskWriteActivity.Value = (float)_perfWrite.Result;

        _perfTotal.Update(diskPerformance.IdleTime, diskPerformance.QueryTime);
        _sensorDiskTotalActivity.Value = (float)(100 - _perfTotal.Result);

        long readCount = diskPerformance.BytesRead;
        long readDiff = readCount - _lastReadCount;
        _lastReadCount = readCount;

        long writeCount = diskPerformance.BytesWritten;
        long writeDiff = writeCount - _lastWriteCount;
        _lastWriteCount = writeCount;

        long currentTime = Stopwatch.GetTimestamp();
        if (_lastTime != 0)
        {
            double timeDeltaSeconds = TimeSpan.FromTicks(currentTime - _lastTime).TotalSeconds;

            // Convert bytes/s to GB/s (decimal, matching the GB convention used elsewhere in this codebase).
            double writeSpeed = writeDiff / timeDeltaSeconds / BytesPerGigabyte;
            _sensorDiskWriteRate.Value = (float)writeSpeed;

            double readSpeed = readDiff / timeDeltaSeconds / BytesPerGigabyte;
            _sensorDiskReadRate.Value = (float)readSpeed;
        }

        _lastTime = currentTime;
    }

    /// <summary>
    /// Appends the device-specific part of the report.
    /// </summary>
    /// <param name="r">The builder collecting the report.</param>
    protected abstract void GetReport(StringBuilder r);

    /// <inheritdoc />
    public override string GetReport()
    {
        var r = new StringBuilder();
        r.AppendLine("Storage");
        r.AppendLine();
        r.AppendLine("Drive Name: " + _name);
        r.AppendLine("Firmware Version: " + FirmwareRevision);
        r.AppendLine();
        GetReport(r);

        foreach (DriveInfo di in DriveInfos)
        {
            if (!di.IsReady)
                continue;

            try
            {
                r.AppendLine("Logical Drive Name: " + di.Name);
                r.AppendLine("Format: " + di.DriveFormat);
                r.AppendLine("Total Size: " + di.TotalSize);
                r.AppendLine("Total Free Space: " + di.TotalFreeSpace);
                r.AppendLine();
            }
            catch (IOException)
            { }
            catch (UnauthorizedAccessException)
            { }
        }

        return r.ToString();
    }

    /// <inheritdoc />
    public override void Traverse(IVisitor visitor)
    {
        foreach (ISensor sensor in Sensors)
            sensor.Accept(visitor);
    }

    /// <summary>
    /// Helper to calculate the disk performance with base timestamps
    /// https://docs.microsoft.com/en-us/windows/win32/cimwin32prov/win32-perfrawdata
    /// </summary>
    private class PerformanceValue
    {
        public double Result { get; private set; }

        private long Time { get; set; }

        private long Value { get; set; }

        public void Update(long val, long valBase)
        {
            long diffValue = val - Value;
            long diffTime = valBase - Time;

            Value = val;
            Time = valBase;
            Result = 100.0 / diffTime * diffValue;

            //sometimes it is possible that diff_value > diff_timebase
            //limit result to 100%, this is because timing issues during read from pcie controller an latency between IO operation
            if (Result > 100)
                Result = 100;
        }
    }
}
