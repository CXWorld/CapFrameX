using CapFrameX.Contracts.Configuration;
using CapFrameX.Contracts.Overlay;
using CapFrameX.Contracts.Sensor;
using CapFrameX.Data.Session.Contracts;
using CapFrameX.Overlay;
using Microsoft.Extensions.Logging;
using OpenHardwareMonitor.Hardware;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;

namespace CapFrameX.Sensor
{

    public class SensorService : ISensorService
    {
        private readonly object _lockComputer = new object();

        private readonly IAppConfiguration _appConfiguration;
        private readonly ILogger<SensorService> _logger;
        private readonly object _dictLock = new object();
        private readonly Dictionary<string, IOverlayEntry> _overlayEntryDict
            = new Dictionary<string, IOverlayEntry>();

        private Computer _computer;
        private SessionSensorDataLive _sessionSensorDataLive;
        private bool _isLoggingActive = false;

        private ISubject<TimeSpan> _sensorUpdateSubject;
        private ISubject<TimeSpan> _osdUpdateSubject;
        private ISubject<TimeSpan> _loggingUpdateSubject;
        private TimeSpan _currentLoggingTimespan;
        private TimeSpan _currentOSDTimespan;
        private TimeSpan _currentSensorTimespan
        {
            get
            {
                if (_currentLoggingTimespan < _currentOSDTimespan)
                {
                    return _currentLoggingTimespan;
                }
                return _currentOSDTimespan;
            }
        }
        private IObservable<(DateTime, Dictionary<ISensor, float>)> _sensorSnapshotStream;
        private ISubject<IOverlayEntry[]> _onDictionaryUpdated = new Subject<IOverlayEntry[]>();
        public IObservable<IOverlayEntry[]> OnDictionaryUpdated => _onDictionaryUpdated;

        public bool UseSensorLogging => _appConfiguration.UseSensorLogging;

        public bool IsOverlayActive => _appConfiguration.IsOverlayActive;

        public SensorService(IAppConfiguration appConfiguration,
                             ILogger<SensorService> logger)
        {
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();

            _appConfiguration = appConfiguration;
            _logger = logger;
            _currentOSDTimespan = TimeSpan.FromMilliseconds(_appConfiguration.OSDRefreshPeriod);
            _currentLoggingTimespan = TimeSpan.FromMilliseconds(_appConfiguration.SensorLoggingRefreshPeriod);
            _loggingUpdateSubject = new BehaviorSubject<TimeSpan>(_currentLoggingTimespan);
            _osdUpdateSubject = new BehaviorSubject<TimeSpan>(_currentOSDTimespan);
            _sensorUpdateSubject = new BehaviorSubject<TimeSpan>(_currentSensorTimespan);
            _sensorSnapshotStream = _sensorUpdateSubject
                .Select(timespan => Observable.Concat(Observable.Return(-1L), Observable.Interval(timespan)))
                .Switch()
                .Where((_, idx) => idx == 0 || IsOverlayActive || (_isLoggingActive && UseSensorLogging))
                .Select(_ => GetSensorValues())
                .Replay(0).RefCount();

            _logger.LogDebug("{componentName} Ready", this.GetType().Name);

            Observable.FromAsync(() => StartOpenHardwareMonitor())
                .Delay(TimeSpan.FromMilliseconds(200))
                .SubscribeOn(Scheduler.Default)
                .Subscribe(t =>
                    {
                        InitializeOverlayEntryDict();

                        _sensorSnapshotStream
                            .Sample(_osdUpdateSubject.Select(timespan => Observable.Concat(Observable.Return(-1L), Observable.Interval(timespan))).Switch())
                            .Where((_, idx) => idx == 0 || IsOverlayActive)
                            .SubscribeOn(Scheduler.Default)
                            .Subscribe(sensorData =>
                            {
                                UpdateOSD(sensorData.Item2);
                                _onDictionaryUpdated.OnNext(GetSensorOverlayEntries());
                            });

                        _sensorSnapshotStream
                            .Sample(_loggingUpdateSubject.Select(timespan => Observable.Concat(Observable.Return(-1L), Observable.Interval(timespan))).Switch())
                            .Where(_ => _isLoggingActive && UseSensorLogging)
                            .SubscribeOn(Scheduler.Default)
                            .Subscribe(sensorData => LogCurrentValues(sensorData.Item2, sensorData.Item1));
                    });

            stopwatch.Stop();
            _logger.LogInformation(GetType().Name + " {initializationTime}s initialization time", Math.Round(stopwatch.ElapsedMilliseconds * 1E-03), 1);
        }

        public void SetLoggingInterval(TimeSpan timeSpan)
        {
            _currentLoggingTimespan = timeSpan;
            UpdateSensorInterval();
            _loggingUpdateSubject.OnNext(timeSpan);
        }

        public void SetOSDInterval(TimeSpan timeSpan)
        {
            _currentOSDTimespan = timeSpan;
            UpdateSensorInterval();
            _osdUpdateSubject.OnNext(timeSpan);
        }

        public string GetSensorTypeString(IOverlayEntry entry)
        {
            if (entry == null)
                return string.Empty;

            string SensorType;

            if (entry.Identifier.Contains("cpu"))
            {
                if (entry.Identifier.Contains("load"))
                    SensorType = "CPU Load";
                else if (entry.Identifier.Contains("clock"))
                    SensorType = "CPU Clock";
                else if (entry.Identifier.Contains("power"))
                    SensorType = "CPU Power";
                else if (entry.Identifier.Contains("temperature"))
                    SensorType = "CPU Temperature";
                else if (entry.Identifier.Contains("voltage"))
                    SensorType = "CPU Voltage";
                else
                    SensorType = string.Empty;
            }

            else if (entry.Identifier.Contains("gpu"))
            {
                if (entry.Identifier.Contains("load"))
                    SensorType = "GPU Load";
                else if (entry.Identifier.Contains("clock"))
                    SensorType = "GPU Clock";
                else if (entry.Identifier.Contains("power"))
                    SensorType = "GPU Power";
                else if (entry.Identifier.Contains("temperature"))
                    SensorType = "GPU Temperature";
                else if (entry.Identifier.Contains("voltage"))
                    SensorType = "GPU Voltage";
                else if (entry.Identifier.Contains("factor"))
                    SensorType = "GPU Limits";
                else
                    SensorType = string.Empty;
            }

            else
                SensorType = string.Empty;

            return SensorType;
        }

        private void UpdateSensorInterval()
        {
            _sensorUpdateSubject.OnNext(_currentSensorTimespan);
        }

        private Task StartOpenHardwareMonitor()
        {
            return Task.Run(() =>
            {
                try
                {
                    lock (_lockComputer)
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
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error when starting OpenHardwareMonitor");
                }
            });
        }

        private void InitializeOverlayEntryDict()
        {
            if (_computer == null) return;

            lock (_dictLock)
                _overlayEntryDict.Clear();

            try
            {
                var sensors = GetSensors();
                if (sensors != null)
                {
                    foreach (var sensor in sensors)
                    {
                        var dictEntry = CreateOverlayEntry(sensor);
                        var id = sensor.Identifier.ToString();
                        lock (_dictLock)
                        {
                            if (!_overlayEntryDict.ContainsKey(id))
                                _overlayEntryDict.Add(id, dictEntry);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while getting sensors.");
            }
        }

        private IOverlayEntry CreateOverlayEntry(ISensor sensor)
        {
            return new OverlayEntryWrapper(sensor.Identifier.ToString())
            {
                Description = GetDescription(sensor),
                OverlayEntryType = MapType(sensor.Hardware.HardwareType),
                GroupName = GetGroupName(sensor),
                ShowGraph = false,
                ShowGraphIsEnabled = false,
                ShowOnOverlayIsEnabled = true,
                ShowOnOverlay = GetIsDefaultOverlayItem(sensor),
                Value = 0,
                ValueUnitFormat = GetValueUnitString(sensor.SensorType),
                ValueAlignmentAndDigits = GetValueAlignmentAndDigitsString(sensor.SensorType)
            };
        }

        private string GetValueAlignmentAndDigitsString(SensorType sensorType)
        {
            string formatString = "{0}";
            switch (sensorType)
            {
                case SensorType.Voltage:
                    formatString = "{0,5:F2}";
                    break;
                case SensorType.Clock:
                    formatString = "{0,5:F0}";
                    break;
                case SensorType.Temperature:
                    formatString = "{0,5:F0}";
                    break;
                case SensorType.Load:
                    formatString = "{0,5:F0}";
                    break;
                case SensorType.Fan:
                    formatString = "{0,5:F0}";
                    break;
                case SensorType.Flow:
                    formatString = "{0,5:F0}";
                    break;
                case SensorType.Control:
                    formatString = "{0,5:F0}";
                    break;
                case SensorType.Level:
                    formatString = "{0,5:F0}";
                    break;
                case SensorType.Factor:
                    formatString = "{0,5:F0}";
                    break;
                case SensorType.Power:
                    formatString = "{0,5:F1}";
                    break;
                case SensorType.Data:
                    formatString = "{0,5:F2}";
                    break;
                case SensorType.SmallData:
                    formatString = "{0,5:F0}";
                    break;
                case SensorType.Throughput:
                    formatString = "{0,5:F0}";
                    break;
            }

            return formatString;
        }

        private string GetValueUnitString(SensorType sensorType)
        {
            string formatString = "{0}";
            switch (sensorType)
            {
                case SensorType.Voltage:
                    formatString = "V  ";
                    break;
                case SensorType.Clock:
                    formatString = "MHz";
                    break;
                case SensorType.Temperature:
                    formatString = $"{GetDegreeCelciusUnitByCulture()} ";
                    break;
                case SensorType.Load:
                    formatString = "%  ";
                    break;
                case SensorType.Fan:
                    formatString = "RPM";
                    break;
                case SensorType.Flow:
                    formatString = "L/h";
                    break;
                case SensorType.Control:
                    formatString = "%  ";
                    break;
                case SensorType.Level:
                    formatString = "%  ";
                    break;
                case SensorType.Factor:
                    formatString = "   ";
                    break;
                case SensorType.Power:
                    formatString = "W  ";
                    break;
                case SensorType.Data:
                    formatString = "GB ";
                    break;
                case SensorType.SmallData:
                    formatString = "MB ";
                    break;
                case SensorType.Throughput:
                    formatString = "MB/s";
                    break;
            }

            return formatString;
        }

        private string GetDegreeCelciusUnitByCulture()
        {
            try
            {
                if (CultureInfo.CurrentCulture.Name == new CultureInfo("en-DE").Name)
                    return "బC";
                else
                    return "°C";
            }
            catch
            {
                return "°C";
            }
        }

        private bool GetIsDefaultOverlayItem(ISensor sensor)
        {
            if (sensor.Name.Contains("Core"))
            {
                if ((sensor.SensorType == SensorType.Power &&
                    sensor.Name.Contains("CPU")) ||
                    (sensor.SensorType == SensorType.Temperature &&
                    sensor.Name.Contains("CPU")) ||
                    sensor.Name.Contains("VRM") ||
                    sensor.SensorType == SensorType.Voltage)
                    return false;

                return true;
            }
            else if (sensor.Name.Contains("Memory")
                && sensor.Hardware.HardwareType == HardwareType.RAM
                && sensor.SensorType == SensorType.Load)
            {
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
            else if (name.Contains("CPU Max Clock"))
            {
                name = name.Replace("CPU Max Clock", "CPU Max");
            }
            else if (name.Contains("GPU Core"))
            {
                name = name.Replace(" Core", "");
            }
            else if (name.Contains("Memory Controller"))
            {
                name = name.Replace("Memory Controller", "MemCtrl");
            }
            else if (name.Contains("Memory"))
            {
                name = name.Replace("Memory", "Mem");

                if (name.Contains("Dedicated"))
                    name = name.Replace("GPU Mem Dedicated", "GPU Mem");

                else if (name.Contains("Shared"))
                    name = name.Replace("GPU Mem Shared", "GPU Mem");
            }
            else if (name.Contains("Power Limit"))
            {
                name = name.Replace("Power Limit", "PL");
            }
            else if (name.Contains("Thermal Limit"))
            {
                name = name.Replace("Thermal Limit", "TL");
            }
            else if (name.Contains("Voltage Limit"))
            {
                name = name.Replace("Voltage Limit", "VL");
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

        private IOverlayEntry[] GetSensorOverlayEntries()
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
            return UseSensorLogging ? _sessionSensorDataLive
                .ToSessionSensorData() : null;
        }

        public void StartSensorLogging()
        {
            if (UseSensorLogging)
            {
                _isLoggingActive = true;
                _sessionSensorDataLive = new SessionSensorDataLive();
                _sensorUpdateSubject.OnNext(_currentSensorTimespan);
            }
        }

        public void StopSensorLogging()
        {
            Observable.Timer(_currentLoggingTimespan).Subscribe(_ =>
            {
                _isLoggingActive = false;
            });
        }

        private void UpdateOSD(Dictionary<ISensor, float> sensorData)
        {
            if (_computer == null) return;

            foreach (var sensorPair in sensorData)
            {
                var sensorIdentifier = sensorPair.Key.Identifier.ToString();
                var sensorValue = sensorPair.Value;
                if (_overlayEntryDict.TryGetValue(sensorIdentifier, out IOverlayEntry entry))
                {
                    lock (_dictLock)
                    {
                        entry.Value = sensorValue;
                    }
                }
            }
        }

        private void LogCurrentValues(Dictionary<ISensor, float> currentValues, DateTime timestamp)
        {
            _sessionSensorDataLive.AddMeasureTime(timestamp);
            foreach (var sensorPair in currentValues)
            {
                _sessionSensorDataLive.AddSensorValue(sensorPair.Key, sensorPair.Value);
            }
        }

        private (DateTime, Dictionary<ISensor, float>) GetSensorValues()
        {
            var dict = new ConcurrentDictionary<ISensor, float>();
            try
            {
                var sensors = GetSensors();
                if (sensors != null)
                {
                    foreach (var sensor in sensors)
                    {
                        if (sensor.Value != null)
                            dict.TryAdd(sensor, sensor.Value.Value);
                    }
                }
            }
            catch 
            {
                // Don't write periodic log entries
            }

            return (DateTime.UtcNow, dict.ToDictionary(x => x.Key, x => x.Value));
        }

        private IEnumerable<ISensor> GetSensors()
        {
            IEnumerable<ISensor> sensors = null;
            lock (_lockComputer)
            {
                sensors = _computer.Hardware.SelectMany(hardware =>
                   {
                       hardware.Update();
                       return hardware.Sensors.Concat(hardware.SubHardware.SelectMany(subHardware =>
                       {
                           subHardware.Update();
                           return subHardware.Sensors;
                       }));
                   });
            }

            return sensors;
        }

        public void CloseOpenHardwareMonitor()
        {
            lock (_lockComputer)
            {
                _computer?.Close();
            }
        }

        public string GetGpuDriverVersion()
        {
            IHardware gpu = null;
            lock (_lockComputer)
            {
                gpu = _computer.Hardware
               .FirstOrDefault(hdw => hdw.HardwareType == HardwareType.GpuAti
                   || hdw.HardwareType == HardwareType.GpuNvidia);
            }

            return gpu != null ? gpu.GetDriverVersion() : "Unknown";
        }

        public string GetCpuName()
        {
            IHardware cpu = null;
            lock (_lockComputer)
            {
                cpu = _computer.Hardware
                    .FirstOrDefault(hdw => hdw.HardwareType == HardwareType.CPU);
            }

            return cpu != null ? cpu.Name : "Unknown";
        }

        public string GetGpuName()
        {
            IHardware gpu = null;
            lock (_lockComputer)
            {
                gpu = _computer.Hardware
                   .FirstOrDefault(hdw => hdw.HardwareType == HardwareType.GpuAti
                       || hdw.HardwareType == HardwareType.GpuNvidia);
            }

            return gpu != null ? gpu.Name : "Unknown";
        }
    }
}
