using CapFrameX.Contracts.Configuration;
using CapFrameX.Contracts.Overlay;
using CapFrameX.Contracts.Sensor;
using CapFrameX.Data.Session.Contracts;
using CapFrameX.Overlay;
using Microsoft.Extensions.Logging;
using OpenHardwareMonitor.Hardware;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace CapFrameX.Sensor
{
    public class SensorService : ISensorService
    {
        private readonly IAppConfiguration _appConfiguration;
        private readonly ILogger<SensorService> _logger;
        private readonly object _dictLock = new object();
        private readonly Dictionary<string, IOverlayEntry> _overlayEntryDict
            = new Dictionary<string, IOverlayEntry>();

        private Computer _computer;
        private SessionSensorDataLive _sessionSensorDataLive;
        private bool _isLoggingActive = false;

        public bool UseSensorLogging => _appConfiguration.UseSensorLogging;

        public SensorService(IAppConfiguration appConfiguration,
                             ILogger<SensorService> logger)
        {
            _appConfiguration = appConfiguration;
            _logger = logger;

            _logger.LogDebug("{componentName} Ready", this.GetType().Name);

            StartOpenHardwareMonitor();
            InitializeOverlayEntryDict();

            // Test
            _appConfiguration.UseSensorLogging = true;
        }

        private void StartOpenHardwareMonitor()
        {
            try
            {
                _computer = new Computer();
                _computer.Open();

                _computer.GPUEnabled = true;
                _computer.CPUEnabled = true;
                _computer.RAMEnabled = true;
                _computer.MainboardEnabled = false;
                _computer.FanControllerEnabled = false;
                _computer.HDDEnabled = false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error when starting OpenHardwareMonitor");
            }
        }

        private void InitializeOverlayEntryDict()
        {
            if (_computer == null) return;

            foreach (var hardware in _computer.Hardware)
            {
                hardware.Update();
                foreach (var subHardware in hardware.SubHardware)
                {
                    subHardware.Update();
                    foreach (var sensor in subHardware.Sensors)
                    {
                        var currentEntry = CreateOverlayEntry(sensor, hardware.HardwareType);
                        lock (_dictLock)
                        {
                            if (!_overlayEntryDict.ContainsKey(currentEntry.Identifier))
                                _overlayEntryDict.Add(currentEntry.Identifier, currentEntry);
                        }
                    }
                }

                foreach (var sensor in hardware.Sensors)
                {
                    var currentEntry = CreateOverlayEntry(sensor, hardware.HardwareType);
                    lock (_dictLock)
                    {
                        if (!_overlayEntryDict.ContainsKey(currentEntry.Identifier))
                            _overlayEntryDict.Add(currentEntry.Identifier, currentEntry);
                    }
                }
            }
        }

        private IOverlayEntry CreateOverlayEntry(ISensor sensor, HardwareType hardwareType)
        {
            return new OverlayEntryWrapper(sensor.Identifier.ToString())
            {
                Description = GetDescription(sensor),
                OverlayEntryType = MapType(hardwareType),
                GroupName = GetGroupName(sensor),
                ShowGraph = false,
                ShowGraphIsEnabled = true,
                ShowOnOverlayIsEnabled = true,
                ShowOnOverlay = GetOverlayToggle(sensor),
                Value = 0,
                ValueFormat = GetFormatString(sensor.SensorType),
            };
        }

        private string GetDegreeCelciusUnitByCulture()
        {
            if (CultureInfo.CurrentCulture.Name == new CultureInfo("en-DE").Name)
                return "బC";
            else
                return "°C";
        }

        private bool GetOverlayToggle(ISensor sensor)
        {
            if (sensor.Name.Contains("Core"))
            {
                if (sensor.SensorType == SensorType.Power &&
                    sensor.Name.Contains("CPU"))
                    return false;

                return true;
            }
            else
                return false;
        }

        private string GetGroupName(ISensor sensor)
        {
            var name = sensor.Name;
            if (name.Contains("CPU Core #"))
            {
                name = name.Replace("Core #", "");
            }
            else if (name.Contains("GPU Core"))
            {
                name = name.Replace(" Core", "");

            }
            else if (name.Contains("Memory"))
            {
                name = name.Replace("Memory", "Mem");
            }

            if (name.Contains(" - Thread #1"))
            {
                name = name.Replace(" - Thread #1", "");

            }

            if (name.Contains(" - Thread #2"))
            {
                name = name.Replace(" - Thread #2", "");

            }

            return name;
        }

        private string GetDescription(ISensor sensor)
        {
            string description = string.Empty;
            switch (sensor.SensorType)
            {
                case SensorType.Voltage:
                    description = $"{sensor.Name} (V)";
                    break;
                case SensorType.Clock:
                    description = $"{sensor.Name} (MHz)";
                    break;
                case SensorType.Temperature:
                    description = $"{sensor.Name} (°C)";
                    break;
                case SensorType.Load:
                    description = $"{sensor.Name} (%)";
                    break;
                case SensorType.Fan:
                    description = $"{sensor.Name} (RPM)";
                    break;
                case SensorType.Flow:
                    description = $"{sensor.Name} (L/h)";
                    break;
                case SensorType.Control:
                    description = $"{sensor.Name} (%)";
                    break;
                case SensorType.Level:
                    description = $"{sensor.Name} (%)";
                    break;
                case SensorType.Factor:
                    description = sensor.Name;
                    break;
                case SensorType.Power:
                    description = $"{sensor.Name} (W)";
                    break;
                case SensorType.Data:
                    description = $"{sensor.Name} (GB)";
                    break;
                case SensorType.SmallData:
                    description = $"{sensor.Name} (MB)";
                    break;
                case SensorType.Throughput:
                    description = $"{sensor.Name} (MB/s)";
                    break;
            }

            return description;
        }

        private EOverlayEntryType MapType(HardwareType hardwareType)
        {
            EOverlayEntryType type = EOverlayEntryType.Undefined;
            switch (hardwareType)
            {
                case HardwareType.Mainboard:
                    type = EOverlayEntryType.Mainboard;
                    break;
                case HardwareType.SuperIO:
                    type = EOverlayEntryType.Undefined;
                    break;
                case HardwareType.CPU:
                    type = EOverlayEntryType.CPU;
                    break;
                case HardwareType.RAM:
                    type = EOverlayEntryType.RAM;
                    break;
                case HardwareType.GpuNvidia:
                    type = EOverlayEntryType.GPU;
                    break;
                case HardwareType.GpuAti:
                    type = EOverlayEntryType.GPU;
                    break;
                case HardwareType.TBalancer:
                    type = EOverlayEntryType.Undefined;
                    break;
                case HardwareType.Heatmaster:
                    type = EOverlayEntryType.Undefined;
                    break;
                case HardwareType.HDD:
                    type = EOverlayEntryType.HDD;
                    break;
            }

            return type;
        }

        private string GetFormatString(SensorType sensorType)
        {
            string formatString = "{0}";
            switch (sensorType)
            {
                case SensorType.Voltage:
                    formatString = "{0,4:F2}<S=50>V  <S>";
                    break;
                case SensorType.Clock:
                    formatString = "{0,4:F0}<S=50>MHz<S>";
                    break;
                case SensorType.Temperature:
                    formatString = "{0,4:F0}<S=50>" + GetDegreeCelciusUnitByCulture() + " <S>";
                    break;
                case SensorType.Load:
                    formatString = "{0,4:F0}<S=50>%  <S>";
                    break;
                case SensorType.Fan:
                    formatString = "{0,4:F0}<S=50>RPM<S>";
                    break;
                case SensorType.Flow:
                    formatString = "{0,4:F0}<S=50>L/h<S>";
                    break;
                case SensorType.Control:
                    formatString = "{0,4:F0}<S=50>%  <S>";
                    break;
                case SensorType.Level:
                    formatString = "{0,4:F0}<S=50>%  <S>";
                    break;
                case SensorType.Factor:
                    formatString = "{0,4:F0}<S=50>   <S>";
                    break;
                case SensorType.Power:
                    formatString = "{0,4:F1}<S=50>W  <S>";
                    break;
                case SensorType.Data:
                    formatString = "{0,4:F2}<S=50>GB <S>";
                    break;
                case SensorType.SmallData:
                    formatString = "{0,4:F0}<S=50>MB <S>";
                    break;
                case SensorType.Throughput:
                    formatString = "{0,4:F0}<S=50>MB/s<S>";
                    break;
            }

            return formatString;
        }

        public bool CheckHardwareChanged(List<IOverlayEntry> overlayEntries)
        {
            var overlayEntryIdentfiers = overlayEntries
                    .Select(entry => entry.Identifier)
                    .ToList();
            var overlayEntryLiveIdentfiers = GetSensorOverlayEntries()
                    .Select(entry => entry.Identifier)
                    .ToList(); ;

            return !(overlayEntryIdentfiers.All(overlayEntryLiveIdentfiers.Contains)
                 && overlayEntryIdentfiers.Count == overlayEntryLiveIdentfiers.Count);
        }

        public IOverlayEntry[] GetSensorOverlayEntries()
        {
            lock (_dictLock)
                return _overlayEntryDict.Values.ToArray();
        }

        public IOverlayEntry GetSensorOverlayEntry(string identifier)
        {
            lock (_dictLock)
            {
                _overlayEntryDict.TryGetValue(identifier, out IOverlayEntry entry);
                return entry;
            }
        }

        public ISessionSensorData GetSessionSensorData()
        {
            return _sessionSensorDataLive.ToSessionSensorData();
        }

        public void StartSensorLogging()
        {
            if (UseSensorLogging)
            {
                _sessionSensorDataLive = new SessionSensorDataLive();
                _isLoggingActive = true;
            }
        }

        public void StopSensorLogging()
        {
            _isLoggingActive = false;
        }

        public void UpdateSensors()
        {
            if (_computer == null) return;

            if (UseSensorLogging && _isLoggingActive)
                _sessionSensorDataLive.AddMeasureTime();

                foreach (var hardware in _computer.Hardware)
            {
                hardware.Update();
                foreach (var subHardware in hardware.SubHardware)
                {
                    subHardware.Update();
                    foreach (var sensor in subHardware.Sensors)
                    {
                        var currentIdentifier = sensor.Identifier.ToString();
                        lock (_dictLock)
                        {
                            _overlayEntryDict.TryGetValue(currentIdentifier, out IOverlayEntry entry);

                            if (entry != null)
                            {
                                entry.Value = sensor.Value;

                                if (UseSensorLogging && _isLoggingActive)
                                    _sessionSensorDataLive.AddSensorValue(sensor);
                            }
                        }
                    }
                }

                foreach (var sensor in hardware.Sensors)
                {
                    var currentIdentifier = sensor.Identifier.ToString();

                    lock (_dictLock)
                    {
                        _overlayEntryDict.TryGetValue(currentIdentifier, out IOverlayEntry entry);

                        if (entry != null)
                        {
                            entry.Value = sensor.Value;

                            if (UseSensorLogging && _isLoggingActive)
                                _sessionSensorDataLive.AddSensorValue(sensor);
                        }
                    }
                }
            }
        }

        public void CloseOpenHardwareMonitor()
        {
            _computer?.Close();
        }
    }
}
