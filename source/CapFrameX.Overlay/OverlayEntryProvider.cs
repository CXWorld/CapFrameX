using CapFrameX.Contracts.Configuration;
using CapFrameX.Contracts.Overlay;
using CapFrameX.Contracts.Sensor;
using CapFrameX.Extensions;
using CapFrameX.PresentMonInterface;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Subjects;

namespace CapFrameX.Overlay
{
    public class OverlayEntryProvider : IOverlayEntryProvider
    {
        private const string JSON_FILE_NAME
            = @"OverlayConfiguration\OverlayEntryConfiguration.json";

        private readonly ISensorService _sensorService;
        private readonly IAppConfiguration _appConfiguration;
        private readonly Dictionary<string, IOverlayEntry> _identifierOverlayEntryDict;
        private List<IOverlayEntry> _overlayEntries;

        public OverlayEntryProvider(ISensorService sensorService, IAppConfiguration appConfiguration)
        {
            _sensorService = sensorService;
            _appConfiguration = appConfiguration;
            _identifierOverlayEntryDict = new Dictionary<string, IOverlayEntry>();
            EntryUpdateStream = new Subject<Unit>();

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

        public ISubject<Unit> EntryUpdateStream { get; }

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
                File.WriteAllText(JSON_FILE_NAME, json);

                return true;
            }
            catch { return false; }
        }

        private void LoadOverlayEntriesFromJson()
        {
            string json = File.ReadAllText(JSON_FILE_NAME);
            _overlayEntries = new List<IOverlayEntry>(JsonConvert.
                DeserializeObject<OverlayEntryPersistence>(json).OverlayEntries);

            if (_sensorService.CheckHardwareChanged(_overlayEntries))
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

                //bool reorderFlag = false;
                foreach (var entry in sensorOverlayEntries)
                {
                    if (!adjustedOverlayEntryIdentfiers.Contains(entry.Identifier))
                    {
                        //reorderFlag = true;
                        adjustedOverlayEntries.Add(entry);
                    }
                }

                //if (reorderFlag)
                //  ReorderOverlayEntries();

                _overlayEntries = new List<IOverlayEntry>(adjustedOverlayEntries);
            }

            foreach (var entry in _overlayEntries)
            {
                entry.OverlayEntryProvider = this;
                _identifierOverlayEntryDict.Add(entry.Identifier, entry);
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

        private void ReorderOverlayEntries()
        {
            var reorderedOverlayEntries = new List<IOverlayEntry>();
            reorderedOverlayEntries.AddRange(_overlayEntries.Where(entry => entry.OverlayEntryType == EOverlayEntryType.CX));
            reorderedOverlayEntries.AddRange(_overlayEntries.Where(entry => entry.OverlayEntryType == EOverlayEntryType.GPU));
            reorderedOverlayEntries.AddRange(_overlayEntries.Where(entry => entry.OverlayEntryType == EOverlayEntryType.CPU));
            reorderedOverlayEntries.AddRange(_overlayEntries.Where(entry => entry.OverlayEntryType == EOverlayEntryType.RAM));
            reorderedOverlayEntries.AddRange(_overlayEntries.Where(entry => entry.OverlayEntryType == EOverlayEntryType.Mainboard));
            reorderedOverlayEntries.AddRange(_overlayEntries.Where(entry => entry.OverlayEntryType == EOverlayEntryType.FanController));
            reorderedOverlayEntries.AddRange(_overlayEntries.Where(entry => entry.OverlayEntryType == EOverlayEntryType.HDD));

            _overlayEntries = new List<IOverlayEntry>(reorderedOverlayEntries);
        }

        private void SetOverlayEntryDefaults()
        {
            _overlayEntries = GetOverlayEntryDefaults().Select(item => item as IOverlayEntry).ToList();

            // Sensor data
            var sensorOverlayEntries = _sensorService.GetSensorOverlayEntries();
            _overlayEntries.AddRange(sensorOverlayEntries);

            foreach (var entry in _overlayEntries)
            {
                entry.OverlayEntryProvider = this;
                _identifierOverlayEntryDict.Add(entry.Identifier, entry);
            }
        }

        private void UpdateSensorData()
        {
            _sensorService.UpdateSensors();

            foreach (var entry in _overlayEntries.Where(x => !(x.OverlayEntryType == EOverlayEntryType.CX)))
            {
                var sensorEntry = _sensorService.GetSensorOverlayEntry(entry.Identifier);
                entry.Value = sensorEntry.Value;
            }
        }

        public static List<OverlayEntryWrapper> GetOverlayEntryDefaults()
        {
            return new List<OverlayEntryWrapper>
                {
					// CX 
					// CaptureServiceStatus
					new OverlayEntryWrapper("CaptureServiceStatus")
                    {
                        OverlayEntryType = EOverlayEntryType.CX,
                        ShowOnOverlay = true,
                        ShowOnOverlayIsEnabled = true,
                        Description = "Capture service status",
                        GroupName = "Status:",
                        Value = "Capture service ready...",
                        ValueFormat = default,
                        ShowGraph = false,
                        ShowGraphIsEnabled = false,
                        Color = string.Empty
                    },

					// CaptureTimer
					new OverlayEntryWrapper("CaptureTimer")
                    {
                        OverlayEntryType = EOverlayEntryType.CX,
                        ShowOnOverlay = true,
                        ShowOnOverlayIsEnabled = true,
                        Description = "Capture timer",
                        GroupName = "Status:",
                        Value = "0",
                        ValueFormat = default,
                        ShowGraph = false,
                        ShowGraphIsEnabled = false,
                        Color = string.Empty
                    },

					// RunHistory
					new OverlayEntryWrapper("RunHistory")
                    {
                        OverlayEntryType = EOverlayEntryType.CX,
                        ShowOnOverlay = false,
                        ShowOnOverlayIsEnabled = true,
                        Description = "Run history",
                        GroupName = string.Empty,
                        Value = default,
                        ValueFormat = default,
                        ShowGraph = false,
                        ShowGraphIsEnabled = false,
                        Color = string.Empty
                    },

					// RTSS
					// Framerate
					new OverlayEntryWrapper("Framerate")
                    {
                        OverlayEntryType = EOverlayEntryType.CX,
                        ShowOnOverlay = true,
                        ShowOnOverlayIsEnabled = true,
                        Description = "Framerate",
                        GroupName = "<APP>",
                        Value = 0d,
                        ValueFormat = default,
                        ShowGraph = false,
                        ShowGraphIsEnabled = true,
                        Color = string.Empty
                    },

					// Frametime
					new OverlayEntryWrapper("Frametime")
                    {
                        OverlayEntryType = EOverlayEntryType.CX,
                        ShowOnOverlay = true,
                        ShowOnOverlayIsEnabled = true,
                        Description = "Frametime",
                        GroupName = "<APP>",
                        Value = 0d,
                        ValueFormat = default,
                        ShowGraph = false,
                        ShowGraphIsEnabled = true,
                        Color = string.Empty
                    },

                    // Custom CPU
					new OverlayEntryWrapper("CustomCPU")
                    {
                        OverlayEntryType = EOverlayEntryType.CX,
                        ShowOnOverlay = false,
                        ShowOnOverlayIsEnabled = true,
                        Description = "Custom CPU Name",
                        GroupName = "System Info",
                        Value = "CPU",
                        ValueFormat = default,
                        ShowGraph = false,
                        ShowGraphIsEnabled = false,
                        Color = string.Empty
                    },

                    // Custom GPU
					new OverlayEntryWrapper("CustomGPU")
                    {
                        OverlayEntryType = EOverlayEntryType.CX,
                        ShowOnOverlay = false,
                        ShowOnOverlayIsEnabled = true,
                        Description = "Custom GPU Name",
                        GroupName = "System Info",
                        Value = "GPU",
                        ValueFormat = default,
                        ShowGraph = false,
                        ShowGraphIsEnabled = false,
                        Color = string.Empty
                    },

                    // Custom Mainboard
					new OverlayEntryWrapper("Mainboard")
                    {
                        OverlayEntryType = EOverlayEntryType.CX,
                        ShowOnOverlay = false,
                        ShowOnOverlayIsEnabled = true,
                        Description = "Mainboard Name",
                        GroupName = "System Info",
                        Value = "Mainboard",
                        ValueFormat = default,
                        ShowGraph = false,
                        ShowGraphIsEnabled = false,
                        Color = string.Empty
                    },

                    // Custom RAM
					new OverlayEntryWrapper("CustomRAM")
                    {
                        OverlayEntryType = EOverlayEntryType.CX,
                        ShowOnOverlay = false,
                        ShowOnOverlayIsEnabled = true,
                        Description = "Custom RAM Description",
                        GroupName = "System Info",
                        Value = "RAM",
                        ValueFormat = default,
                        ShowGraph = false,
                        ShowGraphIsEnabled = false,
                        Color = string.Empty
                    },

                    // OS
					new OverlayEntryWrapper("OS")
                    {
                        OverlayEntryType = EOverlayEntryType.CX,
                        ShowOnOverlay = false,
                        ShowOnOverlayIsEnabled = true,
                        Description = "OS Version",
                        GroupName = "OS",
                        Value = "OS",
                        ValueFormat = default,
                        ShowGraph = false,
                        ShowGraphIsEnabled = false,
                        Color = string.Empty
                    }
            };
        }
    }
}
