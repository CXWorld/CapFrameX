namespace CapFrameX.Core.Hardware;

/// <summary>
/// GPU type classification
/// </summary>
public enum GpuType
{
    Unknown,
    Integrated,
    Discrete,
    Virtual,
    Cpu
}

/// <summary>
/// GPU vendor
/// </summary>
public enum GpuVendor
{
    Unknown,
    Amd,
    Nvidia,
    Intel
}

/// <summary>
/// Static GPU information from Vulkan enumeration
/// </summary>
public class GpuInfo
{
    /// <summary>
    /// GPU device name from Vulkan driver
    /// </summary>
    public string Name { get; init; } = "Unknown GPU";

    /// <summary>
    /// GPU vendor
    /// </summary>
    public GpuVendor Vendor { get; init; } = GpuVendor.Unknown;

    /// <summary>
    /// GPU type (discrete, integrated, etc.)
    /// </summary>
    public GpuType Type { get; init; } = GpuType.Unknown;

    /// <summary>
    /// Vulkan driver version
    /// </summary>
    public string DriverVersion { get; init; } = "";

    /// <summary>
    /// Vulkan API version supported
    /// </summary>
    public string ApiVersion { get; init; } = "";

    /// <summary>
    /// PCI vendor ID (e.g., 0x1002 for AMD, 0x10DE for NVIDIA, 0x8086 for Intel)
    /// </summary>
    public uint VendorId { get; init; }

    /// <summary>
    /// PCI device ID
    /// </summary>
    public uint DeviceId { get; init; }

    /// <summary>
    /// DRM card path (e.g., /sys/class/drm/card0)
    /// </summary>
    public string? DrmCardPath { get; set; }

    /// <summary>
    /// Hwmon path for this GPU (e.g., /sys/class/hwmon/hwmon2)
    /// </summary>
    public string? HwmonPath { get; set; }

    /// <summary>
    /// Device memory size in bytes (if available)
    /// </summary>
    public ulong MemorySize { get; init; }

    public override string ToString() => $"{Name} ({Type})";
}

/// <summary>
/// Real-time GPU metrics from sysfs/hwmon
/// </summary>
public class GpuMetrics
{
    /// <summary>
    /// GPU temperature in Celsius (edge/die temp)
    /// </summary>
    public float? Temperature { get; set; }

    /// <summary>
    /// GPU hotspot/junction temperature in Celsius
    /// </summary>
    public float? TemperatureHotspot { get; set; }

    /// <summary>
    /// GPU memory temperature in Celsius
    /// </summary>
    public float? TemperatureMemory { get; set; }

    /// <summary>
    /// GPU core clock in MHz
    /// </summary>
    public int? CoreClockMhz { get; set; }

    /// <summary>
    /// GPU memory clock in MHz
    /// </summary>
    public int? MemoryClockMhz { get; set; }

    /// <summary>
    /// GPU usage percentage (0-100)
    /// </summary>
    public int? UsagePercent { get; set; }

    /// <summary>
    /// GPU power consumption in Watts
    /// </summary>
    public float? PowerWatts { get; set; }

    /// <summary>
    /// GPU power limit/cap in Watts
    /// </summary>
    public float? PowerCapWatts { get; set; }

    /// <summary>
    /// Fan speed in RPM
    /// </summary>
    public int? FanRpm { get; set; }

    /// <summary>
    /// Fan speed percentage (0-100)
    /// </summary>
    public int? FanPercent { get; set; }

    /// <summary>
    /// VRAM used in bytes
    /// </summary>
    public ulong? VramUsed { get; set; }

    /// <summary>
    /// VRAM total in bytes
    /// </summary>
    public ulong? VramTotal { get; set; }

    /// <summary>
    /// GPU core voltage in mV
    /// </summary>
    public int? VoltageMv { get; set; }
}
