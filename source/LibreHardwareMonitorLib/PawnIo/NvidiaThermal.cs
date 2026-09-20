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
    internal const int MemoryTemperatureSensorCount = 48;
    private const int MemoryTemperatureOutputLength = MemoryTemperatureFirstSensorIndex + MemoryTemperatureSensorCount;
    private const long MemoryTemperatureUnavailable = int.MinValue;
    private const int ReadTimeoutMilliseconds = 1000;
    private const int ThermalChannelCount = 6;
    private const string ReadMemoryTemperatures = "ioctl_read_memory_temperatures";
    private const string ReadThermalRegisters = "ioctl_read_thermal_registers";

    private static readonly long CacheLifetimeTicks = MillisecondsToStopwatchTicks(CacheLifetimeMilliseconds);
    private static readonly long ReadTimeoutTicks = MillisecondsToStopwatchTicks(ReadTimeoutMilliseconds);

    private readonly string _deviceAddress;
    private readonly float?[] _cachedMemoryTemperatures = new float?[MemoryTemperatureSensorCount];
    private readonly long[] _input = new long[3];
    private readonly long[] _memoryTemperatureOutput = new long[MemoryTemperatureOutputLength];
    private readonly PawnIo _pawnIo = PawnIo.LoadModuleFromResource(
        typeof(NvidiaThermal).Assembly,
        $"{nameof(LibreHardwareMonitor)}.Resources.PawnIO.Nvidia.bin");
    private readonly AutoResetEvent _readRequested = new(false);
    private readonly object _sync = new();
    private readonly long[] _thermalOutput = new long[ThermalChannelCount];
    private readonly Thread _worker;
    private readonly float?[] _workerMemoryTemperatures = new float?[MemoryTemperatureSensorCount];

    private float? _cachedHotSpot;
    private float? _cachedMemoryJunction;
    private bool _cachedMemoryReadSucceeded;
    private long _cacheTimestamp;
    private bool _closeRequested;
    private int _consecutiveFailures;
    private int _consecutiveMemoryFailures;
    private bool _disabled;
    private bool _hasCachedData;
    private bool _hasDeliveredData;
    private bool _readInProgress;
    private bool _readMemoryTemperaturesPending;
    private bool _readPending;
    private long _readStartedTimestamp;
    private bool _memoryFailureLogged;
    private bool _memoryResultLogged;

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
                Name = $"NVIDIA thermal reader {_deviceAddress}",
                Priority = ThreadPriority.BelowNormal
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

    public bool TryRead(
        bool readMemoryTemperatures,
        out float? hotSpot,
        out float? memoryJunction,
        float?[] memoryTemperatures,
        out bool memoryReadSucceeded)
    {
        if (memoryTemperatures == null || memoryTemperatures.Length < MemoryTemperatureSensorCount)
            throw new ArgumentException($"At least {MemoryTemperatureSensorCount} memory temperature entries are required.", nameof(memoryTemperatures));

        hotSpot = null;
        memoryJunction = null;
        memoryReadSucceeded = false;
        Array.Clear(memoryTemperatures, 0, MemoryTemperatureSensorCount);

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

            if (!_closeRequested && !_disabled && _worker != null && !_readInProgress)
            {
                // A normal OSD needs only the six thermal registers for the hot-spot
                // value. The optional per-chip path scans up to 48 VRAM locations and
                // several registers per location, so request it only for an explicitly
                // selected per-chip temperature sensor.
                _readMemoryTemperaturesPending |= readMemoryTemperatures;
                if (!_readPending)
                {
                    _readPending = true;
                    requestRead = true;
                }
            }

            hasData = _hasCachedData && !HasElapsed(now, _cacheTimestamp, CacheLifetimeTicks);
            if (hasData)
            {
                hotSpot = _cachedHotSpot;
                memoryJunction = _cachedMemoryJunction;
                memoryReadSucceeded = _cachedMemoryReadSucceeded;
                if (memoryReadSucceeded)
                    Array.Copy(_cachedMemoryTemperatures, memoryTemperatures, MemoryTemperatureSensorCount);

                if (memoryReadSucceeded ||
                    (hotSpot.HasValue && _consecutiveMemoryFailures >= MaxConsecutiveFailures))
                {
                    _hasDeliveredData = true;
                }
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

    internal static bool TryDecodeMemoryTemperatures(
        long[] output,
        uint returnSize,
        float?[] memoryTemperatures,
        out float? memoryJunction)
    {
        memoryJunction = null;

        if (memoryTemperatures == null || memoryTemperatures.Length < MemoryTemperatureSensorCount)
            return false;

        Array.Clear(memoryTemperatures, 0, MemoryTemperatureSensorCount);

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
            {
                Array.Clear(memoryTemperatures, 0, MemoryTemperatureSensorCount);
                memoryJunction = null;
                return false;
            }

            float temperature = raw;
            memoryTemperatures[i] = temperature;
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
                bool readMemoryTemperatures;

                lock (_sync)
                {
                    if (_closeRequested)
                        break;

                    if (_disabled)
                    {
                        _readMemoryTemperaturesPending = false;
                        _readPending = false;
                        continue;
                    }

                    readMemoryTemperatures = _readMemoryTemperaturesPending;
                    _readMemoryTemperaturesPending = false;
                    _readPending = false;
                    _readInProgress = true;
                    _readStartedTimestamp = Stopwatch.GetTimestamp();
                }

                bool thermalSuccess = TryReadThermalHardware(out float? hotSpot);
                float? memoryJunction = null;
                bool memorySuccess = readMemoryTemperatures &&
                    TryReadMemoryHardware(out memoryJunction, _workerMemoryTemperatures);
                bool success = thermalSuccess || memorySuccess;
                bool disableAfterFailures = false;
                bool closeRequested;

                lock (_sync)
                {
                    _readInProgress = false;
                    _readStartedTimestamp = 0;
                    if (readMemoryTemperatures)
                    {
                        _consecutiveMemoryFailures = memorySuccess
                            ? 0
                            : Math.Min(_consecutiveMemoryFailures + 1, MaxConsecutiveFailures);
                    }

                    if (success && !_disabled && !_closeRequested)
                    {
                        _cachedHotSpot = thermalSuccess ? hotSpot : null;
                        _cachedMemoryJunction = readMemoryTemperatures && memorySuccess ? memoryJunction : null;
                        _cachedMemoryReadSucceeded = readMemoryTemperatures && memorySuccess;
                        if (_cachedMemoryReadSucceeded)
                        {
                            Array.Copy(_workerMemoryTemperatures, _cachedMemoryTemperatures, MemoryTemperatureSensorCount);
                        }
                        else
                        {
                            Array.Clear(_cachedMemoryTemperatures, 0, MemoryTemperatureSensorCount);
                            if (thermalSuccess && !readMemoryTemperatures)
                                _hasDeliveredData = true;
                        }

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

    private bool TryReadMemoryHardware(out float? memoryJunction, float?[] memoryTemperatures)
    {
        memoryJunction = null;
        Array.Clear(memoryTemperatures, 0, MemoryTemperatureSensorCount);

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
            {
                if (!_memoryFailureLogged)
                {
                    Log.Warning(
                        "PawnIO NVIDIA memory temperature read for {DeviceAddress} failed with HRESULT 0x{HResult:X8} and return size {ReturnSize}.",
                        _deviceAddress,
                        unchecked((uint)hr),
                        returnSize);
                    _memoryFailureLogged = true;
                }

                return false;
            }

            if (!TryDecodeMemoryTemperatures(_memoryTemperatureOutput, returnSize, memoryTemperatures, out memoryJunction))
            {
                if (!_memoryFailureLogged)
                {
                    Log.Warning(
                        "PawnIO NVIDIA memory temperature read for {DeviceAddress} returned an invalid payload.",
                        _deviceAddress);
                    _memoryFailureLogged = true;
                }

                return false;
            }

            _memoryFailureLogged = false;

            if (!_memoryResultLogged)
            {
                int availableCount = 0;
                foreach (float? temperature in memoryTemperatures)
                {
                    if (temperature.HasValue)
                        availableCount++;
                }

                Log.Information(
                    "PawnIO NVIDIA memory temperatures for {DeviceAddress}: {AvailableCount}/{SensorCount} sensors available, topology 0x{Topology:X8}.",
                    _deviceAddress,
                    availableCount,
                    MemoryTemperatureSensorCount,
                    unchecked((uint)_memoryTemperatureOutput[0]));
                _memoryResultLogged = true;
            }

            return true;
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "PawnIO NVIDIA memory temperature read for {DeviceAddress} failed.", _deviceAddress);
            return false;
        }
    }
}
