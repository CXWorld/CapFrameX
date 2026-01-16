using CapFrameX.Contracts.Sensor;
using PmcReader;
using PmcReader.Intel;
using PmcReader.Interop;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;

namespace CapFrameX.PmcReader.Plugin
{
    public class PmcReaderSensorPlugin : IPmcReaderSensorPlugin
    {
        private const string L3Identifier = "pmcreader/cpu/l3-hitrate";
        private const string DramIdentifier = "pmcreader/cpu/dram-bandwidth";

        private readonly ISensorEntry _l3HitRateEntry = new PmcReaderSensorEntry
        {
            Identifier = L3Identifier,
            SortKey = "0_1_1_900",
            Name = "CPU L3 Hit Rate",
            SensorType = "Load",
            HardwareType = "Cpu",
            IsPresentationDefault = false
        };

        private readonly ISensorEntry _dramBandwidthEntry = new PmcReaderSensorEntry
        {
            Identifier = DramIdentifier,
            SortKey = "0_1_1_901",
            Name = "CPU DRAM Bandwidth",
            SensorType = "Throughput",
            HardwareType = "Cpu",
            IsPresentationDefault = false
        };

        private readonly BehaviorSubject<TimeSpan> _updateIntervalSubject
            = new BehaviorSubject<TimeSpan>(TimeSpan.FromSeconds(1));

        private MonitoringConfig _l3Config;
        private MonitoringConfig _dramConfig;

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
            if (_dramConfig != null)
                entries.Add(_dramBandwidthEntry);

            return Task.FromResult<IEnumerable<ISensorEntry>>(entries);
        }

        private void TryInitializeMonitoring()
        {
            try
            {
                Ring0.Open();
                OpCode.Open();
            }
            catch
            {
                return;
            }

            string manufacturer = OpCode.GetManufacturerId();
            if (!string.Equals(manufacturer, "GenuineIntel", StringComparison.Ordinal))
                return;

            OpCode.GetProcessorVersion(out byte family, out byte model, out _);
            if (family != 0x6)
                return;

            _l3Config = TryCreateL3Config(model);
            if (_l3Config != null)
                _l3Config.Initialize();

            _dramConfig = TryCreateDramConfig(model);
            if (_dramConfig != null)
                _dramConfig.Initialize();
        }

        private static MonitoringConfig TryCreateL3Config(byte model)
        {
            MonitoringArea area = null;

            if (model == 0x46 || model == 0x45 || model == 0x3C || model == 0x3D)
                area = new HaswellClientL3();
            else if ((model & 0xF) == 0xE || model == 0xA5)
                area = new SkylakeClientL3();
            else if (model == 0x97 || model == 0x9A || model == 0xB7 || model == 0xBA)
                area = new AlderLakeL3();
            else if (model == 0xAA || model == 0xC6)
                area = new MeteorLakeL3();

            return area?.GetMonitoringConfigs()
                ?.FirstOrDefault(config => string.Equals(config.GetConfigName(), "L3 Hitrate", StringComparison.Ordinal));
        }

        private static MonitoringConfig TryCreateDramConfig(byte model)
        {
            if (!IsSkylakeOrNewer(model))
                return null;

            var arb = new SkylakeClientArb();
            if (arb.barAddress == 0)
                return null;

            return new SkylakeClientArb.MemoryBandwidth(arb);
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
                || model == 0xAA
                || model == 0xC6;
        }

        private (DateTime, Dictionary<ISensorEntry, float>) CaptureSnapshot()
        {
            var result = new Dictionary<ISensorEntry, float>();

            if (_l3Config != null)
            {
                try
                {
                    var update = _l3Config.Update();
                    if (update?.overallMetrics?.Length > 1
                        && TryParsePercentage(update.overallMetrics[1], out float hitRate))
                    {
                        result[_l3HitRateEntry] = hitRate;
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
                        result[_dramBandwidthEntry] = bytesPerSecond;
                    }
                }
                catch
                {
                }
            }

            return (DateTime.UtcNow, result);
        }

        private static bool TryParsePercentage(string input, out float value)
        {
            value = 0;
            if (string.IsNullOrWhiteSpace(input))
                return false;

            var trimmed = input.Trim().TrimEnd('%').Trim();
            if (!float.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed))
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
            if (value.EndsWith("B/s", StringComparison.OrdinalIgnoreCase))
                value = value.Substring(0, value.Length - 3).Trim();

            if (value.EndsWith("B", StringComparison.OrdinalIgnoreCase))
                value = value.Substring(0, value.Length - 1).Trim();

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

            if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed))
                return false;

            bytesPerSecond = (float)(parsed * multiplier);
            return true;
        }
    }
}
