namespace CapFrameX.Core.Hardware;

/// <summary>
/// Reads hardware information and metrics from Linux sysfs/hwmon interfaces
/// </summary>
public class SysfsReader
{
    private const string HwmonBasePath = "/sys/class/hwmon";
    private const string DrmBasePath = "/sys/class/drm";
    private const string CpuBasePath = "/sys/devices/system/cpu";
    private const string RaplBasePath = "/sys/class/powercap/intel-rapl";

    private readonly Dictionary<string, string> _hwmonNameCache = new();
    private long _lastCpuIdle;
    private long _lastCpuTotal;

    #region CPU Info

    /// <summary>
    /// Get static CPU information
    /// </summary>
    public CpuInfo GetCpuInfo()
    {
        var name = "Unknown CPU";
        var vendor = "Unknown";
        int coreCount = 0;
        int threadCount = Environment.ProcessorCount;
        int maxClockMhz = 0;

        try
        {
            var lines = File.ReadAllLines("/proc/cpuinfo");
            var coreIds = new HashSet<string>();

            foreach (var line in lines)
            {
                if (line.StartsWith("model name") && name == "Unknown CPU")
                {
                    var idx = line.IndexOf(':');
                    if (idx > 0) name = line[(idx + 1)..].Trim();
                }
                else if (line.StartsWith("vendor_id") && vendor == "Unknown")
                {
                    var idx = line.IndexOf(':');
                    if (idx > 0) vendor = line[(idx + 1)..].Trim();
                }
                else if (line.StartsWith("core id"))
                {
                    var idx = line.IndexOf(':');
                    if (idx > 0) coreIds.Add(line[(idx + 1)..].Trim());
                }
            }

            coreCount = coreIds.Count > 0 ? coreIds.Count : threadCount;
        }
        catch { }

        // Get max frequency
        try
        {
            var maxFreqPath = $"{CpuBasePath}/cpu0/cpufreq/cpuinfo_max_freq";
            if (File.Exists(maxFreqPath))
            {
                var khz = int.Parse(File.ReadAllText(maxFreqPath).Trim());
                maxClockMhz = khz / 1000;
            }
        }
        catch { }

        return new CpuInfo
        {
            Name = name,
            Vendor = vendor,
            CoreCount = coreCount,
            ThreadCount = threadCount,
            MaxClockMhz = maxClockMhz,
            HwmonPath = FindHwmonByName("coretemp") ?? FindHwmonByName("k10temp") ?? FindHwmonByName("zenpower")
        };
    }

    /// <summary>
    /// Get real-time CPU metrics
    /// </summary>
    public CpuMetrics GetCpuMetrics(CpuInfo cpuInfo)
    {
        var metrics = new CpuMetrics();

        // Temperature
        if (cpuInfo.HwmonPath != null)
        {
            metrics.Temperature = ReadHwmonTemp(cpuInfo.HwmonPath, "temp1_input");

            // Per-core temps
            for (int i = 2; i <= 64; i++)
            {
                var coreTemp = ReadHwmonTemp(cpuInfo.HwmonPath, $"temp{i}_input");
                if (coreTemp.HasValue)
                {
                    var labelPath = Path.Combine(cpuInfo.HwmonPath, $"temp{i}_label");
                    if (File.Exists(labelPath))
                    {
                        var label = File.ReadAllText(labelPath).Trim();
                        if (label.StartsWith("Core ") && int.TryParse(label[5..], out var coreId))
                        {
                            metrics.CoreTemperatures[coreId] = coreTemp.Value;
                        }
                    }
                }
            }
        }

        // Frequencies
        var frequencies = new List<int>();
        for (int i = 0; i < cpuInfo.ThreadCount; i++)
        {
            var freqPath = $"{CpuBasePath}/cpu{i}/cpufreq/scaling_cur_freq";
            if (File.Exists(freqPath))
            {
                try
                {
                    var khz = int.Parse(File.ReadAllText(freqPath).Trim());
                    var mhz = khz / 1000;
                    frequencies.Add(mhz);
                    metrics.CoreFrequencies[i] = mhz;
                }
                catch { }
            }
        }

        if (frequencies.Count > 0)
        {
            metrics.FrequencyMhz = (int)frequencies.Average();
        }

        // CPU usage from /proc/stat
        metrics.UsagePercent = GetCpuUsage();

        // Power from RAPL
        metrics.PowerWatts = GetRaplPower();

        return metrics;
    }

    private float? GetCpuUsage()
    {
        try
        {
            var line = File.ReadAllLines("/proc/stat")[0];
            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length < 5 || parts[0] != "cpu") return null;

            long user = long.Parse(parts[1]);
            long nice = long.Parse(parts[2]);
            long system = long.Parse(parts[3]);
            long idle = long.Parse(parts[4]);
            long iowait = parts.Length > 5 ? long.Parse(parts[5]) : 0;
            long irq = parts.Length > 6 ? long.Parse(parts[6]) : 0;
            long softirq = parts.Length > 7 ? long.Parse(parts[7]) : 0;

            long total = user + nice + system + idle + iowait + irq + softirq;
            long idleTime = idle + iowait;

            if (_lastCpuTotal == 0)
            {
                _lastCpuIdle = idleTime;
                _lastCpuTotal = total;
                return null;
            }

            long totalDiff = total - _lastCpuTotal;
            long idleDiff = idleTime - _lastCpuIdle;

            _lastCpuIdle = idleTime;
            _lastCpuTotal = total;

            if (totalDiff == 0) return 0;

            return (1.0f - (float)idleDiff / totalDiff) * 100f;
        }
        catch { return null; }
    }

    private float? GetRaplPower()
    {
        try
        {
            // Intel RAPL: /sys/class/powercap/intel-rapl/intel-rapl:0/energy_uj
            var energyPath = $"{RaplBasePath}/intel-rapl:0/energy_uj";
            if (!File.Exists(energyPath)) return null;

            // Would need to track over time for power calculation
            // For now, return null - needs proper implementation with timing
            return null;
        }
        catch { return null; }
    }

    #endregion

    #region GPU Metrics

    /// <summary>
    /// Map GPUs to their DRM card and hwmon paths
    /// </summary>
    public void MapGpuPaths(List<GpuInfo> gpus)
    {
        foreach (var gpu in gpus)
        {
            // Find DRM card by PCI vendor/device ID
            gpu.DrmCardPath = FindDrmCard(gpu.VendorId, gpu.DeviceId);

            // Find hwmon by driver name or DRM association
            if (gpu.DrmCardPath != null)
            {
                gpu.HwmonPath = FindHwmonForDrmCard(gpu.DrmCardPath);
            }

            // Fallback: find by driver name
            if (gpu.HwmonPath == null)
            {
                gpu.HwmonPath = gpu.Vendor switch
                {
                    GpuVendor.Amd => FindHwmonByName("amdgpu"),
                    GpuVendor.Intel => FindHwmonByName("i915"),
                    _ => null
                };
            }
        }
    }

    /// <summary>
    /// Get real-time GPU metrics
    /// </summary>
    public GpuMetrics GetGpuMetrics(GpuInfo gpu)
    {
        var metrics = new GpuMetrics();

        if (gpu.HwmonPath != null)
        {
            // Temperature
            metrics.Temperature = ReadHwmonTemp(gpu.HwmonPath, "temp1_input");
            metrics.TemperatureHotspot = ReadHwmonTemp(gpu.HwmonPath, "temp2_input");
            metrics.TemperatureMemory = ReadHwmonTemp(gpu.HwmonPath, "temp3_input");

            // Power
            metrics.PowerWatts = ReadHwmonValue(gpu.HwmonPath, "power1_average", 1_000_000f); // microwatts to watts
            metrics.PowerCapWatts = ReadHwmonValue(gpu.HwmonPath, "power1_cap", 1_000_000f);

            // Clocks
            metrics.CoreClockMhz = (int?)ReadHwmonValue(gpu.HwmonPath, "freq1_input", 1_000_000f); // Hz to MHz
            metrics.MemoryClockMhz = (int?)ReadHwmonValue(gpu.HwmonPath, "freq2_input", 1_000_000f);

            // Fan
            metrics.FanRpm = (int?)ReadHwmonValue(gpu.HwmonPath, "fan1_input", 1f);
            var pwm = ReadHwmonValue(gpu.HwmonPath, "pwm1", 1f);
            if (pwm.HasValue)
            {
                metrics.FanPercent = (int)(pwm.Value / 255f * 100f);
            }

            // Voltage
            metrics.VoltageMv = (int?)ReadHwmonValue(gpu.HwmonPath, "in0_input", 1f);
        }

        // GPU usage and VRAM from DRM
        if (gpu.DrmCardPath != null)
        {
            var devicePath = Path.Combine(gpu.DrmCardPath, "device");

            // GPU usage (AMD only)
            var busyPath = Path.Combine(devicePath, "gpu_busy_percent");
            if (File.Exists(busyPath))
            {
                try
                {
                    metrics.UsagePercent = int.Parse(File.ReadAllText(busyPath).Trim());
                }
                catch { }
            }

            // VRAM
            var vramTotalPath = Path.Combine(devicePath, "mem_info_vram_total");
            var vramUsedPath = Path.Combine(devicePath, "mem_info_vram_used");

            if (File.Exists(vramTotalPath))
            {
                try { metrics.VramTotal = ulong.Parse(File.ReadAllText(vramTotalPath).Trim()); } catch { }
            }
            if (File.Exists(vramUsedPath))
            {
                try { metrics.VramUsed = ulong.Parse(File.ReadAllText(vramUsedPath).Trim()); } catch { }
            }
        }

        return metrics;
    }

    #endregion

    #region Memory Info

    /// <summary>
    /// Get total system RAM
    /// </summary>
    public (ulong TotalBytes, ulong AvailableBytes) GetMemoryInfo()
    {
        ulong total = 0;
        ulong available = 0;

        try
        {
            foreach (var line in File.ReadLines("/proc/meminfo"))
            {
                if (line.StartsWith("MemTotal:"))
                {
                    var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 2) total = ulong.Parse(parts[1]) * 1024; // kB to bytes
                }
                else if (line.StartsWith("MemAvailable:"))
                {
                    var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 2) available = ulong.Parse(parts[1]) * 1024;
                }
            }
        }
        catch { }

        return (total, available);
    }

    #endregion

    #region System Info

    /// <summary>
    /// Get motherboard info
    /// </summary>
    public (string Vendor, string Name) GetMotherboardInfo()
    {
        var vendor = ReadSysFile("/sys/class/dmi/id/board_vendor") ?? "Unknown";
        var name = ReadSysFile("/sys/class/dmi/id/board_name") ?? "Unknown";
        return (vendor, name);
    }

    /// <summary>
    /// Get kernel version
    /// </summary>
    public string GetKernelVersion()
    {
        try
        {
            var version = File.ReadAllText("/proc/version");
            return version.Split(' ')[2];
        }
        catch { return "Unknown"; }
    }

    /// <summary>
    /// Get distribution name
    /// </summary>
    public string GetDistribution()
    {
        try
        {
            foreach (var line in File.ReadLines("/etc/os-release"))
            {
                if (line.StartsWith("PRETTY_NAME="))
                {
                    return line[12..].Trim('"');
                }
            }
        }
        catch { }
        return "Linux";
    }

    #endregion

    #region Helpers

    private string? FindHwmonByName(string name)
    {
        try
        {
            foreach (var hwmon in Directory.GetDirectories(HwmonBasePath))
            {
                var namePath = Path.Combine(hwmon, "name");
                if (File.Exists(namePath))
                {
                    var hwmonName = File.ReadAllText(namePath).Trim();
                    if (hwmonName == name)
                    {
                        return hwmon;
                    }
                }
            }
        }
        catch { }
        return null;
    }

    private string? FindDrmCard(uint vendorId, uint deviceId)
    {
        try
        {
            var vendorHex = $"0x{vendorId:x4}";
            var deviceHex = $"0x{deviceId:x4}";

            foreach (var card in Directory.GetDirectories(DrmBasePath, "card*"))
            {
                // Skip connectors (card0-DP-1, etc.)
                if (Path.GetFileName(card).Contains('-')) continue;

                var vendorPath = Path.Combine(card, "device/vendor");
                var devicePath = Path.Combine(card, "device/device");

                if (File.Exists(vendorPath) && File.Exists(devicePath))
                {
                    var v = File.ReadAllText(vendorPath).Trim();
                    var d = File.ReadAllText(devicePath).Trim();

                    if (v == vendorHex && d == deviceHex)
                    {
                        return card;
                    }
                }
            }
        }
        catch { }
        return null;
    }

    private string? FindHwmonForDrmCard(string drmCardPath)
    {
        try
        {
            var hwmonDir = Path.Combine(drmCardPath, "device/hwmon");
            if (Directory.Exists(hwmonDir))
            {
                var hwmons = Directory.GetDirectories(hwmonDir);
                if (hwmons.Length > 0) return hwmons[0];
            }
        }
        catch { }
        return null;
    }

    private float? ReadHwmonTemp(string hwmonPath, string file)
    {
        var value = ReadHwmonValue(hwmonPath, file, 1000f); // millidegrees to degrees
        return value;
    }

    private float? ReadHwmonValue(string hwmonPath, string file, float divisor)
    {
        try
        {
            var path = Path.Combine(hwmonPath, file);
            if (File.Exists(path))
            {
                var text = File.ReadAllText(path).Trim();
                if (long.TryParse(text, out var value))
                {
                    return value / divisor;
                }
            }
        }
        catch { }
        return null;
    }

    private string? ReadSysFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                return File.ReadAllText(path).Trim();
            }
        }
        catch { }
        return null;
    }

    #endregion
}
