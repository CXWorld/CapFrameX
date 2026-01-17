using CapFrameX.Contracts.Sensor;
using PmcReader;
using PmcReader.AMD;
using PmcReader.Intel;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using LibreHardwareMonitor.Hardware;

namespace CapFrameX.PmcReader.Plugin
{
    public class PmcReaderSensorPlugin : IPmcReaderSensorPlugin
    {
        private const string L3Identifier = "pmcreader/cpu/l3-hitrate";
        private const string DramIdentifier = "pmcreader/cpu/dram-bandwidth";
        private const string DramLatencyIdentifier = "pmcreader/cpu/dram-latency";

        private readonly ISensorEntry _l3HitRateEntry = new PmcReaderSensorEntry
        {
            Identifier = L3Identifier,
            SortKey = "6_0",
            Name = "CPU L3 Hit Rate",
            SensorType = SensorType.Load.ToString(),
            HardwareType = HardwareType.Cpu.ToString(),
            IsPresentationDefault = false
        };

        private readonly ISensorEntry _dramBandwidthEntry = new PmcReaderSensorEntry
        {
            Identifier = DramIdentifier,
            SortKey = "6_1",
            Name = "CPU DRAM Bandwidth",
            SensorType = SensorType.Throughput.ToString(),
            HardwareType = HardwareType.Cpu.ToString(),
            IsPresentationDefault = false
        };

        private readonly ISensorEntry _dramLatencyEntry = new PmcReaderSensorEntry
        {
            Identifier = DramLatencyIdentifier,
            SortKey = "6_2",
            Name = "CPU DRAM Latency",
            SensorType = SensorType.Timing.ToString(),
            HardwareType = HardwareType.Cpu.ToString(),
            IsPresentationDefault = false
        };

        private readonly BehaviorSubject<TimeSpan> _updateIntervalSubject
            = new BehaviorSubject<TimeSpan>(TimeSpan.FromSeconds(1));

        private MonitoringConfig _l3Config;
        private int? _l3HitrateMetricIndex;
        private int? _dramLatencyMetricIndex;
        private MonitoringConfig _dramConfig;
        private bool _disposed;

        public string Name => "PmcReader";

        public IObservable<(DateTime, Dictionary<ISensorEntry, float>)> SensorSnapshotStream { get; private set; }

        public async Task InitializeAsync(IObservable<TimeSpan> updateIntervalStream)
        {
            TryInitializeMonitoring();

            updateIntervalStream.Subscribe(_updateIntervalSubject);

            SensorSnapshotStream = _updateIntervalSubject
                .Select(timespan => Observable.Interval(timespan).StartWith(0L))
                .Switch()
                .Select(_ => CaptureSnapshot())
                .Publish()
                .RefCount();

            await Task.CompletedTask;
        }

        public Task<IEnumerable<ISensorEntry>> GetSensorEntriesAsync()
        {
            var entries = new List<ISensorEntry>();
            if (_l3Config != null)
                entries.Add(_l3HitRateEntry);
            if (_dramLatencyMetricIndex.HasValue)
                entries.Add(_dramLatencyEntry);
            if (_dramConfig != null)
                entries.Add(_dramBandwidthEntry);

            return Task.FromResult<IEnumerable<ISensorEntry>>(entries);
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _updateIntervalSubject.Dispose();
            PmcReaderInterop.Close();
        }

        private void TryInitializeMonitoring()
        {
            try
            {
                PmcReaderInterop.Open();
            }
            catch
            {
                return;
            }

            string manufacturer = PmcReaderInterop.GetManufacturerId();
            if (!string.Equals(manufacturer, "GenuineIntel", StringComparison.Ordinal)
                && !string.Equals(manufacturer, "AuthenticAMD", StringComparison.Ordinal))
                return;

            PmcReaderInterop.GetProcessorVersion(out byte family, out byte model, out _);

            L3ConfigInfo l3ConfigInfo = TryCreateL3Config(manufacturer, family, model);
            if (l3ConfigInfo != null)
            {
                _l3Config = l3ConfigInfo.Config;
                _l3HitrateMetricIndex = l3ConfigInfo.HitrateMetricIndex;
                _dramLatencyMetricIndex = TryGetDramLatencyColumnIndex(_l3Config);
                _l3Config.Initialize();
            }

            _dramConfig = TryCreateDramConfig(manufacturer, family, model);
            if (_dramConfig != null)
                _dramConfig.Initialize();
        }

        private static L3ConfigInfo TryCreateL3Config(string manufacturer, byte family, byte model)
        {
            MonitoringArea area = null;

            if (string.Equals(manufacturer, "GenuineIntel", StringComparison.Ordinal))
            {
                if (model == 0x46 || model == 0x45 || model == 0x3C || model == 0x3D)
                    area = new HaswellClientL3();
                else if ((model & 0xF) == 0xE || model == 0xA5)
                    area = new SkylakeClientL3();
                else if (model == 0x97 || model == 0x9A || model == 0xB7 || model == 0xBA || model == 0xBF || model == 0xBE)
                    area = new AlderLakeL3();
                else if (model == 0xAA || model == 0xC6)
                    area = new MeteorLakeL3();

                if (area == null)
                    area = new ModernIntelCpu();

                MonitoringConfig config = FindMonitoringConfig(area, "L3 Hitrate", "Arch Counters");
                return CreateL3ConfigInfo(config);
            }

            if (!string.Equals(manufacturer, "AuthenticAMD", StringComparison.Ordinal))
                return null;

            if (family == 0x17)
            {
                if (model == 0x71 || model == 0x31 || model == 0x90 || model == 0x60)
                    area = new Zen2L3Cache();
                else if (model == 0x1 || model == 0x18 || model == 0x8)
                    area = new ZenL3Cache();
            }
            else if (family == 0x19)
            {
                if (model == 0x61)
                    area = new Zen4L3Cache();
                else
                    area = new Zen3L3Cache();
            }
            else if (family == 0x1A)
            {
                if (model == 0x44 || model == 0x60)
                    area = new Zen5L3Cache();
            }

            MonitoringConfig amdConfig = FindMonitoringConfig(area, "Hitrate and Latency", "Hitrate and Miss Latency");
            return CreateL3ConfigInfo(amdConfig);
        }

        private static MonitoringConfig TryCreateDramConfig(string manufacturer, byte family, byte model)
        {
            if (string.Equals(manufacturer, "GenuineIntel", StringComparison.Ordinal))
            {
                if (!IsSkylakeOrNewer(model))
                    return null;

                var arb = new SkylakeClientArb();
                if (arb.barAddress == 0)
                    return null;

                return new SkylakeClientArb.MemoryBandwidth(arb);
            }

            if (!string.Equals(manufacturer, "AuthenticAMD", StringComparison.Ordinal))
                return null;

            MonitoringArea area = null;
            if (family == 0x17)
            {
                if (model == 0x31)
                    area = new Zen2DataFabric(Zen2DataFabric.DfType.DestkopThreadripper);
                else if (model == 0x71 || model == 0x90 || model == 0x60)
                    area = new Zen2DataFabric(Zen2DataFabric.DfType.Client);
            }
            else if (family == 0x19)
            {
                if (model == 0x61)
                    area = new Zen4DataFabric(Zen4DataFabric.DfType.Client);
                else
                    area = new Zen2DataFabric(Zen2DataFabric.DfType.Client);
            }
            else if (family == 0x1A)
            {
                if (model == 0x44 || model == 0x60)
                    area = new Zen5DataFabric(Zen5DataFabric.DfType.Client);
            }

            if (area is Zen5DataFabric)
                return FindMonitoringConfig(area, "UMC");
            if (area is Zen4DataFabric)
                return FindMonitoringConfig(area, "DRAM Bandwidth??");
            if (area is Zen2DataFabric)
                return FindMonitoringConfig(area, "MTS/RNR DRAM Bandwidth??", "TR DRAM Bandwidth?");

            return null;
        }

        private static bool IsSkylakeOrNewer(byte model)
        {
            if ((model & 0xF) == 0xE)
                return true;

            return model == 0xA5
                || model == 0x97
                || model == 0x9A
                || model == 0xB7
                || model == 0xBA
                || model == 0xBF
                || model == 0xBE
                || model == 0xAA
                || model == 0xC6;
        }

        private static MonitoringConfig FindMonitoringConfig(MonitoringArea area, params string[] names)
        {
            if (area == null)
                return null;

            var configs = area.GetMonitoringConfigs();
            if (configs == null || names == null)
                return null;

            foreach (string name in names)
            {
                var config = configs.FirstOrDefault(entry => string.Equals(entry.GetConfigName(), name, StringComparison.Ordinal));
                if (config != null)
                    return config;
            }

            return null;
        }

        private (DateTime, Dictionary<ISensorEntry, float>) CaptureSnapshot()
        {
            var result = new Dictionary<ISensorEntry, float>();

            if (_l3Config != null)
            {
                try
                {
                    var update = _l3Config.Update();
                    if (TryGetL3Hitrate(update, out float hitRate))
                    {
                        result[_l3HitRateEntry] = hitRate;
                    }

                    if (TryGetDramLatency(update, out float dramLatency))
                    {
                        result[_dramLatencyEntry] = dramLatency;
                    }
                }
                catch
                {
                }
            }

            if (_dramConfig != null)
            {
                try
                {
                    var update = _dramConfig.Update();
                    if (update?.overallMetrics?.Length > 1
                        && TryParseBandwidth(update.overallMetrics[1], out float bytesPerSecond))
                    {
                        result[_dramBandwidthEntry] = bytesPerSecond / 1_073_741_824f;
                    }
                }
                catch
                {
                }
            }

            return (DateTime.UtcNow, result);
        }

        private bool TryGetL3Hitrate(MonitoringUpdateResults update, out float hitRate)
        {
            hitRate = 0;
            if (update?.overallMetrics == null || !_l3HitrateMetricIndex.HasValue)
                return false;

            int indexToRead = _l3HitrateMetricIndex.Value;
            if (update.overallMetrics.Length <= indexToRead)
                return false;

            return TryParsePercentage(update.overallMetrics[indexToRead], out hitRate);
        }

        private bool TryGetDramLatency(MonitoringUpdateResults update, out float latency)
        {
            latency = 0;
            if (update?.overallMetrics == null || !_dramLatencyMetricIndex.HasValue)
                return false;

            int indexToRead = _dramLatencyMetricIndex.Value;
            if (update.overallMetrics.Length <= indexToRead)
                return false;

            return TryParseLatency(update.overallMetrics[indexToRead], out latency);
        }

        private static bool TryParsePercentage(string input, out float value)
        {
            value = 0;
            if (string.IsNullOrWhiteSpace(input))
                return false;

            var trimmed = input.Trim().TrimEnd('%').Trim();
            if (!TryParseFloatWithCulture(trimmed, out float parsed))
                return false;

            value = parsed;
            return true;
        }

        private static bool TryParseBandwidth(string input, out float bytesPerSecond)
        {
            bytesPerSecond = 0;
            if (string.IsNullOrWhiteSpace(input))
                return false;

            string value = input.Trim();
            value = TrimSuffix(value, "B/s");
            value = TrimSuffix(value, "B");

            double multiplier = 1;
            char suffix = value.Length > 0 ? value[value.Length - 1] : '\0';
            if (char.IsLetter(suffix))
            {
                value = value.Substring(0, value.Length - 1).Trim();
                switch (char.ToUpperInvariant(suffix))
                {
                    case 'K':
                        multiplier = 1_000d;
                        break;
                    case 'M':
                        multiplier = 1_000_000d;
                        break;
                    case 'G':
                        multiplier = 1_000_000_000d;
                        break;
                    case 'T':
                        multiplier = 1_000_000_000_000d;
                        break;
                    default:
                        multiplier = 1;
                        break;
                }
            }

            if (!TryParseDoubleWithCulture(value, out double parsed))
                return false;

            bytesPerSecond = (float)(parsed * multiplier);
            return true;
        }

        private static bool TryParseLatency(string input, out float value)
        {
            value = 0;
            if (string.IsNullOrWhiteSpace(input))
                return false;

            string trimmed = input.Trim();
            trimmed = TrimSuffix(trimmed, "ns");
            trimmed = TrimSuffix(trimmed, "clk");
            trimmed = TrimSuffix(trimmed, "clks");

            return TryParseFloatWithCulture(trimmed, out value);
        }

        private static string TrimSuffix(string value, string suffix)
        {
            if (value.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                return value.Substring(0, value.Length - suffix.Length).Trim();

            return value;
        }

        private static bool TryParseFloatWithCulture(string input, out float value)
        {
            var styles = NumberStyles.Float | NumberStyles.AllowThousands;
            if (float.TryParse(input, styles, CultureInfo.CurrentCulture, out value))
                return true;

            return float.TryParse(input, styles, CultureInfo.InvariantCulture, out value);
        }

        private static bool TryParseDoubleWithCulture(string input, out double value)
        {
            var styles = NumberStyles.Float | NumberStyles.AllowThousands;
            if (double.TryParse(input, styles, CultureInfo.CurrentCulture, out value))
                return true;

            return double.TryParse(input, styles, CultureInfo.InvariantCulture, out value);
        }

        private static L3ConfigInfo CreateL3ConfigInfo(MonitoringConfig config)
        {
            if (config == null)
                return null;

            int? metricIndex = TryGetHitrateColumnIndex(config);
            if (!metricIndex.HasValue)
                return null;

            return new L3ConfigInfo(config, metricIndex.Value);
        }

        private static int? TryGetHitrateColumnIndex(MonitoringConfig config)
        {
            string[] columns = config.GetColumns();
            if (columns == null || columns.Length == 0)
                return null;

            string[] preferredNames = new[] { "L3 Hitrate", "LLC Hitrate", "Hitrate" };
            foreach (string preferredName in preferredNames)
            {
                for (int i = 0; i < columns.Length; i++)
                {
                    if (string.Equals(columns[i], preferredName, StringComparison.Ordinal))
                        return i;
                }
            }

            return null;
        }

        private static int? TryGetDramLatencyColumnIndex(MonitoringConfig config)
        {
            string[] columns = config.GetColumns();
            if (columns == null || columns.Length == 0)
                return null;

            string[] preferredNames = new[] { "Latency, DRAM", "Mem Latency?" };
            foreach (string preferredName in preferredNames)
            {
                for (int i = 0; i < columns.Length; i++)
                {
                    if (string.Equals(columns[i], preferredName, StringComparison.Ordinal))
                        return i;
                }
            }

            return null;
        }

        private sealed class L3ConfigInfo
        {
            public L3ConfigInfo(MonitoringConfig config, int hitrateMetricIndex)
            {
                Config = config;
                HitrateMetricIndex = hitrateMetricIndex;
            }

            public MonitoringConfig Config { get; }
            public int HitrateMetricIndex { get; }
        }
    }
}
