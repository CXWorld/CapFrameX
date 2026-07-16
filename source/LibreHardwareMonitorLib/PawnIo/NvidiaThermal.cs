using System;
using System.Diagnostics;
using System.Threading;
using Serilog;

namespace LibreHardwareMonitor.PawnIo;

internal sealed class NvidiaThermal
{
    private const int CacheLifetimeMilliseconds = 5000;
    private const int HotSpotIndex = 0;
    private const int HotSpot2Index = 1;
    private const int MaxConsecutiveFailures = 3;
    private const int OutputLength = 2;
    private const int ReadTimeoutMilliseconds = 1000;
    private const string ReadThermalRegisters = "ioctl_read_thermal_registers";

    private static readonly long CacheLifetimeTicks = MillisecondsToStopwatchTicks(CacheLifetimeMilliseconds);
    private static readonly long ReadTimeoutTicks = MillisecondsToStopwatchTicks(ReadTimeoutMilliseconds);

    private readonly string _deviceAddress;
    private readonly long[] _input = new long[3];
    private readonly long[] _output = new long[OutputLength];
    private readonly PawnIo _pawnIo = PawnIo.LoadModuleFromResource(
        typeof(NvidiaThermal).Assembly,
        $"{nameof(LibreHardwareMonitor)}.Resources.PawnIO.Nvidia.bin");
    private readonly AutoResetEvent _readRequested = new(false);
    private readonly object _sync = new();
    private readonly Thread _worker;

    private float? _cachedHotSpot;
    private float? _cachedHotSpot2;
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

    public bool TryRead(out float? hotSpot, out float? hotSpot2)
    {
        hotSpot = null;
        hotSpot2 = null;

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
                hotSpot2 = _cachedHotSpot2;
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
        uint value = (uint)raw;
        uint whole = (value >> 8) & 0xFF;
        if (whole is 0 or 0xFF)
            return null;

        return whole + (value & 0xFF) / 32.0f;
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

                bool success = TryReadHardware(out float? hotSpot, out float? hotSpot2);
                bool disableAfterFailures = false;
                bool closeRequested;

                lock (_sync)
                {
                    _readInProgress = false;
                    _readStartedTimestamp = 0;

                    if (success && !_disabled && !_closeRequested)
                    {
                        _cachedHotSpot = hotSpot;
                        _cachedHotSpot2 = hotSpot2;
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

    private bool TryReadHardware(out float? hotSpot, out float? hotSpot2)
    {
        hotSpot = null;
        hotSpot2 = null;

        try
        {
            int hr = _pawnIo.ExecuteHr(ReadThermalRegisters, _input, 3, _output, OutputLength, out uint returnSize);
            if (hr != 0 || returnSize < OutputLength)
                return false;

            hotSpot = DecodeTemperature(_output[HotSpotIndex]);
            hotSpot2 = DecodeTemperature(_output[HotSpot2Index]);
            return hotSpot.HasValue || hotSpot2.HasValue;
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "PawnIO NVIDIA thermal read for {DeviceAddress} failed.", _deviceAddress);
            return false;
        }
    }
}
