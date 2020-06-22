using CapFrameX.Contracts.Configuration;
using CapFrameX.Contracts.Data;
using CapFrameX.Contracts.Overlay;
using CapFrameX.Contracts.Sensor;
using CapFrameX.EventAggregation.Messages;
using CapFrameX.Extensions;
using CapFrameX.PresentMonInterface;
using CapFrameX.Statistics.NetStandard.Contracts;
using Newtonsoft.Json;
using Prism.Events;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;

namespace CapFrameX.Overlay
{
    public class OverlayEntryProvider : IOverlayEntryProvider
    {
        private static readonly string OVERLAY_CONFIG_FOLDER
            = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    @"CapFrameX\OverlayConfiguration\");

        private readonly ISensorService _sensorService;
        private readonly IAppConfiguration _appConfiguration;
        private readonly IEventAggregator _eventAggregator;
        private readonly IOnlineMetricService _onlineMetricService;
        private readonly ISystemInfo _systemInfo;

        private readonly ConcurrentDictionary<string, IOverlayEntry> _identifierOverlayEntryDict
             = new ConcurrentDictionary<string, IOverlayEntry>();
        private readonly TaskCompletionSource<bool> _taskCompletionSource
            = new TaskCompletionSource<bool>();
        private BlockingCollection<IOverlayEntry> _overlayEntries;
        private IObservable<IOverlayEntry[]> _onDictionaryUpdatedBuffered;

        public OverlayEntryProvider(ISensorService sensorService,
            IAppConfiguration appConfiguration,
            IEventAggregator eventAggregator,
            IOnlineMetricService onlineMetricService,
            ISystemInfo systemInfo)
        {
            _sensorService = sensorService;
            _appConfiguration = appConfiguration;
            _eventAggregator = eventAggregator;
            _onlineMetricService = onlineMetricService;
            _systemInfo = systemInfo;
            _onDictionaryUpdatedBuffered = _sensorService.OnDictionaryUpdated
                .Replay(1)
                .AutoConnect(0);
             

            _ = Task.Run(async () => await LoadOrSetDefault())
                .ContinueWith(task => _taskCompletionSource.SetResult(true));

            SubscribeToOptionPopupClosed();
        }

        public async Task<IOverlayEntry[]> GetOverlayEntries()
        {
            await _taskCompletionSource.Task;
            UpdateSensorData();
            UpdateOnlineMetrics();
            UpdateAdvancedFormatting();
            return _overlayEntries.ToArray();
        }

        public IOverlayEntry GetOverlayEntry(string identifier)
        {
            _identifierOverlayEntryDict.TryGetValue(identifier, out IOverlayEntry entry);
            return entry;
        }

        public void MoveEntry(int sourceIndex, int targetIndex)
        {
            _overlayEntries.Move(sourceIndex, targetIndex);
        }

        public async Task SaveOverlayEntriesToJson()
        {
            try
            {
                var persistence = new OverlayEntryPersistence()
                {
                    OverlayEntries = _overlayEntries.Select(entry => entry as OverlayEntryWrapper).ToList()
                };

                var json = JsonConvert.SerializeObject(persistence);

                if (!Directory.Exists(OVERLAY_CONFIG_FOLDER))
                    Directory.CreateDirectory(OVERLAY_CONFIG_FOLDER);

                using (StreamWriter outputFile = new StreamWriter(GetConfigurationFileName()))
                {
                    await outputFile.WriteAsync(json);
                }
            }
            catch { return; }
        }

        public async Task SwitchConfigurationTo(int index)
        {
            SetConfigurationFileName(index);
            await LoadOrSetDefault();
        }

        public async Task<IEnumerable<IOverlayEntry>> GetDefaultOverlayEntries()
        {
            _overlayEntries = await SetOverlayEntryDefaults();
            _identifierOverlayEntryDict.Clear();
            foreach (var entry in _overlayEntries)
            {
                _identifierOverlayEntryDict.TryAdd(entry.Identifier, entry);
            }

            return _overlayEntries.ToList();
        }

        private async Task LoadOrSetDefault()
        {
            try
            {
                _overlayEntries = await InitializeOverlayEntryDictionary();
            }
            catch
            {
                _overlayEntries = await SetOverlayEntryDefaults();
            }
            _identifierOverlayEntryDict.Clear();
            foreach (var entry in _overlayEntries)
            {
                _identifierOverlayEntryDict.TryAdd(entry.Identifier, entry);
            }
            CheckCustomSystemInfo();
            CheckOSVersion();
            CheckGpuDriver();
        }

        private IObservable<BlockingCollection<IOverlayEntry>> InitializeOverlayEntryDictionary()
        {
            string json = File.ReadAllText(GetConfigurationFileName());
            var overlayEntriesFromJson = JsonConvert.DeserializeObject<OverlayEntryPersistence>(json)
                .OverlayEntries.ToBlockingCollection<IOverlayEntry>();

            return _onDictionaryUpdatedBuffered
                .Take(1)
                .Select(sensorOverlayEntries =>
                {
                    var sensorOverlayEntryIdentfiers = sensorOverlayEntries
                        .Select(entry => entry.Identifier)
                        .ToList();

                    var adjustedOverlayEntries = new List<IOverlayEntry>(overlayEntriesFromJson);
                    var adjustedOverlayEntryIdentfiers = adjustedOverlayEntries
                        .Select(entry => entry.Identifier)
                        .ToList();

                    foreach (var entry in overlayEntriesFromJson.Where(x =>
                     (x.OverlayEntryType == EOverlayEntryType.GPU
                     || x.OverlayEntryType == EOverlayEntryType.CPU
                     || x.OverlayEntryType == EOverlayEntryType.RAM)))
                    {
                        if (!sensorOverlayEntryIdentfiers.Contains(entry.Identifier))
                            adjustedOverlayEntries.Remove(entry);
                    }

                    foreach (var entry in sensorOverlayEntries)
                    {
                        if (!adjustedOverlayEntryIdentfiers.Contains(entry.Identifier))
                        {
                            adjustedOverlayEntries.Add(entry);
                        }
                    }

                    return adjustedOverlayEntries.ToBlockingCollection();
                });
        }

        private void CheckOSVersion()
        {
            _identifierOverlayEntryDict.TryGetValue("OS", out IOverlayEntry entry);

            if (entry != null)
            {
                entry.Value = _systemInfo.GetOSVersion();
            }
        }

        private void CheckGpuDriver()
        {
            _identifierOverlayEntryDict.TryGetValue("GPUDriver", out IOverlayEntry entry);

            if (entry != null)
            {
                entry.Value = _sensorService.GetGpuDriverVersion();
            }
        }

        private void CheckCustomSystemInfo()
        {
            _identifierOverlayEntryDict.TryGetValue("CustomCPU", out IOverlayEntry customCPUEntry);

            if (customCPUEntry != null)
            {
                customCPUEntry.Value =
                    _appConfiguration.HardwareInfoSource == "Auto" ? _systemInfo.GetProcessorName()
                    : _appConfiguration.CustomCpuDescription;
            }

            _identifierOverlayEntryDict.TryGetValue("CustomGPU", out IOverlayEntry customGPUEntry);

            if (customGPUEntry != null)
            {
                customGPUEntry.Value =
                    _appConfiguration.HardwareInfoSource == "Auto" ? _systemInfo.GetGraphicCardName()
                    : _appConfiguration.CustomGpuDescription;
            }

            _identifierOverlayEntryDict.TryGetValue("Mainboard", out IOverlayEntry mainboardEntry);

            if (mainboardEntry != null)
            {
                mainboardEntry.Value = _systemInfo.GetMotherboardName();
            }

            _identifierOverlayEntryDict.TryGetValue("CustomRAM", out IOverlayEntry customRAMEntry); ;

            if (customRAMEntry != null)
            {
                customRAMEntry.Value =
                    _appConfiguration.HardwareInfoSource == "Auto" ? _systemInfo.GetSystemRAMInfoName()
                    : _appConfiguration.CustomRamDescription;
            }
        }

        private IObservable<BlockingCollection<IOverlayEntry>> SetOverlayEntryDefaults()
        {
            var overlayEntries = OverlayUtils.GetOverlayEntryDefaults()
                    .Select(item => item as IOverlayEntry).ToBlockingCollection();

            // Sensor data
            return _onDictionaryUpdatedBuffered
                .Take(1)
                .Select(sensorOverlayEntries =>
                {
                    sensorOverlayEntries.ForEach(sensor => overlayEntries.TryAdd(sensor));
                    return overlayEntries;
                });
        }

        private void UpdateSensorData()
        {
            foreach (var entry in _overlayEntries.Where(x => 
                (x.OverlayEntryType == EOverlayEntryType.GPU 
                 || x.OverlayEntryType == EOverlayEntryType.CPU
                 || x.OverlayEntryType == EOverlayEntryType.RAM)))
            {
                var sensorEntry = _sensorService.GetSensorOverlayEntry(entry.Identifier);
                entry.Value = sensorEntry?.Value;
            }
        }

        private void UpdateOnlineMetrics()
        {
            // average
            _identifierOverlayEntryDict.TryGetValue("OnlineAverage", out IOverlayEntry averageEntry);

            if (averageEntry != null && averageEntry.ShowOnOverlay)
            {
                averageEntry.Value = Math.Round(_onlineMetricService.GetOnlineFpsMetricValue(EMetric.Average));
                averageEntry.ValueFormat = "{0,4:F0}<S=50>FPS<S>";
            }

            // P1
            _identifierOverlayEntryDict.TryGetValue("OnlineP1", out IOverlayEntry p1Entry);

            if (p1Entry != null && p1Entry.ShowOnOverlay)
            {
                p1Entry.Value = Math.Round(_onlineMetricService.GetOnlineFpsMetricValue(EMetric.P1));
                p1Entry.ValueFormat = "{0,4:F0}<S=50>FPS<S>";
            }

            // P0.2
            _identifierOverlayEntryDict.TryGetValue("OnlineP0dot2", out IOverlayEntry p1dot2Entry);

            if (p1dot2Entry != null && p1dot2Entry.ShowOnOverlay)
            {
                p1dot2Entry.Value = Math.Round(_onlineMetricService.GetOnlineFpsMetricValue(EMetric.P0dot2));
                p1dot2Entry.ValueFormat = "{0,4:F0}<S=50>FPS<S>";
            }
        }

        private void UpdateAdvancedFormatting()
        {
            foreach (var entry in _overlayEntries.Where(x => x.FormatChanged))
            {
                entry.GroupNameFormat 
                    = entry.GroupSeparators == 0 ? "{0}" 
                    : Enumerable.Repeat("\n", entry.GroupSeparators).Aggregate((i, j) => i + j) + "{0}";
                entry.FormatChanged = false;
            }
        }

        private string GetConfigurationFileName()
        {
            return Path.Combine(OVERLAY_CONFIG_FOLDER, $"OverlayEntryConfiguration_" +
                $"{_appConfiguration.OverlayEntryConfigurationFile}.json");
        }

        private void SetConfigurationFileName(int index)
        {
            _appConfiguration.OverlayEntryConfigurationFile = index;
        }

        private void SubscribeToOptionPopupClosed()
        {
            _eventAggregator.GetEvent<PubSubEvent<ViewMessages.OptionPopupClosed>>()
                            .Subscribe(_ =>
                            {
                                CheckCustomSystemInfo();
                            });
        }
    }
}
