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
}

public interface ISystemInfoService
{
    SystemInfo GetSystemInfo();
}

public class SystemInfoService : ISystemInfoService
{
    private SystemInfo? _cachedInfo;

    public SystemInfo GetSystemInfo()
    {
        if (_cachedInfo != null)
            return _cachedInfo;

        var (discreteGpu, integratedGpu) = GetGpuNames();

        _cachedInfo = new SystemInfo
        {
            CpuName = GetCpuName(),
            CpuCores = GetCpuCores(),
            CpuThreads = GetCpuThreads(),
            TotalRam = GetTotalRam(),
            Motherboard = GetMotherboard(),
            GpuName = discreteGpu ?? integratedGpu ?? "Unknown GPU",
            IgpuName = discreteGpu != null ? integratedGpu ?? "" : "",
            KernelVersion = GetKernelVersion(),
            Distribution = GetDistribution()
        };

        return _cachedInfo;
    }

    private static string GetCpuName()
    {
        try
        {
            var lines = File.ReadAllLines("/proc/cpuinfo");
            foreach (var line in lines)
            {
                if (line.StartsWith("model name"))
                {
                    var parts = line.Split(':');
                    if (parts.Length > 1)
                        return parts[1].Trim();
                }
            }
        }
        catch { }
        return "Unknown CPU";
    }

    private static int GetCpuCores()
    {
        try
        {
            // Count unique core ids
            var coreIds = new HashSet<string>();
            var lines = File.ReadAllLines("/proc/cpuinfo");
            foreach (var line in lines)
            {
                if (line.StartsWith("core id"))
                {
                    var parts = line.Split(':');
                    if (parts.Length > 1)
                        coreIds.Add(parts[1].Trim());
                }
            }
            return coreIds.Count > 0 ? coreIds.Count : Environment.ProcessorCount;
        }
        catch { }
        return Environment.ProcessorCount;
    }

    private static int GetCpuThreads()
    {
        return Environment.ProcessorCount;
    }

    private static string GetTotalRam()
    {
        try
        {
            var lines = File.ReadAllLines("/proc/meminfo");
            foreach (var line in lines)
            {
                if (line.StartsWith("MemTotal:"))
                {
                    var parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 2 && long.TryParse(parts[1], out var kb))
                    {
                        var gb = kb / 1024.0 / 1024.0;
                        return $"{gb:F1} GB";
                    }
                }
            }
        }
        catch { }
        return "Unknown";
    }

    private static string GetMotherboard()
    {
        try
        {
            var vendor = File.ReadAllText("/sys/class/dmi/id/board_vendor").Trim();
            var name = File.ReadAllText("/sys/class/dmi/id/board_name").Trim();
            return $"{vendor} {name}";
        }
        catch { }
        return "Unknown";
    }

    private static (string? DiscreteGpu, string? IntegratedGpu) GetGpuNames()
    {
        try
        {
            var startInfo = new global::System.Diagnostics.ProcessStartInfo
            {
                FileName = "lspci",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = global::System.Diagnostics.Process.Start(startInfo);
            if (process == null) return (null, null);

            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();

            string? discreteGpu = null;
            string? integratedGpu = null;

            foreach (var line in output.Split('\n'))
            {
                if (line.Contains("VGA compatible controller:") || line.Contains("3D controller:"))
                {
                    // Format: "00:02.0 VGA compatible controller: Intel Corporation ..."
                    // Find the controller type and extract everything after it
                    string gpuPart;
                    if (line.Contains("VGA compatible controller:"))
                    {
                        var idx = line.IndexOf("VGA compatible controller:") + "VGA compatible controller:".Length;
                        gpuPart = line.Substring(idx).Trim();
                    }
                    else
                    {
                        var idx = line.IndexOf("3D controller:") + "3D controller:".Length;
                        gpuPart = line.Substring(idx).Trim();
                    }

                    // Remove revision info like "(rev xx)"
                    var revIndex = gpuPart.LastIndexOf("(rev");
                    if (revIndex > 0)
                        gpuPart = gpuPart.Substring(0, revIndex).Trim();

                    // Categorize as discrete or integrated
                    if (gpuPart.Contains("NVIDIA") || gpuPart.Contains("AMD") || gpuPart.Contains("Radeon"))
                    {
                        discreteGpu ??= CleanGpuName(gpuPart);
                    }
                    else if (gpuPart.Contains("Intel"))
                    {
                        integratedGpu ??= CleanGpuName(gpuPart);
                    }
                    else
                    {
                        discreteGpu ??= CleanGpuName(gpuPart);
                    }
                }
            }

            return (discreteGpu, integratedGpu);
        }
        catch { }
        return (null, null);
    }

    private static string CleanGpuName(string gpuName)
    {
        // Clean up common prefixes
        gpuName = gpuName
            .Replace("Advanced Micro Devices, Inc. [AMD/ATI]", "AMD")
            .Replace("NVIDIA Corporation", "NVIDIA")
            .Replace("Intel Corporation", "Intel")
            .Trim();

        return gpuName;
    }

    private static string GetKernelVersion()
    {
        try
        {
            return File.ReadAllText("/proc/version").Split(' ')[2];
        }
        catch { }
        return "Unknown";
    }

    private static string GetDistribution()
    {
        try
        {
            if (File.Exists("/etc/os-release"))
            {
                var lines = File.ReadAllLines("/etc/os-release");
                foreach (var line in lines)
                {
                    if (line.StartsWith("PRETTY_NAME="))
                    {
                        return line.Substring(12).Trim('"');
                    }
                }
            }
        }
        catch { }
        return "Linux";
    }
}
