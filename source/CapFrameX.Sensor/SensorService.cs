using CapFrameX.Contracts.Configuration;
using CapFrameX.Contracts.Sensor;
using CapFrameX.Data.Session.Contracts;
using Microsoft.Extensions.Logging;
using OpenHardwareMonitor.Hardware;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;

namespace CapFrameX.Sensor
{
    public class SensorService : ISensorService
    {
        private readonly object _lockComputer = new object();
        private readonly Computer _computer;
        private readonly IAppConfiguration _appConfiguration;
        private readonly ILogger<SensorService> _logger;

        private SessionSensorDataLive _sessionSensorDataLive;
        private bool _isLoggingActive = false;

        private ISubject<TimeSpan> _sensorUpdateSubject;
        private ISubject<TimeSpan> _osdUpdateSubject;
        private ISubject<TimeSpan> _loggingUpdateSubject;
        private TimeSpan _currentLoggingTimespan;
        private TimeSpan _currentOSDTimespan;

        private TimeSpan CurrentSensorTimespan
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

        public IObservable<(DateTime, Dictionary<ISensorEntry, float>)> SensorSnapshotStream { get; }

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
            _sensorUpdateSubject = new BehaviorSubject<TimeSpan>(CurrentSensorTimespan);
            SensorSnapshotStream = _sensorUpdateSubject
                .Select(timespan => Observable.Concat(Observable.Return(-1L), Observable.Interval(timespan)))
                .Switch()
                .Where((_, idx) => idx == 0 || IsOverlayActive || (_isLoggingActive && UseSensorLogging))
                .Select(_ => GetSensorValues())
                .Replay(0)
                .RefCount();

            _logger.LogDebug("{componentName} Ready", this.GetType().Name);

            Observable.FromAsync(() => StartOpenHardwareMonitor())
                .Delay(TimeSpan.FromMilliseconds(200))
                .SubscribeOn(Scheduler.Default)
                .Subscribe(t =>
                {
                    SensorSnapshotStream
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

        public string GetSensorTypeString(string identifier)
        {
            if (identifier == null)
                return string.Empty;

            string SensorType;

            if (identifier.Contains("cpu"))
            {
                if (identifier.Contains("load"))
                    SensorType = "CPU Load";
                else if (identifier.Contains("clock"))
                    SensorType = "CPU Clock";
                else if (identifier.Contains("power"))
                    SensorType = "CPU Power";
                else if (identifier.Contains("temperature"))
                    SensorType = "CPU Temperature";
                else if (identifier.Contains("voltage"))
                    SensorType = "CPU Voltage";
                else
                    SensorType = string.Empty;
            }

            else if (identifier.Contains("gpu"))
            {
                if (identifier.Contains("load"))
                    SensorType = "GPU Load";
                else if (identifier.Contains("clock"))
                    SensorType = "GPU Clock";
                else if (identifier.Contains("power"))
                    SensorType = "GPU Power";
                else if (identifier.Contains("temperature"))
                    SensorType = "GPU Temperature";
                else if (identifier.Contains("voltage"))
                    SensorType = "GPU Voltage";
                else if (identifier.Contains("factor"))
                    SensorType = "GPU Limits";
                else
                    SensorType = string.Empty;
            }

            else
                SensorType = string.Empty;

            return SensorType;
        }

        private Task StartOpenHardwareMonitor()
        {
            return Task.Run(() =>
            {
                try
                {
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
            });
        }

        private void UpdateSensorInterval()
        {
            _sensorUpdateSubject.OnNext(CurrentSensorTimespan);
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
                _sessionSensorDataLive = new SessionSensorDataLive();
                // Logging must be activated after creating a session data object
                // because of time stamp consistency
                _isLoggingActive = true;
                _sensorUpdateSubject.OnNext(CurrentSensorTimespan);
            }
        }

        public void StopSensorLogging()
        {
            Observable.Timer(_currentLoggingTimespan).Subscribe(_ =>
            {
                _isLoggingActive = false;
            });
        }

        public IEnumerable<ISensorEntry> GetSensorEntries()
        {
            var entries = new List<ISensorEntry>();

            try
            {
                var sensors = GetSensors();
                if (sensors != null)
                {
                    foreach (var sensor in sensors)
                    {
                        if (sensor.Value != null)
                        {

                            entries.Add(new SensorEntry()
                            {
                                Identifier = sensor.Identifier.ToString(),
                                Value = sensor.Value,
                                SensorType = sensor.SensorType.ToString(),
                                HardwareType = sensor.Hardware.HardwareType.ToString()
                            });
                        }
                    }
                }
            }
            catch
            {
                // Don't write periodic log entries
            }

            return entries;
        }

        private void LogCurrentValues(Dictionary<ISensorEntry, float> currentValues, DateTime timestamp)
        {
            _sessionSensorDataLive.AddMeasureTime(timestamp);
            foreach (var sensorPair in currentValues)
            {
                _sessionSensorDataLive.AddSensorValue(sensorPair.Key, sensorPair.Value);
            }
        }

        private (DateTime, Dictionary<ISensorEntry, float>) GetSensorValues()
        {
            var dict = new ConcurrentDictionary<ISensorEntry, float>();
            try
            {
                var sensors = GetSensors();
                if (sensors != null)
                {
                    foreach (var sensor in sensors)
                    {
                        if (sensor.Value != null)
                            dict.TryAdd(new SensorEntry()
                            {
                                Identifier = sensor.Identifier.ToString(),
                                Value = sensor.Value,
                                SensorType = sensor.SensorType.ToString(),
                                HardwareType = sensor.Hardware.HardwareType.ToString()
                            },
                            sensor.Value.Value);
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
                gpu = _computer?.Hardware
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
                cpu = _computer?.Hardware
                    .FirstOrDefault(hdw => hdw.HardwareType == HardwareType.CPU);
            }

            return cpu != null ? cpu.Name : "Unknown";
        }

        public string GetGpuName()
        {
            IHardware gpu = null;
            lock (_lockComputer)
            {
                gpu = _computer?.Hardware
                   .FirstOrDefault(hdw => hdw.HardwareType == HardwareType.GpuAti
                       || hdw.HardwareType == HardwareType.GpuNvidia);
            }

            return gpu != null ? gpu.Name : "Unknown";
        }
    }
}
