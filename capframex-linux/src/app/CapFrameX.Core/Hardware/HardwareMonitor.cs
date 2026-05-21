namespace CapFrameX.Core.Hardware;

/// <summary>
/// Main hardware monitoring service combining Vulkan GPU enumeration with sysfs metrics
/// </summary>
public class HardwareMonitor : IDisposable
{
    private readonly SysfsReader _sysfs = new();
    private List<GpuInfo>? _gpus;
    private CpuInfo? _cpu;
    private bool _initialized;

    /// <summary>
    /// All detected GPUs
    /// </summary>
    public IReadOnlyList<GpuInfo> Gpus => _gpus ?? (IReadOnlyList<GpuInfo>)Array.Empty<GpuInfo>();

    /// <summary>
    /// Primary/discrete GPU (first discrete, or first integrated if no discrete)
    /// </summary>
    public GpuInfo? PrimaryGpu => _gpus?.FirstOrDefault(g => g.Type == GpuType.Discrete)
                                  ?? _gpus?.FirstOrDefault();

    /// <summary>
    /// Integrated GPU (if present alongside discrete)
    /// </summary>
    public GpuInfo? IntegratedGpu => _gpus?.FirstOrDefault(g => g.Type == GpuType.Integrated);

    /// <summary>
    /// CPU information
    /// </summary>
    public CpuInfo? Cpu => _cpu;

    /// <summary>
    /// Initialize hardware detection
    /// </summary>
    public void Initialize()
    {
        if (_initialized) return;

        // Enumerate GPUs via Vulkan
        _gpus = VulkanGpuEnumerator.EnumerateGpus();

        // Map sysfs paths for metrics
        _sysfs.MapGpuPaths(_gpus);

        // Get CPU info
        _cpu = _sysfs.GetCpuInfo();

        _initialized = true;
    }

    /// <summary>
    /// Get current metrics for a GPU
    /// </summary>
    public GpuMetrics? GetGpuMetrics(GpuInfo? gpu = null)
    {
        gpu ??= PrimaryGpu;
        if (gpu == null) return null;

        return _sysfs.GetGpuMetrics(gpu);
    }

    /// <summary>
    /// Get current CPU metrics
    /// </summary>
    public CpuMetrics? GetCpuMetrics()
    {
        if (_cpu == null) return null;
        return _sysfs.GetCpuMetrics(_cpu);
    }

    /// <summary>
    /// Get memory info
    /// </summary>
    public (ulong TotalBytes, ulong AvailableBytes) GetMemoryInfo()
    {
        return _sysfs.GetMemoryInfo();
    }

    /// <summary>
    /// Get motherboard info
    /// </summary>
    public (string Vendor, string Name) GetMotherboardInfo()
    {
        return _sysfs.GetMotherboardInfo();
    }

    /// <summary>
    /// Get kernel version
    /// </summary>
    public string GetKernelVersion() => _sysfs.GetKernelVersion();

    /// <summary>
    /// Get distribution name
    /// </summary>
    public string GetDistribution() => _sysfs.GetDistribution();

    /// <summary>
    /// Format bytes to human readable string
    /// </summary>
    public static string FormatBytes(ulong bytes)
    {
        if (bytes >= 1024UL * 1024 * 1024 * 1024)
            return $"{bytes / (1024.0 * 1024 * 1024 * 1024):F1} TB";
        if (bytes >= 1024UL * 1024 * 1024)
            return $"{bytes / (1024.0 * 1024 * 1024):F1} GB";
        if (bytes >= 1024UL * 1024)
            return $"{bytes / (1024.0 * 1024):F1} MB";
        if (bytes >= 1024)
            return $"{bytes / 1024.0:F1} KB";
        return $"{bytes} B";
    }

    public void Dispose()
    {
        // Nothing to dispose currently, but interface for future cleanup
    }
}
