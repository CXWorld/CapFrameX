namespace CapFrameX.Core.Hardware;

/// <summary>
/// Static CPU information
/// </summary>
public class CpuInfo
{
    /// <summary>
    /// CPU model name
    /// </summary>
    public string Name { get; init; } = "Unknown CPU";

    /// <summary>
    /// CPU vendor (AMD, Intel, etc.)
    /// </summary>
    public string Vendor { get; init; } = "Unknown";

    /// <summary>
    /// Number of physical cores
    /// </summary>
    public int CoreCount { get; init; }

    /// <summary>
    /// Number of logical processors (threads)
    /// </summary>
    public int ThreadCount { get; init; }

    /// <summary>
    /// Base clock speed in MHz
    /// </summary>
    public int BaseClockMhz { get; init; }

    /// <summary>
    /// Maximum clock speed in MHz
    /// </summary>
    public int MaxClockMhz { get; init; }

    /// <summary>
    /// Hwmon path for CPU temperature sensor
    /// </summary>
    public string? HwmonPath { get; set; }

    public override string ToString() => Name;
}

/// <summary>
/// Real-time CPU metrics from sysfs
/// </summary>
public class CpuMetrics
{
    /// <summary>
    /// CPU package temperature in Celsius
    /// </summary>
    public float? Temperature { get; set; }

    /// <summary>
    /// Per-core temperatures in Celsius
    /// </summary>
    public Dictionary<int, float> CoreTemperatures { get; set; } = new();

    /// <summary>
    /// Current CPU frequency in MHz (average across cores)
    /// </summary>
    public int? FrequencyMhz { get; set; }

    /// <summary>
    /// Per-core frequencies in MHz
    /// </summary>
    public Dictionary<int, int> CoreFrequencies { get; set; } = new();

    /// <summary>
    /// CPU usage percentage (0-100)
    /// </summary>
    public float? UsagePercent { get; set; }

    /// <summary>
    /// Per-core usage percentages
    /// </summary>
    public Dictionary<int, float> CoreUsage { get; set; } = new();

    /// <summary>
    /// CPU power consumption in Watts (from RAPL)
    /// </summary>
    public float? PowerWatts { get; set; }
}
