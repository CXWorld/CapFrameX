using CapFrameX.Core.Hardware;

namespace CapFrameX.Core.System;

public class SystemInfo
{
    public string CpuName { get; set; } = "Unknown";
    public int CpuCores { get; set; }
    public int CpuThreads { get; set; }
    public string TotalRam { get; set; } = "Unknown";
    public string Motherboard { get; set; } = "Unknown";
    public string GpuName { get; set; } = "Unknown";
    public string IgpuName { get; set; } = "";
    public bool HasIgpu => !string.IsNullOrEmpty(IgpuName);
    public string KernelVersion { get; set; } = "Unknown";
    public string Distribution { get; set; } = "Unknown";

    // Extended info from HardwareMonitor
    public string GpuDriverVersion { get; set; } = "";
    public string GpuVram { get; set; } = "";
    public string GpuType { get; set; } = "";
}

public interface ISystemInfoService
{
    SystemInfo GetSystemInfo();
    HardwareMonitor HardwareMonitor { get; }
}

public class SystemInfoService : ISystemInfoService, IDisposable
{
    private SystemInfo? _cachedInfo;
    private readonly HardwareMonitor _hardwareMonitor = new();

    public HardwareMonitor HardwareMonitor => _hardwareMonitor;

    public SystemInfo GetSystemInfo()
    {
        if (_cachedInfo != null)
            return _cachedInfo;

        // Initialize hardware monitor
        _hardwareMonitor.Initialize();

        var cpu = _hardwareMonitor.Cpu;
        var primaryGpu = _hardwareMonitor.PrimaryGpu;
        var igpu = _hardwareMonitor.IntegratedGpu;
        var (totalRam, _) = _hardwareMonitor.GetMemoryInfo();
        var (mbVendor, mbName) = _hardwareMonitor.GetMotherboardInfo();

        _cachedInfo = new SystemInfo
        {
            CpuName = cpu?.Name ?? "Unknown CPU",
            CpuCores = cpu?.CoreCount ?? Environment.ProcessorCount,
            CpuThreads = cpu?.ThreadCount ?? Environment.ProcessorCount,
            TotalRam = HardwareMonitor.FormatBytes(totalRam),
            Motherboard = $"{mbVendor} {mbName}",
            GpuName = primaryGpu?.Name ?? "Unknown GPU",
            GpuDriverVersion = primaryGpu?.DriverVersion ?? "",
            GpuVram = primaryGpu?.MemorySize > 0 ? HardwareMonitor.FormatBytes(primaryGpu.MemorySize) : "",
            GpuType = primaryGpu?.Type.ToString() ?? "",
            // Only show iGPU if there's also a discrete GPU
            IgpuName = primaryGpu?.Type == GpuType.Discrete ? (igpu?.Name ?? "") : "",
            KernelVersion = _hardwareMonitor.GetKernelVersion(),
            Distribution = _hardwareMonitor.GetDistribution()
        };

        return _cachedInfo;
    }

    public void Dispose()
    {
        _hardwareMonitor.Dispose();
    }
}
