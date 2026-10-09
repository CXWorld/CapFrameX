using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using CapFrameX.Contracts.Sensor;
using CapFrameX.Contracts.Overlay;
using CapFrameX.Monitoring.Contracts;
using CapFrameX.PresentMonInterface;
using CapFrameX.Statistics.NetStandard.Contracts;

namespace CapFrameX.OSD.Integration
{
    /// <summary>
    /// Discovers every CapFrameX sensor and reads the existing shared telemetry streams.
    /// The host owns visibility leases; neither discovery nor a subscription changes profiles.
    /// </summary>
    public sealed class OverlayTelemetryService : IOverlayTelemetryService
    {
        // Native CPU sensor names are defined by GenericCpu, IntelCpu and Amd17Cpu.
        // Restrict suffixes so temperatures, effective clocks and aggregates cannot mix.
        private static readonly Regex CoreSensorName = new Regex(
            @"^(?:CPU\s+)?Core\s+#(?<core>\d{1,5})(?:\s+(?:LPE|LP|P|E|D))?(?:\s+Thread\s+#(?<thread>\d{1,3}))?\s*(?<suffix>\(Effective\)|Distance to TjMax|\(SMU\)|VID)?$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private readonly ISensorService _sensors;
        private readonly ISensorConfig _config;
        private readonly IOnlineMetricService _metrics;
        private readonly IOverlayEntryProvider _classic;
        private readonly IOverlayService _overlay;
        private readonly IHookOverlayStatusService _hookStatus;
        private readonly SemaphoreSlim _catalogLock = new SemaphoreSlim(1, 1);
        private readonly object _lifecycleLock = new object();
        private readonly HashSet<IDisposable> _leases = new HashSet<IDisposable>();
        private readonly ReplaySubject<bool> _stop = new ReplaySubject<bool>(1);
        private volatile OverlayTelemetrySource[] _sources = Array.Empty<OverlayTelemetrySource>();
        private volatile bool _disposed;

        public IObservable<OverlayTelemetrySnapshot> Snapshots { get; }

        public OverlayTelemetryService(ISensorService sensors, ISensorConfig config,
            IOnlineMetricService metrics = null, IOverlayEntryProvider classic = null,
            IOverlayService overlay = null, IHookOverlayStatusService hookStatus = null)
        {
            _sensors = sensors ?? throw new ArgumentNullException(nameof(sensors));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _metrics = metrics;
            _classic = classic;
            _overlay = overlay;
            _hookStatus = hookStatus;
            Snapshots = sensors.SensorSnapshotStream
                // Formatting and statistics must not hold up the single hardware polling worker.
                .ObserveOn(TaskPoolScheduler.Default)
                .TakeUntil(_stop)
                .Select(snapshot => CreateSnapshot(snapshot.Item1, snapshot.Item2))
                .Publish()
                .RefCount();
        }

        public async Task<IReadOnlyList<OverlayTelemetrySource>> GetSourcesAsync()
        {
            await _catalogLock.WaitAsync().ConfigureAwait(false);
            try
            {
                ThrowIfDisposed();
                // Discovery can invoke vendor APIs even when its completion task is already ready.
                var entries = await Task.Run(async () =>
                    (await _sensors.GetSensorEntries().ConfigureAwait(false))?.ToArray()
                    ?? Array.Empty<ISensorEntry>()).ConfigureAwait(false);
                var sources = BuildHardwareSources(entries).ToList();
                if (_metrics != null) sources.AddRange(CreateMetricSources());
                if (_classic != null)
                {
                    var classicEntries = await _classic.GetTelemetrySourcesAsync().ConfigureAwait(false)
                        ?? Array.Empty<IOverlayEntry>();
                    var known = new HashSet<string>(sources.Select(s => s.Identifier), StringComparer.Ordinal);
                    sources.AddRange(classicEntries.Where(e => e != null && known.Add(e.Identifier))
                        .Select(ClassicSource));
                }
                if (_metrics != null)
                {
                    sources.Add(AdditionalSource("ProcessName", "Selected application", "Performance", "Application", "", "processName", true));
                    sources.Add(AdditionalSource("GraphicsAPI", "Graphics API / runtime", "Performance", "Application", "", "graphicsApi", true));
                }
                if (_overlay != null)
                {
                    sources.Add(AdditionalSource("RunHistoryCount", "Completed runs", "Capture", "Results", "", "runCount", false));
                    sources.Add(AdditionalSource("RunHistoryAggregation", "Aggregated capture result", "Capture", "Results", "", "runAggregation", true));
                    sources.Add(AdditionalSource("RunHistoryOutlierCount", "Outlier runs", "Capture", "Results", "", "runOutlierCount", false));
                }
                AssignSemanticKeys(sources);
                ThrowIfDisposed();
                _sources = sources.ToArray();
                return Array.AsReadOnly(_sources);
            }
            finally
            {
                _catalogLock.Release();
            }
        }

        public IDisposable AcquirePreview()
        {
            lock (_lifecycleLock)
            {
                ThrowIfDisposed();
                var sensorLease = _config.AcquireAllSensors();
                IDisposable metricLease;
                try { metricLease = _metrics?.AcquireTelemetry(); }
                catch { sensorLease?.Dispose(); throw; }
                var demand = new CompositeDisposable(sensorLease ?? Disposable.Empty,
                    metricLease ?? Disposable.Empty);
                IDisposable lease = null;
                lease = Disposable.Create(() =>
                {
                    lock (_lifecycleLock) _leases.Remove(lease);
                    demand.Dispose();
                });
                _leases.Add(lease);
                return lease;
            }
        }

        private static IEnumerable<OverlayTelemetrySource> BuildHardwareSources(IEnumerable<ISensorEntry> entries)
        {
            var uniqueEntries = entries.Where(e => e != null && !string.IsNullOrWhiteSpace(e.Identifier))
                .GroupBy(e => e.Identifier, StringComparer.Ordinal).Select(group => group.First()).ToArray();
            var stableCounts = uniqueEntries.Select(SensorIdentifierHelper.BuildStableIdentifier)
                .Where(id => !string.IsNullOrEmpty(id)).GroupBy(id => id, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
            var deviceCounts = uniqueEntries.GroupBy(DeviceNameKey, StringComparer.Ordinal)
                .ToDictionary(group => group.Key,
                    group => group.Select(RuntimeDeviceKey).Distinct(StringComparer.Ordinal).Count(),
                    StringComparer.Ordinal);

            foreach (var entry in uniqueEntries.OrderBy(e => e.HardwareName, StringComparer.Ordinal)
                .ThenBy(e => e.SensorType, StringComparer.Ordinal).ThenBy(e => e.Name, StringComparer.Ordinal)
                .ThenBy(e => e.Identifier, StringComparer.Ordinal))
            {
                var stable = SensorIdentifierHelper.BuildStableIdentifier(entry);
                bool ambiguous = stable != null && stableCounts[stable] > 1;
                // Qualify *every* duplicate, never publish an alias pointing to an arbitrary device.
                string id = stable == null ? "sensor-id:" + Uri.EscapeDataString(entry.Identifier)
                    : "sensor:" + Uri.EscapeDataString(stable)
                    + (ambiguous ? ":runtime:" + Uri.EscapeDataString(entry.Identifier) : string.Empty);
                string device = DeviceNameKey(entry);
                // Hardware names are stable across sensor index changes, but identical devices
                // must remain separate. Unknown provider identifiers are deliberately isolated.
                string deviceKey = "device:" + Uri.EscapeDataString(device)
                    + (deviceCounts[device] > 1 || string.IsNullOrWhiteSpace(entry.HardwareName)
                        ? ":runtime:" + Uri.EscapeDataString(RuntimeDeviceKey(entry)) : string.Empty);
                var group = GetRepeatedSensorGroup(entry);
                yield return new OverlayTelemetrySource
                {
                    Id = id,
                    Identifier = entry.Identifier,
                    StableIdentifier = stable,
                    Name = entry.Name ?? entry.Identifier,
                    HardwareName = entry.HardwareName ?? entry.HardwareType,
                    HardwareType = entry.HardwareType,
                    SensorType = entry.SensorType,
                    Unit = GetUnit(entry),
                    Category = HardwareCategory(entry.HardwareType),
                    Subcategory = SensorCategory(entry.SensorType),
                    DeviceKey = deviceKey,
                    GroupKey = group.Family == null ? null : deviceKey + "/" + group.Family,
                    GroupName = group.Name,
                    GroupOrder = group.Order,
                    IsHardwareSensor = true,
                    IsStableIdentifierAmbiguous = ambiguous
                };
            }
        }

        private static string DeviceNameKey(ISensorEntry entry)
            => (entry.HardwareType ?? string.Empty) + "/" + (entry.HardwareName ?? string.Empty);

        private static string RuntimeDeviceKey(ISensorEntry entry)
        {
            string identifier = entry.Identifier;
            int indexSeparator = identifier.LastIndexOf('/');
            int typeSeparator = indexSeparator <= 0 ? -1 : identifier.LastIndexOf('/', indexSeparator - 1);
            if (typeSeparator > 0 && int.TryParse(identifier.Substring(indexSeparator + 1),
                    NumberStyles.None, CultureInfo.InvariantCulture, out _)
                && string.Equals(identifier.Substring(typeSeparator + 1, indexSeparator - typeSeparator - 1),
                    entry.SensorType, StringComparison.OrdinalIgnoreCase))
                return identifier.Substring(0, typeSeparator);
            return identifier;
        }

        private static (string Family, string Name, int Order) GetRepeatedSensorGroup(ISensorEntry entry)
        {
            if (entry.HardwareType != "Cpu" || string.IsNullOrWhiteSpace(entry.Name))
                return default;
            var match = CoreSensorName.Match(entry.Name.Trim());
            if (!match.Success) return default;
            int core = int.Parse(match.Groups["core"].Value, CultureInfo.InvariantCulture);
            int thread = match.Groups["thread"].Success
                ? int.Parse(match.Groups["thread"].Value, CultureInfo.InvariantCulture) : 0;
            string suffix = match.Groups["suffix"].Value;
            var family = (entry.SensorType, suffix) switch
            {
                ("Load", "") => ("cpu-loads", "CPU core / thread loads"),
                ("Clock", "") => ("cpu-clocks", "CPU core clocks"),
                ("Clock", "(Effective)") => ("cpu-effective-clocks", "CPU effective clocks"),
                ("Temperature", "") => ("cpu-temperatures", "CPU core temperatures"),
                ("Temperature", "Distance to TjMax") => ("cpu-thermal-headroom", "CPU thermal headroom"),
                ("Power", "(SMU)") => ("cpu-core-power", "CPU core power"),
                ("Voltage", "VID") => ("cpu-core-vid", "CPU core VID"),
                _ => ((string)null, (string)null)
            };
            return (family.Item1, family.Item2, core * 1024 + thread);
        }

        private static string GetUnit(ISensorEntry entry)
        {
            switch (entry.SensorType)
            {
                case "Voltage": return "V";
                case "Current": return "A";
                case "Power": return "W";
                case "Clock": return "MHz";
                case "Temperature": return "°C";
                case "Load": case "Control": case "Level": case "Humidity": return "%";
                case "Frequency": return "Hz";
                case "Fan": return "RPM";
                case "Flow": return "L/h";
                case "Data": return "GB";
                case "SmallData": return "MB";
                // CapFrameX GPU/storage implementations already convert their values to GB/s;
                // upstream LHM network adapters retain bytes/second.
                case "Throughput": return entry.HardwareType == "Network" ? "B/s" : "GB/s";
                case "TimeSpan": return "s";
                case "Timing": return "ns";
                case "Energy": return "mWh";
                case "Noise": return "dBA";
                case "Conductivity": return "µS/cm";
                case "Latency": return "ms";
                case "DataRate": return "MT/s";
                default: return string.Empty;
            }
        }

        private static void AssignSemanticKeys(IList<OverlayTelemetrySource> sources)
        {
            Assign("gpuLoad", "Load", s => IsGpu(s), "GPU Core");
            Assign("gpuTemp", "Temperature", s => IsGpu(s), "GPU Core");
            Assign("gpuPower", "Power", s => IsGpu(s), "GPU TBP", "GPU Power", "GPU Total", "GPU TDP");
            Assign("cpuLoad", "Load", s => s.HardwareType == "Cpu", "CPU Total");
            Assign("cpuTemp", "Temperature", s => s.HardwareType == "Cpu", "CPU Package (Tctl/Tdie)", "CPU Package", "CPU Max Core Temp");
            Assign("ramUsed", "Data", s => s.HardwareType == "Memory", "RAM Used", "Memory Used");
            Assign("vramUsed", "Data", s => IsGpu(s), "GPU Memory Dedicated", "GPU Memory Used", "GPU Memory Allocated");
            Assign("gpuClock", "Clock", s => IsGpu(s), "GPU Core");
            Assign("gpuMemoryClock", "Clock", s => IsGpu(s), "GPU Memory");
            Assign("gpuFan", "Fan", s => IsGpu(s), "GPU Fan", "GPU Fan 1");
            Assign("vramLoad", "Load", s => IsGpu(s), "GPU Memory");
            Assign("cpuClock", "Clock", s => s.HardwareType == "Cpu", "CPU Clock", "CPU Max", "CPU Max Clock");
            Assign("cpuPower", "Power", s => s.HardwareType == "Cpu", "CPU Package");
            Assign("ramLoad", "Load", s => s.HardwareType == "Memory", "RAM Usage", "RAM", "Memory");

            void Assign(string key, string type, Func<OverlayTelemetrySource, bool> filter, params string[] names)
            {
                var matching = sources.Where(s => s.IsHardwareSensor && filter(s)
                    && s.SensorType == type && names.Contains(s.Name, StringComparer.Ordinal)).ToArray();
                if (matching.Select(s => s.HardwareName).Distinct(StringComparer.Ordinal).Take(2).Count() > 1)
                    return;
                foreach (var name in names)
                {
                    var candidates = matching.Where(s => s.Name == name).ToArray();
                    if (candidates.Length == 0) continue;
                    if (candidates.Length == 1 && !candidates[0].IsStableIdentifierAmbiguous)
                        candidates[0].SemanticKey = key;
                    return;
                }
            }
        }

        private static bool IsGpu(OverlayTelemetrySource source)
            => source.HardwareType?.StartsWith("Gpu", StringComparison.Ordinal) == true;

        private static IEnumerable<OverlayTelemetrySource> CreateMetricSources()
        {
            yield return Metric("Framerate", "Current framerate", "FPS", "fpsCurrent");
            yield return Metric("Frametime", "Current frame time", "ms", "frametimeCurrent");
            yield return Metric("DisplayTime", "Display time", "ms", "displayTime");
            yield return Metric("OnlineAverage", "Average FPS", "FPS", "fps");
            yield return Metric("OnlineP1", "1% percentile FPS", "FPS", "fpsP1");
            yield return Metric("OnlineP0dot1", "0.1% percentile FPS", "FPS", "fpsP0dot1");
            yield return Metric("OnlineP0dot2", "0.2% percentile FPS", "FPS", "fpsP0dot2");
            yield return Metric("Online1PercentLow", "1% low FPS", "FPS", "fps1pctLow");
            yield return Metric("Online0dot1PercentLow", "0.1% low FPS", "FPS", "fps0dot1pctLow");
            yield return Metric("Online0dot2PercentLow", "0.2% low FPS", "FPS", "fps0dot2pctLow");
            yield return Metric("OnlineFrameTimeAverage", "Average frame time", "ms", "frametime");
            yield return Metric("OnlineGpuActiveTimeAverage", "Average GPU active time", "ms", "gpuActiveTime");
            yield return Metric("OnlineCpuActiveTimeAverage", "Average CPU active time", "ms", "cpuActiveTime");
            yield return Metric("OnlineGpuActiveTimePercentageDeviation", "GPU active time deviation", "%", "gpuActiveDeviation");
            yield return Metric("OnlineStutteringPercentage", "Stuttering", "%", "stuttering");
            yield return Metric("OnlinePcLatency", "PC latency", "ms", "pcLatency");
            yield return Metric("OnlineAnimationError", "Animation error", "ms", "animationError");
            yield return Metric("PmdGpuPowerCurrent", "PMD GPU power", "W", "pmdGpuPower");
            yield return Metric("PmdCpuPowerCurrent", "PMD CPU power", "W", "pmdCpuPower");
            yield return Metric("PmdSystemPowerCurrent", "PMD system power", "W", "pmdSystemPower");
        }

        private static OverlayTelemetrySource Metric(string identifier, string name, string unit, string semantic = null)
        {
            bool isPmd = identifier.StartsWith("Pmd", StringComparison.Ordinal);
            return new OverlayTelemetrySource
            {
                Id = "metric/" + identifier, Identifier = identifier, StableIdentifier = identifier,
                Name = name, Unit = unit, HardwareName = isPmd ? "PMD" : "PresentMon",
                HardwareType = isPmd ? "PMD" : "Performance", SensorType = isPmd ? "Power" : "Metric",
                Category = isPmd ? "Power" : "Performance", Subcategory = MetricCategory(identifier),
                SemanticKey = semantic
            };
        }

        private static string HardwareCategory(string hardwareType)
            => hardwareType?.StartsWith("Gpu", StringComparison.Ordinal) == true ? "Graphics"
                : hardwareType == "Cpu" ? "Processor" : hardwareType == "Memory" ? "Memory"
                : hardwareType == "Storage" ? "Storage" : hardwareType == "Network" ? "Network"
                : hardwareType == "Battery" ? "Power" : "System";

        private static string SensorCategory(string sensorType)
            => sensorType switch
            {
                "Clock" or "Frequency" or "DataRate" => "Clocks & speed",
                "Temperature" => "Temperatures",
                "Power" or "Energy" or "Voltage" or "Current" => "Power & voltage",
                "Load" or "Factor" or "Level" => "Utilization",
                "Data" or "SmallData" => "Capacity & usage",
                "Fan" or "Control" or "Flow" => "Cooling",
                "Throughput" => "Throughput",
                "TimeSpan" or "Timing" or "Latency" => "Timing & latency",
                _ => "Other sensors"
            };

        private static string MetricCategory(string id)
            => id.StartsWith("Pmd", StringComparison.Ordinal) ? "PMD"
                : id.Contains("PercentLow") ? "Low averages"
                : id.StartsWith("OnlineP", StringComparison.Ordinal) && id != "OnlinePcLatency" ? "Percentiles"
                : id.Contains("Latency") || id.Contains("Animation") ? "Latency & pacing"
                : id.Contains("Stuttering") || id.Contains("Deviation") ? "Stability"
                : id.Contains("ActiveTime") ? "CPU & GPU timing"
                : id.StartsWith("Online", StringComparison.Ordinal) ? "Averages" : "Live";

        private static OverlayTelemetrySource ClassicSource(IOverlayEntry entry)
        {
            string id = entry.Identifier;
            var info = id switch
            {
                "CaptureServiceStatus" => ("Capture", "Status", "", "captureStatus", true),
                "CaptureTimer" => ("Capture", "Status", "s", "captureTimer", false),
                "RunHistory" => ("Capture", "Results", "", "runHistory", true),
                "HookOverlayStatus" => ("Performance", "Overlay", "", "hookStatus", true),
                "FrameGenerationTechnology" => ("Performance", "Frame generation", "", "frameGenerationTechnology", true),
                "FrameGenerationStatus" => ("Performance", "Frame generation", "", "frameGenerationStatus", true),
                "CxAppCpuUsage" => ("System", "CapFrameX", "%", "appCpuLoad", false),
                "SystemTime" => ("System", "Clock", "", "systemTime", true),
                "CustomCPU" => ("Processor", "Information", "", "cpuName", true),
                "CustomGPU" => ("Graphics", "Information", "", "gpuName", true),
                "CustomRAM" => ("Memory", "Information", "", "ramDescription", true),
                "Mainboard" => ("System", "Information", "", "mainboardName", true),
                "OS" => ("System", "Information", "", "osVersion", true),
                "GPUDriver" => ("Graphics", "Information", "", "gpuDriver", true),
                "Resolution" => ("Graphics", "Displays", "", "resolution", true),
                "BatteryLifePercent" => ("Power", "Battery", "%", "batteryLevel", false),
                "BatteryLifeRemaining" => ("Power", "Battery", "min", "batteryRemaining", false),
                "Ping" => ("Network", "Connectivity", "ms", "ping", false),
                _ when id.StartsWith("DisplayResolution:", StringComparison.Ordinal) => ("Graphics", "Displays", "", (string)null, true),
                _ => ("System", "Other", "", (string)null, !entry.IsNumeric)
            };
            return new OverlayTelemetrySource
            {
                Id = "metric/" + id, Identifier = id, StableIdentifier = entry.StableIdentifier ?? id,
                Name = entry.Description ?? id, Unit = info.Item3, Category = info.Item1,
                Subcategory = info.Item2, IsText = info.Item5, SemanticKey = info.Item4,
                // A renderer toggle controls live availability, never whether a source can be
                // placed in a design intended for a different renderer or later session.
                IsAvailable = true, HardwareName = "CapFrameX",
                HardwareType = info.Item1, SensorType = info.Item5 ? "Text" : "Metric"
            };
        }

        private static OverlayTelemetrySource AdditionalSource(string id, string name, string category,
            string subcategory, string unit, string semantic, bool text)
            => new OverlayTelemetrySource
            {
                Id = "metric/" + id, Identifier = id, StableIdentifier = id, Name = name,
                Category = category, Subcategory = subcategory, Unit = unit, SemanticKey = semantic,
                IsText = text, HardwareName = "CapFrameX", HardwareType = category,
                SensorType = text ? "Text" : "Metric"
            };

        private OverlayTelemetrySnapshot CreateSnapshot(DateTime timestamp, Dictionary<ISensorEntry, float> readings)
        {
            var sources = _sources;
            var values = sources.ToDictionary(source => source.Id, source => (double?)null, StringComparer.Ordinal);
            var textValues = new Dictionary<string, object>(StringComparer.Ordinal);
            var runtimeValues = new Dictionary<string, float>(StringComparer.Ordinal);
            if (readings != null)
                foreach (var pair in readings)
                    if (pair.Key?.Identifier != null) runtimeValues[pair.Key.Identifier] = pair.Value;

            foreach (var source in sources)
                if (source.IsHardwareSensor && runtimeValues.TryGetValue(source.Identifier, out float value))
                    values[source.Id] = Finite(value);

            if (_metrics != null)
            {
                var currentFrame = _metrics.GetFrameTelemetrySnapshot();
                var currentAge = currentFrame == null ? TimeSpan.MaxValue : DateTime.UtcNow - currentFrame.TimestampUtc;
                if (currentFrame != null && currentAge >= TimeSpan.Zero && currentAge <= TimeSpan.FromSeconds(2))
                {
                    Set("Framerate", currentFrame.Framerate);
                    Set("Frametime", currentFrame.Frametime);
                    Set("DisplayTime", currentFrame.DisplayTime);
                    textValues["metric/ProcessName"] = NormalizeText(currentFrame.ProcessName);
                    textValues["metric/GraphicsAPI"] = NormalizeText(currentFrame.Runtime);
                }
                // Online metrics retain history when a process stops. Do not present that history
                // as a fresh measurement or substitute a fictitious 0 while a source is absent.
                var lastFrameTimestamp = _metrics.LastFrameTimestampUtc;
                var age = DateTime.UtcNow - lastFrameTimestamp;
                if (age >= TimeSpan.Zero && age <= TimeSpan.FromSeconds(2))
                {
                    Set("OnlineAverage", _metrics.GetOnlineFpsMetricValue(EMetric.Average));
                    Set("OnlineP1", _metrics.GetOnlineFpsMetricValue(EMetric.P1));
                    Set("OnlineP0dot1", _metrics.GetOnlineFpsMetricValue(EMetric.P0dot1));
                    Set("OnlineP0dot2", _metrics.GetOnlineFpsMetricValue(EMetric.P0dot2));
                    Set("Online1PercentLow", _metrics.GetOnlineFpsMetricValue(EMetric.OnePercentLowAverage));
                    Set("Online0dot1PercentLow", _metrics.GetOnlineFpsMetricValue(EMetric.ZerodotOnePercentLowAverage));
                    Set("Online0dot2PercentLow", _metrics.GetOnlineFpsMetricValue(EMetric.ZerodotTwoPercentLowAverage));
                    Set("OnlineFrameTimeAverage", _metrics.GetOnlineFrameTimeMetricValue(EMetric.Average));
                    Set("OnlineGpuActiveTimeAverage", _metrics.GetOnlineGpuActiveTimeMetricValue(EMetric.GpuActiveAverage));
                    Set("OnlineCpuActiveTimeAverage", _metrics.GetOnlineCpuActiveTimeMetricValue(EMetric.CpuActiveAverage));
                    Set("OnlineGpuActiveTimePercentageDeviation", _metrics.GetOnlineGpuActiveTimeDeviationMetricValue());
                    Set("OnlineStutteringPercentage", _metrics.GetOnlineStutteringPercentageValue());
                    Set("OnlinePcLatency", _metrics.GetOnlinePcLatencyAverageValue());
                    Set("OnlineAnimationError", _metrics.GetOnlineAnimationErrorValue());
                }
                var pmd = _metrics.GetPmdTelemetrySnapshot();
                var pmdAge = pmd == null ? TimeSpan.MaxValue : DateTime.UtcNow - pmd.TimestampUtc;
                if (pmd != null && pmdAge >= TimeSpan.Zero && pmdAge <= TimeSpan.FromSeconds(2))
                {
                    Set("PmdGpuPowerCurrent", pmd.GpuPowerCurrent);
                    Set("PmdCpuPowerCurrent", pmd.CpuPowerCurrent);
                    Set("PmdSystemPowerCurrent", pmd.SystemPowerCurrent);
                }
            }
            if (_classic != null)
            {
                var classicValues = _classic.GetTelemetryValues();
                foreach (var source in sources.Where(s => !s.IsHardwareSensor))
                {
                    if (classicValues == null || !classicValues.TryGetValue(source.Identifier, out object raw)) continue;
                    if (source.IsText)
                    {
                        var text = NormalizeText(raw);
                        if (source.Identifier != "GraphicsAPI" || text != null) textValues[source.Id] = text;
                    }
                    else if (raw != null && double.TryParse(Convert.ToString(raw, CultureInfo.InvariantCulture),
                        NumberStyles.Float, CultureInfo.InvariantCulture, out double scalar))
                        values[source.Id] = Finite(scalar);
                }
            }
            if (_overlay != null)
            {
                var history = _overlay.RunHistory ?? Array.Empty<string>();
                var outliers = _overlay.RunHistoryOutlierFlags ?? Array.Empty<bool>();
                var runs = history.Select((r, i) => (r, i))
                    .Where(item => !string.IsNullOrWhiteSpace(item.r) && item.r != "N/A")
                    .Select(item => item.r + (item.i < outliers.Count && outliers[item.i] ? " [outlier]" : "")).ToList();
                Set("RunHistoryCount", _overlay.RunHistoryCount);
                Set("RunHistoryOutlierCount", outliers.Count(outlier => outlier));
                textValues["metric/RunHistoryAggregation"] = NormalizeText(_overlay.RunHistoryAggregation);
                if (!string.IsNullOrWhiteSpace(_overlay.RunHistoryAggregation)) runs.Add(_overlay.RunHistoryAggregation);
                textValues["metric/RunHistory"] = runs.Count == 0 ? null : string.Join("\n", runs);
            }
            var hook = _hookStatus?.Current;
            if (hook != null && hook.ProcessId > 0 && hook.State != EHookOverlayStatus.Disabled
                && HookStatusProbe.TryRead(hook.ProcessId, out var native, out _)
                && HookStatusProbe.GetHeartbeatAgeMilliseconds(native.LastHeartbeatTickMs, (ulong)Environment.TickCount64)
                    is >= 0 and <= 3000)
            {
                textValues["metric/FrameGenerationTechnology"] = HookOverlayStatusEvaluator.DescribeFrameGenerationTechnology(native.FgTechnology);
                textValues["metric/FrameGenerationStatus"] = native.FgActivity == 2 ? "On" : native.FgActivity == 1 ? "Off" : null;
            }
            return new OverlayTelemetrySnapshot(timestamp, values, textValues);

            void Set(string identifier, double value) => values["metric/" + identifier] = Finite(value);
        }

        private static double? Finite(double value) => double.IsFinite(value) ? value : (double?)null;

        private static string NormalizeText(object value)
        {
            string text = value?.ToString();
            return string.IsNullOrWhiteSpace(text) || text == "N/A" || text == "Not available" ? null : text;
        }

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(OverlayTelemetryService));
        }

        public void Dispose()
        {
            IDisposable[] leases;
            lock (_lifecycleLock)
            {
                if (_disposed) return;
                _disposed = true;
                leases = _leases.ToArray();
                _leases.Clear();
            }
            foreach (var lease in leases) lease.Dispose();
            _stop.OnNext(true);
            _stop.OnCompleted();
        }
    }
}
