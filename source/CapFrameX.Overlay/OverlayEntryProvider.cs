using CapFrameX.Contracts.Overlay;
using CapFrameX.Contracts.Sensor;
using CapFrameX.Extensions;
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
        private readonly Dictionary<string, IOverlayEntry> _identifierOverlayEntryDict;
        private List<IOverlayEntry> _overlayEntries;

        public OverlayEntryProvider(ISensorService sensorService)
        {
            _sensorService = sensorService;
            _identifierOverlayEntryDict = new Dictionary<string, IOverlayEntry>();
            EntryUpdateStream = new Subject<Unit>();

            try
            {
                LoadOverlayEntriesFromJson();
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
                var sensorOverlayEntries = _sensorService.GetSensorOverlayEntries();

                var adjustedOverlayEntries = new List<IOverlayEntry>(_overlayEntries);
                foreach (var entry in _overlayEntries)
                {
                    if (!sensorOverlayEntries.Contains(entry))
                        adjustedOverlayEntries.Remove(entry);
                }

                bool reorderFlag = false;
                foreach (var entry in sensorOverlayEntries)
                {
                    if (!adjustedOverlayEntries.Contains(entry))
                    {
                        reorderFlag = true;
                        adjustedOverlayEntries.Add(entry);
                    }
                }

                if (reorderFlag)
                    ReorderOverlayEntries();

                _overlayEntries = new List<IOverlayEntry>(adjustedOverlayEntries);
            }

            foreach (var entry in _overlayEntries)
            {
                entry.OverlayEntryProvider = this;
                _identifierOverlayEntryDict.Add(entry.Identifier, entry);
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
            _overlayEntries = new List<IOverlayEntry>
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
                        ShowOnOverlay = false,
                        ShowOnOverlayIsEnabled = false,
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
                        ShowOnOverlayIsEnabled = false,
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
                    }
            };

            // Sensor data
            var sensorOverlayEntries = _sensorService.GetSensorOverlayEntries();
            _overlayEntries.AddRange(sensorOverlayEntries);

            foreach (var entry in _overlayEntries)
            {
                _identifierOverlayEntryDict.Add(entry.Identifier, entry);
            }
        }

        private void UpdateSensorData()
        {
            _sensorService.UpdateSensors();

            foreach (var entry in _overlayEntries)
            {
                var sensorEntry = _sensorService.GetSensorOverlayEntry(entry.Identifier);
                entry.Value = sensorEntry.Value;
                entry.ValueFormat = sensorEntry.ValueFormat;
            }
        }
    }
}
