using System;
using System.Diagnostics;
using System.Threading;
using Serilog;

namespace LibreHardwareMonitor.PawnIo;

internal sealed class NvidiaThermal
{
    private const int CacheLifetimeMilliseconds = 5000;
    private const int HotSpotIndex = 0;
    private const int MaxConsecutiveFailures = 3;
    private const int MemoryTemperatureCountIndex = 1;
    private const int MemoryTemperatureFirstSensorIndex = 2;
    private const int MemoryTemperatureSensorCount = 48;
    private const int MemoryTemperatureOutputLength = MemoryTemperatureFirstSensorIndex + MemoryTemperatureSensorCount;
    private const long MemoryTemperatureUnavailable = int.MinValue;
    private const int ReadTimeoutMilliseconds = 1000;
    private const int ThermalChannelCount = 6;
    private const string ReadMemoryTemperatures = "ioctl_read_memory_temperatures";
    private const string ReadThermalRegisters = "ioctl_read_thermal_registers";

    private static readonly long CacheLifetimeTicks = MillisecondsToStopwatchTicks(CacheLifetimeMilliseconds);
    private static readonly long ReadTimeoutTicks = MillisecondsToStopwatchTicks(ReadTimeoutMilliseconds);

    private readonly string _deviceAddress;
    private readonly long[] _input = new long[3];
    private readonly long[] _memoryTemperatureOutput = new long[MemoryTemperatureOutputLength];
    private readonly PawnIo _pawnIo = PawnIo.LoadModuleFromResource(
        typeof(NvidiaThermal).Assembly,
        $"{nameof(LibreHardwareMonitor)}.Resources.PawnIO.Nvidia.bin");
    private readonly AutoResetEvent _readRequested = new(false);
    private readonly object _sync = new();
    private readonly long[] _thermalOutput = new long[ThermalChannelCount];
    private readonly Thread _worker;

    private float? _cachedHotSpot;
    private float? _cachedMemoryJunction;
    private long _cacheTimestamp;
    private bool _closeRequested;
    private int _consecutiveFailures;
    private bool _disabled;
    private bool _hasCachedData;
    private bool _hasDeliveredData;
    private bool _readInProgress;
    private bool _readPending;
    private long _readStartedTimestamp;

    public NvidiaThermal(uint bus, uint device, uint function)
    {
        _input[0] = bus;
        _input[1] = device;
        _input[2] = function;
        _deviceAddress = $"{bus:X2}:{device:X2}.{function}";

        if (_pawnIo.IsLoaded)
        {
            _worker = new Thread(ReadLoop)
            {
                IsBackground = true,
                Name = $"NVIDIA thermal reader {_deviceAddress}"
            };
            _worker.Start();
        }
    }

    public bool NeedsInitialSample
    {
        get
        {
            lock (_sync)
            {
                return _worker != null && !_closeRequested && !_disabled && !_hasDeliveredData;
            }
        }
    }

    public bool TryRead(out float? hotSpot, out float? memoryJunction)
    {
        hotSpot = null;
        memoryJunction = null;

        bool requestRead = false;
        bool timedOut = false;
        bool hasData;

        lock (_sync)
        {
            long now = Stopwatch.GetTimestamp();

            if (!_disabled && _readInProgress && HasElapsed(now, _readStartedTimestamp, ReadTimeoutTicks))
            {
                _disabled = true;
                _hasCachedData = false;
                timedOut = true;
            }

            if (!_closeRequested && !_disabled && _worker != null && !_readInProgress && !_readPending)
            {
                _readPending = true;
                requestRead = true;
            }

            hasData = _hasCachedData && !HasElapsed(now, _cacheTimestamp, CacheLifetimeTicks);
            if (hasData)
            {
                hotSpot = _cachedHotSpot;
                memoryJunction = _cachedMemoryJunction;
                if (hotSpot.HasValue || memoryJunction.HasValue)
                    _hasDeliveredData = true;
            }
            else
            {
                _hasCachedData = false;
            }
        }

        if (timedOut)
        {
            Log.Warning(
                "PawnIO NVIDIA thermal read for {DeviceAddress} exceeded {TimeoutMilliseconds} ms; disabling direct thermal reads for this session.",
                _deviceAddress,
                ReadTimeoutMilliseconds);
        }

        if (requestRead)
            _readRequested.Set();

        return hasData;
    }

    public void Close()
    {
        bool closeDirectly;

        lock (_sync)
        {
            if (_closeRequested)
                return;

            _closeRequested = true;
            _disabled = true;
            _hasCachedData = false;
            closeDirectly = _worker == null;
        }

        if (closeDirectly)
            _pawnIo.Close();
        else
            _readRequested.Set();
    }

    private static float? DecodeTemperature(long raw)
    {
        uint value = unchecked((uint)raw);
        if ((value & (1u << 30)) == 0)
            return null;

        return (value & 0xFFFF) / 256.0f;
    }

    internal static bool TryDecodeMemoryJunctionTemperature(long[] output, uint returnSize, out float? memoryJunction)
    {
        memoryJunction = null;

        if (output == null || returnSize < MemoryTemperatureFirstSensorIndex || returnSize > output.Length)
            return false;

        long sensorCount = output[MemoryTemperatureCountIndex];
        if (sensorCount < 0 || sensorCount > MemoryTemperatureSensorCount ||
            returnSize < MemoryTemperatureFirstSensorIndex + (uint)sensorCount)
        {
            return false;
        }

        for (int i = 0; i < sensorCount; i++)
        {
            long raw = output[MemoryTemperatureFirstSensorIndex + i];
            if (raw == MemoryTemperatureUnavailable)
                continue;

            if (raw < int.MinValue || raw > int.MaxValue)
                return false;

            float temperature = raw;
            if (!memoryJunction.HasValue || temperature > memoryJunction.Value)
                memoryJunction = temperature;
        }

        return true;
    }

    private static bool HasElapsed(long now, long start, long duration) => now - start >= duration;

    private static long MillisecondsToStopwatchTicks(int milliseconds) =>
        (long)Math.Ceiling(milliseconds * (double)Stopwatch.Frequency / 1000);

    private void ReadLoop()
    {
        try
        {
            while (true)
            {
                _readRequested.WaitOne();

                lock (_sync)
                {
                    if (_closeRequested)
                        break;

                    if (_disabled)
                    {
                        _readPending = false;
                        continue;
                    }

                    _readPending = false;
                    _readInProgress = true;
                    _readStartedTimestamp = Stopwatch.GetTimestamp();
                }

                bool thermalSuccess = TryReadThermalHardware(out float? hotSpot);
                bool memorySuccess = TryReadMemoryHardware(out float? memoryJunction);
                bool success = thermalSuccess || memorySuccess;
                bool disableAfterFailures = false;
                bool closeRequested;

                lock (_sync)
                {
                    _readInProgress = false;
                    _readStartedTimestamp = 0;

                    if (success && !_disabled && !_closeRequested)
                    {
                        _cachedHotSpot = thermalSuccess ? hotSpot : null;
                        _cachedMemoryJunction = memorySuccess ? memoryJunction : null;
                        _cacheTimestamp = Stopwatch.GetTimestamp();
                        _consecutiveFailures = 0;
                        _hasCachedData = true;
                    }
                    else if (!success && !_disabled && !_closeRequested)
                    {
                        _consecutiveFailures++;
                        if (_consecutiveFailures >= MaxConsecutiveFailures)
                        {
                            _disabled = true;
                            _hasCachedData = false;
                            disableAfterFailures = true;
                        }
                    }

                    closeRequested = _closeRequested;
                }

                if (disableAfterFailures)
                {
                    Log.Warning(
                        "PawnIO NVIDIA thermal reads for {DeviceAddress} failed {FailureCount} consecutive times; disabling direct thermal reads for this session.",
                        _deviceAddress,
                        MaxConsecutiveFailures);
                }

                if (closeRequested)
                    break;
            }
        }
        finally
        {
            _pawnIo.Close();
        }
    }

    private bool TryReadThermalHardware(out float? hotSpot)
    {
        hotSpot = null;

        try
        {
            int hr = _pawnIo.ExecuteHr(ReadThermalRegisters, _input, 3, _thermalOutput, ThermalChannelCount, out uint returnSize);
            if (hr != 0 || returnSize != ThermalChannelCount)
                return false;

            hotSpot = DecodeTemperature(_thermalOutput[HotSpotIndex]);
            return true;
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "PawnIO NVIDIA thermal read for {DeviceAddress} failed.", _deviceAddress);
            return false;
        }
    }

    private bool TryReadMemoryHardware(out float? memoryJunction)
    {
        memoryJunction = null;

        try
        {
            int hr = _pawnIo.ExecuteHr(
                ReadMemoryTemperatures,
                _input,
                3,
                _memoryTemperatureOutput,
                MemoryTemperatureOutputLength,
                out uint returnSize);
            if (hr != 0 || returnSize != MemoryTemperatureOutputLength)
                return false;

            return TryDecodeMemoryJunctionTemperature(_memoryTemperatureOutput, returnSize, out memoryJunction);
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "PawnIO NVIDIA memory temperature read for {DeviceAddress} failed.", _deviceAddress);
            return false;
        }
    }
}
