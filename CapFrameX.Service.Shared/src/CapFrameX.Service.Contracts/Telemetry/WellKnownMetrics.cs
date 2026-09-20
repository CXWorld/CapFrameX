namespace CapFrameX.Service.Contracts.Telemetry;

/// <summary>
/// Platform-neutral metric vocabulary. Sensor ids stay platform-specific; every telemetry source
/// maps what it has onto these keys, and overlay presets, live tiles and record summaries bind to
/// the keys rather than to a platform's sensor names.
/// </summary>
public static class WellKnownMetrics
{
    /// <summary>Total CPU load in percent.</summary>
    public const string CpuLoad = "cpu.load";

    /// <summary>CPU package power in watts.</summary>
    public const string CpuPower = "cpu.power";

    /// <summary>CPU package temperature in degrees Celsius.</summary>
    public const string CpuTemperature = "cpu.temp";

    /// <summary>CPU clock in megahertz.</summary>
    public const string CpuClock = "cpu.clock";

    /// <summary>CPU is thermally throttling; 1 or 0.</summary>
    public const string CpuThrottle = "cpu.throttle";

    /// <summary>GPU load in percent.</summary>
    public const string GpuLoad = "gpu.load";

    /// <summary>GPU board power in watts.</summary>
    public const string GpuPower = "gpu.power";

    /// <summary>GPU temperature in degrees Celsius.</summary>
    public const string GpuTemperature = "gpu.temp";

    /// <summary>GPU core clock in megahertz.</summary>
    public const string GpuClock = "gpu.clock";

    /// <summary>Used video memory in megabytes.</summary>
    public const string GpuVramUsed = "gpu.vram.used";

    /// <summary>Total video memory in megabytes.</summary>
    public const string GpuVramTotal = "gpu.vram.total";

    /// <summary>GPU is throttling; 1 or 0.</summary>
    public const string GpuThrottle = "gpu.throttle";

    /// <summary>Used system memory in megabytes.</summary>
    public const string RamUsed = "ram.used";

    /// <summary>Total system memory in megabytes.</summary>
    public const string RamTotal = "ram.total";

    /// <summary>Number of CPU cores reported by the telemetry source.</summary>
    public const string CpuCoreCount = "cpu.core.count";

    /// <summary>Load of a single CPU core in percent.</summary>
    /// <param name="index">Zero-based core index.</param>
    public static string CpuCoreLoad(int index) => $"cpu.load.core[{index}]";

    /// <summary>Clock of a single CPU core in megahertz.</summary>
    /// <param name="index">Zero-based core index.</param>
    public static string CpuCoreClock(int index) => $"cpu.clock.core[{index}]";
}
