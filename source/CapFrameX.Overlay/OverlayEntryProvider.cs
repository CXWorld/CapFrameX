using CapFrameX.Contracts.Configuration;
using CapFrameX.Contracts.Overlay;
using CapFrameX.Contracts.Sensor;
using CapFrameX.Extensions;
using CapFrameX.PresentMonInterface;
using Newtonsoft.Json;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Subjects;

namespace CapFrameX.Overlay
{
    public class OverlayEntryProvider : IOverlayEntryProvider
    {
        private readonly ISensorService _sensorService;
        private readonly IAppConfiguration _appConfiguration;
        private readonly ConcurrentDictionary<string, IOverlayEntry> _identifierOverlayEntryDict
             = new ConcurrentDictionary<string, IOverlayEntry>();
        private BlockingCollection<IOverlayEntry> _overlayEntries;

        public ISubject<Unit> EntryUpdateStream { get; }
            = new Subject<Unit>();

        public OverlayEntryProvider(ISensorService sensorService, 
            IAppConfiguration appConfiguration)
        {
            _sensorService = sensorService;
            _appConfiguration = appConfiguration;

            LoadOrSetDefault();
        }

        public IOverlayEntry[] GetOverlayEntries()
        {
            UpdateSensorData();
            return _overlayEntries.ToArray();
        }

        public IOverlayEntry GetOverlayEntry(string identifier)
        {
            return _identifierOverlayEntryDict[identifier];
        }

        public void MoveEntry(int sourceIndex, int targetIndex)
        {
            _overlayEntries.Move(sourceIndex, targetIndex);
        }

        public bool SaveOverlayEntriesToJson()
        {
            try
            {
                var persistence = new OverlayEntryPersistence()
                {
                    OverlayEntries = _overlayEntries.Select(entry => entry as OverlayEntryWrapper).ToList()
                };

                var json = JsonConvert.SerializeObject(persistence);
                File.WriteAllText(GetConfigurationFileName(), json);

                return true;
            }
            catch { return false; }
        }

        public void SwitchConfigurationTo(int index)
        {
            SetConfigurationFileName(index);
            LoadOrSetDefault();
        }

        private void LoadOrSetDefault()
        {
            try
            {
                LoadOverlayEntriesFromJson();
                CheckCustomSystemInfo();
                ChecOSVersion();
            }
            catch
            {
                SetOverlayEntryDefaults();
            }
        }

        private void LoadOverlayEntriesFromJson()
        {
            string json = File.ReadAllText(GetConfigurationFileName());
            _overlayEntries = JsonConvert.DeserializeObject<OverlayEntryPersistence>(json)
                .OverlayEntries.ToBlockingCollection<IOverlayEntry>();

            if (_sensorService.CheckHardwareChanged(_overlayEntries.ToList()))
            {
                var sensorOverlayEntries = _sensorService
                    .GetSensorOverlayEntries();
                var sensorOverlayEntryIdentfiers = _sensorService
                    .GetSensorOverlayEntries().Select(entry => entry.Identifier)
                    .ToList();

                var adjustedOverlayEntries = new List<IOverlayEntry>(_overlayEntries);
                var adjustedOverlayEntryIdentfiers = adjustedOverlayEntries
                    .Select(entry => entry.Identifier)
                    .ToList();

                foreach (var entry in _overlayEntries.Where(x => !(x.OverlayEntryType == EOverlayEntryType.CX)))
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

                _overlayEntries = adjustedOverlayEntries.ToBlockingCollection();
            }

            _identifierOverlayEntryDict.Clear();
            foreach (var entry in _overlayEntries)
            {
                entry.OverlayEntryProvider = this;
                _identifierOverlayEntryDict.TryAdd(entry.Identifier, entry);
            }
        }

        private void ChecOSVersion()
        {
            _identifierOverlayEntryDict.TryGetValue("OS", out IOverlayEntry entry);

            if (entry != null)
            {
                entry.Value = SystemInfo.GetOSVersion();
            }
        }

        private void CheckCustomSystemInfo()
        {
            _identifierOverlayEntryDict.TryGetValue("CustomCPU", out IOverlayEntry customCPUEntry);

            if (customCPUEntry != null)
            {
                customCPUEntry.Value =
                    _appConfiguration.CustomCpuDescription == "CPU" ? SystemInfo.GetProcessorName()
                    : _appConfiguration.CustomCpuDescription;
            }

            _identifierOverlayEntryDict.TryGetValue("CustomGPU", out IOverlayEntry customGPUEntry);

            if (customGPUEntry != null)
            {
                customGPUEntry.Value =
                    _appConfiguration.CustomGpuDescription == "GPU" ? SystemInfo.GetGraphicCardName()
                    : _appConfiguration.CustomGpuDescription;
            }

            _identifierOverlayEntryDict.TryGetValue("Mainboard", out IOverlayEntry mainboardEntry);

            if (mainboardEntry != null)
            {
                mainboardEntry.Value = SystemInfo.GetMotherboardName();
            }

            _identifierOverlayEntryDict.TryGetValue("CustomRAM", out IOverlayEntry customRAMEntry); ;

            if (customRAMEntry != null)
            {
                customRAMEntry.Value =
                    _appConfiguration.CustomRamDescription == "RAM" ? SystemInfo.GetSystemRAMInfoName()
                    : _appConfiguration.CustomRamDescription;
            }
        }

        private void SetOverlayEntryDefaults()
        {
            _identifierOverlayEntryDict.Clear();
            _overlayEntries = OverlayUtils.GetOverlayEntryDefaults()
                    .Select(item => item as IOverlayEntry).ToBlockingCollection();

            // Sensor data
            var sensorOverlayEntries = _sensorService.GetSensorOverlayEntries();
            sensorOverlayEntries.ForEach(sensor => _overlayEntries.Add(sensor));

            foreach (var entry in _overlayEntries)
            {
                entry.OverlayEntryProvider = this;
                _identifierOverlayEntryDict.TryAdd(entry.Identifier, entry);
            }
        }

        private void UpdateSensorData()
        {
            _sensorService.UpdateSensors();

            foreach (var entry in _overlayEntries
                .Where(x => !(x.OverlayEntryType == EOverlayEntryType.CX)))
            {
                var sensorEntry = _sensorService.GetSensorOverlayEntry(entry.Identifier);
                entry.Value = sensorEntry.Value;
            }
        }

        private string GetConfigurationFileName()
        {
            return $"OverlayConfiguration//OverlayEntryConfiguration_" +
                $"{_appConfiguration.OverlayEntryConfigurationFile}.json";
        }

        private void SetConfigurationFileName(int index)
        {
            _appConfiguration.OverlayEntryConfigurationFile = index;
        }
    }
}
