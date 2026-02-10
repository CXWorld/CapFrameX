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

        // Gaming config identifiers - P-Cores
        private const string GamingPCoreIpcIdentifier = "pmcreader/cpu/gaming-pcore-ipc";
        private const string GamingPCoreL3HitrateIdentifier = "pmcreader/cpu/gaming-pcore-l3-hitrate";
        private const string GamingPCoreL3BoundIdentifier = "pmcreader/cpu/gaming-pcore-l3-bound";
        private const string GamingPCoreMemBoundIdentifier = "pmcreader/cpu/gaming-pcore-mem-bound";
        private const string GamingPCoreOffcoreBwIdentifier = "pmcreader/cpu/gaming-pcore-offcore-bw";

        // Gaming config identifiers - E-Cores
        private const string GamingECoreIpcIdentifier = "pmcreader/cpu/gaming-ecore-ipc";
        private const string GamingECoreL3HitrateIdentifier = "pmcreader/cpu/gaming-ecore-l3-hitrate";
        private const string GamingECoreL3BoundIdentifier = "pmcreader/cpu/gaming-ecore-l3-bound";
        private const string GamingECoreMemBoundIdentifier = "pmcreader/cpu/gaming-ecore-mem-bound";
        private const string GamingECoreL3MissBwIdentifier = "pmcreader/cpu/gaming-ecore-l3miss-bw";

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

        // Gaming config sensor entries - P-Cores
        private readonly ISensorEntry _gamingPCoreIpcEntry = new PmcReaderSensorEntry
        {
            Identifier = GamingPCoreIpcIdentifier,
            SortKey = "6_10",
            Name = "CPU P-Core IPC",
            SensorType = SensorType.Factor.ToString(),
            HardwareType = HardwareType.Cpu.ToString(),
            IsPresentationDefault = false
        };

        private readonly ISensorEntry _gamingPCoreL3HitrateEntry = new PmcReaderSensorEntry
        {
            Identifier = GamingPCoreL3HitrateIdentifier,
            SortKey = "6_11",
            Name = "CPU P-Core L3 Hitrate",
            SensorType = SensorType.Load.ToString(),
            HardwareType = HardwareType.Cpu.ToString(),
            IsPresentationDefault = false
        };

        private readonly ISensorEntry _gamingPCoreL3BoundEntry = new PmcReaderSensorEntry
        {
            Identifier = GamingPCoreL3BoundIdentifier,
            SortKey = "6_12",
            Name = "CPU P-Core L3 Bound",
            SensorType = SensorType.Load.ToString(),
            HardwareType = HardwareType.Cpu.ToString(),
            IsPresentationDefault = false
        };

        private readonly ISensorEntry _gamingPCoreMemBoundEntry = new PmcReaderSensorEntry
        {
            Identifier = GamingPCoreMemBoundIdentifier,
            SortKey = "6_13",
            Name = "CPU P-Core Mem Bound",
            SensorType = SensorType.Load.ToString(),
            HardwareType = HardwareType.Cpu.ToString(),
            IsPresentationDefault = false
        };

        private readonly ISensorEntry _gamingPCoreOffcoreBwEntry = new PmcReaderSensorEntry
        {
            Identifier = GamingPCoreOffcoreBwIdentifier,
            SortKey = "6_14",
            Name = "CPU P-Core Offcore BW",
            SensorType = SensorType.Throughput.ToString(),
            HardwareType = HardwareType.Cpu.ToString(),
            IsPresentationDefault = false
        };

        // Gaming config sensor entries - E-Cores
        private readonly ISensorEntry _gamingECoreIpcEntry = new PmcReaderSensorEntry
        {
            Identifier = GamingECoreIpcIdentifier,
            SortKey = "6_20",
            Name = "CPU E-Core IPC",
            SensorType = SensorType.Factor.ToString(),
            HardwareType = HardwareType.Cpu.ToString(),
            IsPresentationDefault = false
        };

        private readonly ISensorEntry _gamingECoreL3HitrateEntry = new PmcReaderSensorEntry
        {
            Identifier = GamingECoreL3HitrateIdentifier,
            SortKey = "6_21",
            Name = "CPU E-Core L3 Hitrate",
            SensorType = SensorType.Load.ToString(),
            HardwareType = HardwareType.Cpu.ToString(),
            IsPresentationDefault = false
        };

        private readonly ISensorEntry _gamingECoreL3BoundEntry = new PmcReaderSensorEntry
        {
            Identifier = GamingECoreL3BoundIdentifier,
            SortKey = "6_22",
            Name = "CPU E-Core L3 Bound",
            SensorType = SensorType.Load.ToString(),
            HardwareType = HardwareType.Cpu.ToString(),
            IsPresentationDefault = false
        };

        private readonly ISensorEntry _gamingECoreMemBoundEntry = new PmcReaderSensorEntry
        {
            Identifier = GamingECoreMemBoundIdentifier,
            SortKey = "6_23",
            Name = "CPU E-Core Mem Bound",
            SensorType = SensorType.Load.ToString(),
            HardwareType = HardwareType.Cpu.ToString(),
            IsPresentationDefault = false
        };

        private readonly ISensorEntry _gamingECoreL3MissBwEntry = new PmcReaderSensorEntry
        {
            Identifier = GamingECoreL3MissBwIdentifier,
            SortKey = "6_24",
            Name = "CPU E-Core L3 Miss BW",
            SensorType = SensorType.Throughput.ToString(),
            HardwareType = HardwareType.Cpu.ToString(),
            IsPresentationDefault = false
        };

        private readonly BehaviorSubject<TimeSpan> _updateIntervalSubject
            = new BehaviorSubject<TimeSpan>(TimeSpan.FromSeconds(1));

        private MonitoringConfig _l3Config;
        private int? _l3HitrateMetricIndex;
        private int? _dramLatencyMetricIndex;
        private MonitoringConfig _dramConfig;
        private List<ISensorEntry> _ccxL3HitRateEntries;
        private List<ISensorEntry> _ccxDramLatencyEntries;
        private bool _disposed;

        // Gaming config (Arrow Lake, Alder Lake, Raptor Lake)
        private MonitoringConfig _gamingConfig;
        private GamingConfigIndices _gamingIndices;

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
            if (_ccxL3HitRateEntries != null)
                entries.AddRange(_ccxL3HitRateEntries);
            if (_ccxDramLatencyEntries != null)
                entries.AddRange(_ccxDramLatencyEntries);

            // Gaming config entries - P-Cores and E-Cores
            if (_gamingConfig != null && _gamingIndices != null)
            {
                // P-Core entries
                entries.Add(_gamingPCoreIpcEntry);
                entries.Add(_gamingPCoreL3HitrateEntry);
                entries.Add(_gamingPCoreL3BoundEntry);
                entries.Add(_gamingPCoreMemBoundEntry);
                entries.Add(_gamingPCoreOffcoreBwEntry);

                // E-Core entries
                entries.Add(_gamingECoreIpcEntry);
                entries.Add(_gamingECoreL3HitrateEntry);
                entries.Add(_gamingECoreL3BoundEntry);
                entries.Add(_gamingECoreMemBoundEntry);
                entries.Add(_gamingECoreL3MissBwEntry);
            }

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
                TryInitializeCcxEntries();
            }

            _dramConfig = TryCreateDramConfig(manufacturer, family, model);
            if (_dramConfig != null)
                _dramConfig.Initialize();

            // Gaming config (Arrow Lake, Alder Lake, Raptor Lake)
            if (string.Equals(manufacturer, "GenuineIntel", StringComparison.Ordinal) && IsGamingConfigSupported(model))
            {
                var gamingConfigInfo = TryCreateGamingConfig(model);
                if (gamingConfigInfo != null)
                {
                    _gamingConfig = gamingConfigInfo.Config;
                    _gamingIndices = gamingConfigInfo.Indices;
                    _gamingConfig.Initialize();
                }
            }
        }

        private static L3ConfigInfo TryCreateL3Config(string manufacturer, byte family, byte model)
        {
            MonitoringArea area = null;

            if (string.Equals(manufacturer, "GenuineIntel", StringComparison.Ordinal))
            {
                // Arrow Lake, Alder Lake, Raptor Lake use Gaming config instead
                if (IsGamingConfigSupported(model))
                    return null;

                if (model == 0x46 || model == 0x45 || model == 0x3C || model == 0x3D)
                    area = new HaswellClientL3();
                else if ((model & 0xF) == 0xE || model == 0xA5)
                    area = new SkylakeClientL3();
                else if (model == 0xAA)
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
                // Arrow Lake, Alder Lake, Raptor Lake use Gaming config instead
                if (IsGamingConfigSupported(model))
                    return null;

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
                || model == 0xAA;
            // Note: ADL/RPL/ARL excluded - use Gaming config via IsGamingConfigSupported
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

                    if (update?.unitMetrics != null && update.unitMetrics.Length > 0)
                    {
                        EnsureCcxEntries(update.unitMetrics.Length);
                        AddPerCcxMetrics(update.unitMetrics, result);
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

            // Gaming config - P-Cores and E-Cores
            if (_gamingConfig != null && _gamingIndices != null)
            {
                try
                {
                    var update = _gamingConfig.Update();
                    if (update?.unitMetrics != null)
                    {
                        var idx = _gamingIndices;

                        // Find P-Cores Overall and E-Cores Overall rows
                        string[] pCoreMetrics = null;
                        string[] eCoreMetrics = null;

                        foreach (var row in update.unitMetrics)
                        {
                            if (row != null && row.Length > 0)
                            {
                                if (row[0] == ">> P-Cores Overall")
                                    pCoreMetrics = row;
                                else if (row[0] == ">> E-Cores Overall")
                                    eCoreMetrics = row;
                            }
                        }

                        // Parse P-Core metrics
                        if (pCoreMetrics != null)
                        {
                            if (idx.IpcIndex < pCoreMetrics.Length && TryParseFloat(pCoreMetrics[idx.IpcIndex], out float ipc))
                                result[_gamingPCoreIpcEntry] = ipc;

                            if (idx.L3HitrateIndex < pCoreMetrics.Length && TryParsePercentage(pCoreMetrics[idx.L3HitrateIndex], out float l3Hitrate))
                                result[_gamingPCoreL3HitrateEntry] = l3Hitrate;

                            if (idx.L3BoundIndex < pCoreMetrics.Length && TryParsePercentage(pCoreMetrics[idx.L3BoundIndex], out float l3Bound))
                                result[_gamingPCoreL3BoundEntry] = l3Bound;

                            if (idx.MemBoundIndex < pCoreMetrics.Length && TryParsePercentage(pCoreMetrics[idx.MemBoundIndex], out float memBound))
                                result[_gamingPCoreMemBoundEntry] = memBound;

                            if (idx.OffcoreBwIndex < pCoreMetrics.Length && TryParseBandwidth(pCoreMetrics[idx.OffcoreBwIndex], out float offcoreBw))
                                result[_gamingPCoreOffcoreBwEntry] = offcoreBw / 1_073_741_824f;
                        }

                        // Parse E-Core metrics
                        if (eCoreMetrics != null)
                        {
                            if (idx.IpcIndex < eCoreMetrics.Length && TryParseFloat(eCoreMetrics[idx.IpcIndex], out float ipc))
                                result[_gamingECoreIpcEntry] = ipc;

                            if (idx.L3HitrateIndex < eCoreMetrics.Length && TryParsePercentage(eCoreMetrics[idx.L3HitrateIndex], out float l3Hitrate))
                                result[_gamingECoreL3HitrateEntry] = l3Hitrate;

                            if (idx.L3BoundIndex < eCoreMetrics.Length && TryParsePercentage(eCoreMetrics[idx.L3BoundIndex], out float l3Bound))
                                result[_gamingECoreL3BoundEntry] = l3Bound;

                            if (idx.MemBoundIndex < eCoreMetrics.Length && TryParsePercentage(eCoreMetrics[idx.MemBoundIndex], out float memBound))
                                result[_gamingECoreMemBoundEntry] = memBound;

                            if (idx.L3MissBwIndex < eCoreMetrics.Length && TryParseBandwidth(eCoreMetrics[idx.L3MissBwIndex], out float l3MissBw))
                                result[_gamingECoreL3MissBwEntry] = l3MissBw / 1_073_741_824f;
                        }
                    }
                }
                catch
                {
                }
            }

            return (DateTime.UtcNow, result);
        }

        private void TryInitializeCcxEntries()
        {
            if (_l3Config == null || !_l3HitrateMetricIndex.HasValue)
                return;

            try
            {
                var update = _l3Config.Update();
                if (update?.unitMetrics != null && update.unitMetrics.Length > 0)
                    EnsureCcxEntries(update.unitMetrics.Length);
            }
            catch
            {
            }
        }

        private void EnsureCcxEntries(int ccxCount)
        {
            if (ccxCount <= 0)
                return;

            bool hasL3Entries = _ccxL3HitRateEntries != null && _ccxL3HitRateEntries.Count == ccxCount;
            bool hasLatencyEntries = !_dramLatencyMetricIndex.HasValue
                || (_ccxDramLatencyEntries != null && _ccxDramLatencyEntries.Count == ccxCount);

            if (hasL3Entries && hasLatencyEntries)
                return;

            var l3Entries = new List<ISensorEntry>(ccxCount);
            var latencyEntries = _dramLatencyMetricIndex.HasValue ? new List<ISensorEntry>(ccxCount) : null;

            for (int i = 0; i < ccxCount; i++)
            {
                l3Entries.Add(new PmcReaderSensorEntry
                {
                    Identifier = $"{L3Identifier}/ccx{i}",
                    SortKey = $"6_0_1_{i}",
                    Name = $"CPU L3 Hit Rate CCX {i}",
                    SensorType = SensorType.Load.ToString(),
                    HardwareType = HardwareType.Cpu.ToString(),
                    IsPresentationDefault = false
                });

                if (latencyEntries != null)
                {
                    latencyEntries.Add(new PmcReaderSensorEntry
                    {
                        Identifier = $"{DramLatencyIdentifier}/ccx{i}",
                        SortKey = $"6_2_1_{i}",
                        Name = $"CPU DRAM Latency CCX {i}",
                        SensorType = SensorType.Timing.ToString(),
                        HardwareType = HardwareType.Cpu.ToString(),
                        IsPresentationDefault = false
                    });
                }
            }

            _ccxL3HitRateEntries = l3Entries;
            _ccxDramLatencyEntries = latencyEntries;
        }

        private void AddPerCcxMetrics(string[][] unitMetrics, Dictionary<ISensorEntry, float> result)
        {
            if (_ccxL3HitRateEntries == null || !_l3HitrateMetricIndex.HasValue)
                return;

            int hitrateIndex = _l3HitrateMetricIndex.Value;
            int latencyIndex = _dramLatencyMetricIndex ?? -1;

            for (int i = 0; i < unitMetrics.Length && i < _ccxL3HitRateEntries.Count; i++)
            {
                string[] ccxMetrics = unitMetrics[i];
                if (ccxMetrics == null || ccxMetrics.Length <= hitrateIndex)
                    continue;

                if (TryParsePercentage(ccxMetrics[hitrateIndex], out float hitRate))
                    result[_ccxL3HitRateEntries[i]] = hitRate;

                if (_ccxDramLatencyEntries != null
                    && latencyIndex >= 0
                    && i < _ccxDramLatencyEntries.Count
                    && ccxMetrics.Length > latencyIndex
                    && TryParseLatency(ccxMetrics[latencyIndex], out float latency))
                {
                    result[_ccxDramLatencyEntries[i]] = latency;
                }
            }
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

        private static bool IsGamingConfigSupported(byte model)
        {
            return model == 0xC6                                  // Arrow Lake
                || model == 0x97 || model == 0x9A                 // Alder Lake (S, P)
                || model == 0xB7 || model == 0xBA                 // Raptor Lake, RPL-H
                || model == 0xBF || model == 0xBE;                // RPL-HX, RPL-U
        }

        private static GamingConfigInfo TryCreateGamingConfig(byte model)
        {
            MonitoringArea area;
            if (model == 0xC6)
                area = new ArrowLake();
            else
                area = new AlderLake();

            var config = FindMonitoringConfig(area, "All Cores: Gaming Performance");
            if (config == null)
                return null;

            var indices = TryGetGamingConfigIndices(config);
            if (indices == null)
                return null;

            return new GamingConfigInfo(config, indices);
        }

        private static GamingConfigIndices TryGetGamingConfigIndices(MonitoringConfig config)
        {
            string[] columns = config.GetColumns();
            if (columns == null || columns.Length == 0)
                return null;

            int? ipcIndex = FindColumnIndex(columns, "IPC");
            int? l3HitrateIndex = FindColumnIndex(columns, "L3 Hitrate");
            int? l3BoundIndex = FindColumnIndex(columns, "L3 Bound %");
            int? memBoundIndex = FindColumnIndex(columns, "Mem Bound %");
            int? offcoreBwIndex = FindColumnIndex(columns, "Offcore BW");
            int? l3MissBwIndex = FindColumnIndex(columns, "L3 Miss BW");

            if (!ipcIndex.HasValue || !l3HitrateIndex.HasValue || !l3BoundIndex.HasValue
                || !memBoundIndex.HasValue || !offcoreBwIndex.HasValue || !l3MissBwIndex.HasValue)
                return null;

            return new GamingConfigIndices(
                ipcIndex.Value,
                l3HitrateIndex.Value,
                l3BoundIndex.Value,
                memBoundIndex.Value,
                offcoreBwIndex.Value,
                l3MissBwIndex.Value);
        }

        private static int? FindColumnIndex(string[] columns, string name)
        {
            for (int i = 0; i < columns.Length; i++)
            {
                if (string.Equals(columns[i], name, StringComparison.Ordinal))
                    return i;
            }
            return null;
        }

        private static bool TryParseFloat(string input, out float value)
        {
            value = 0;
            if (string.IsNullOrWhiteSpace(input))
                return false;

            return TryParseFloatWithCulture(input.Trim(), out value);
        }

        private sealed class GamingConfigInfo
        {
            public GamingConfigInfo(MonitoringConfig config, GamingConfigIndices indices)
            {
                Config = config;
                Indices = indices;
            }

            public MonitoringConfig Config { get; }
            public GamingConfigIndices Indices { get; }
        }

        private sealed class GamingConfigIndices
        {
            public GamingConfigIndices(int ipcIndex, int l3HitrateIndex, int l3BoundIndex,
                int memBoundIndex, int offcoreBwIndex, int l3MissBwIndex)
            {
                IpcIndex = ipcIndex;
                L3HitrateIndex = l3HitrateIndex;
                L3BoundIndex = l3BoundIndex;
                MemBoundIndex = memBoundIndex;
                OffcoreBwIndex = offcoreBwIndex;
                L3MissBwIndex = l3MissBwIndex;
            }

            public int IpcIndex { get; }
            public int L3HitrateIndex { get; }
            public int L3BoundIndex { get; }
            public int MemBoundIndex { get; }
            public int OffcoreBwIndex { get; }
            public int L3MissBwIndex { get; }
        }
    }
}
