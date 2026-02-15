using CapFrameX.Contracts.Overlay;
using CapFrameX.Contracts.Sensor;
using CapFrameX.Data.Session.Classes;
using CapFrameX.Data.Session.Contracts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;

namespace CapFrameX.Test.Mocks
{
    /// <summary>
    /// Mock implementation of ISensorService for unit testing.
    /// Generates realistic sensor data with configurable hardware profiles.
    /// </summary>
    public class MockSensorService : ISensorService, IDisposable
    {
        private readonly Random _random;
        private readonly Subject<(DateTime, Dictionary<ISensorEntry, float>)> _sensorSnapshotSubject;
        private readonly Subject<TimeSpan> _osdUpdateSubject;
        private readonly List<MockSensorEntry> _sensorEntries;
        private readonly object _lock = new object();

        private Timer _emissionTimer;
        private bool _isLogging;
        private SessionSensorData2 _sessionSensorData;
        private DateTime _loggingStartTime;
        private DateTime _lastMeasureTime;
        private TimeSpan _loggingInterval = TimeSpan.FromMilliseconds(250);
        private TimeSpan _osdInterval = TimeSpan.FromMilliseconds(250);
        private HardwareProfile _profile;

        // Hardware info
        private string _cpuName = "AMD Ryzen 9 5900X";
        private string _gpuName = "NVIDIA GeForce RTX 3080";
        private string _gpuDriverVersion = "546.33";

        public IObservable<(DateTime, Dictionary<ISensorEntry, float>)> SensorSnapshotStream => _sensorSnapshotSubject.AsObservable();
        public IObservable<TimeSpan> OsdUpdateStream => _osdUpdateSubject.AsObservable();
        public TaskCompletionSource<bool> SensorServiceCompletionSource { get; }
        public Func<bool> IsSensorWebsocketActive { get; set; } = () => false;
        public Subject<bool> IsLoggingActiveStream { get; }

        /// <summary>
        /// Current hardware simulation profile.
        /// </summary>
        public HardwareProfile Profile
        {
            get => _profile;
            set
            {
                _profile = value;
                ApplyProfile(value);
            }
        }

        /// <summary>
        /// Interval for automatic sensor emission. Set to TimeSpan.Zero for manual control.
        /// </summary>
        public TimeSpan EmissionInterval { get; set; } = TimeSpan.FromMilliseconds(250);

        /// <summary>
        /// Creates a new MockSensorService with optional random seed for reproducible tests.
        /// </summary>
        public MockSensorService(int? seed = null)
        {
            _random = seed.HasValue ? new Random(seed.Value) : new Random();
            _sensorSnapshotSubject = new Subject<(DateTime, Dictionary<ISensorEntry, float>)>();
            _osdUpdateSubject = new Subject<TimeSpan>();
            IsLoggingActiveStream = new Subject<bool>();
            SensorServiceCompletionSource = new TaskCompletionSource<bool>();
            _sensorEntries = new List<MockSensorEntry>();
            _profile = HardwareProfile.GamingPC;

            InitializeDefaultSensors();

            // Complete initialization immediately for tests
            SensorServiceCompletionSource.SetResult(true);
        }

        /// <summary>
        /// Configures the mock CPU information.
        /// </summary>
        public void SetCpuInfo(string name)
        {
            _cpuName = name;
        }

        /// <summary>
        /// Configures the mock GPU information.
        /// </summary>
        public void SetGpuInfo(string name, string driverVersion)
        {
            _gpuName = name;
            _gpuDriverVersion = driverVersion;
        }

        /// <summary>
        /// Adds a custom sensor entry.
        /// </summary>
        public void AddSensor(MockSensorEntry sensor)
        {
            lock (_lock)
            {
                _sensorEntries.Add(sensor);
            }
        }

        /// <summary>
        /// Removes a sensor by identifier.
        /// </summary>
        public void RemoveSensor(string identifier)
        {
            lock (_lock)
            {
                _sensorEntries.RemoveAll(s => s.Identifier == identifier);
            }
        }

        /// <summary>
        /// Clears all sensors and reinitializes with defaults.
        /// </summary>
        public void ResetSensors()
        {
            lock (_lock)
            {
                _sensorEntries.Clear();
                InitializeDefaultSensors();
            }
        }

        /// <summary>
        /// Manually emit a sensor snapshot for synchronous testing.
        /// </summary>
        public void EmitSensorSnapshot()
        {
            var snapshot = GenerateSensorSnapshot();
            _sensorSnapshotSubject.OnNext(snapshot);

            if (_isLogging)
            {
                LogSensorData(snapshot);
            }
        }

        /// <summary>
        /// Emit multiple sensor snapshots synchronously.
        /// </summary>
        public void EmitSnapshots(int count, TimeSpan? interval = null)
        {
            var actualInterval = interval ?? TimeSpan.FromMilliseconds(250);
            for (int i = 0; i < count; i++)
            {
                EmitSensorSnapshot();
                if (interval.HasValue && i < count - 1)
                {
                    Thread.Sleep(actualInterval);
                }
            }
        }

        /// <summary>
        /// Starts automatic sensor emission at the configured interval.
        /// </summary>
        public void StartAutoEmission()
        {
            if (EmissionInterval <= TimeSpan.Zero) return;

            _emissionTimer = new Timer(
                _ => EmitSensorSnapshot(),
                null,
                EmissionInterval,
                EmissionInterval);
        }

        /// <summary>
        /// Stops automatic sensor emission.
        /// </summary>
        public void StopAutoEmission()
        {
            _emissionTimer?.Dispose();
            _emissionTimer = null;
        }

        #region ISensorService Implementation

        public void StartSensorLogging()
        {
            _isLogging = true;
            _loggingStartTime = DateTime.UtcNow;
            _lastMeasureTime = _loggingStartTime;
            _sessionSensorData = new SessionSensorData2();
            IsLoggingActiveStream.OnNext(true);
        }

        public Task StopSensorLogging()
        {
            _isLogging = false;
            IsLoggingActiveStream.OnNext(false);
            return Task.CompletedTask;
        }

        public ISessionSensorData2 GetSensorSessionData()
        {
            return _sessionSensorData ?? new SessionSensorData2();
        }

        public void ShutdownSensorService()
        {
            StopAutoEmission();
            _isLogging = false;
        }

        public string GetGpuDriverVersion() => _gpuDriverVersion;
        public string GetCpuName() => _cpuName;
        public string GetGpuName() => _gpuName;
        public ECpuVendor GetCpuVendor() => DetectCpuVendorFromName(_cpuName);
        public EGpuVendor GetGpuVendor() => DetectGpuVendorFromName(_gpuName);

        public string GetSensorTypeString(string identifier)
        {
            lock (_lock)
            {
                var sensor = _sensorEntries.FirstOrDefault(s => s.Identifier == identifier);
                return sensor?.SensorType ?? "Unknown";
            }
        }

        public void SetLoggingInterval(TimeSpan timeSpan)
        {
            _loggingInterval = timeSpan;
        }

        public void SetOSDInterval(TimeSpan timeSpan)
        {
            _osdInterval = timeSpan;
            _osdUpdateSubject.OnNext(timeSpan);
        }

        public Task<IEnumerable<ISensorEntry>> GetSensorEntries()
        {
            lock (_lock)
            {
                return Task.FromResult<IEnumerable<ISensorEntry>>(_sensorEntries.ToList());
            }
        }

        public IEnumerable<string> GetDetectedGpus()
        {
            // Return empty - hardware mocking would require LibreHardwareMonitor mocking
            return Enumerable.Empty<string>();
        }

        #endregion

        #region Private Methods

        private void InitializeDefaultSensors()
        {
            // CPU Sensors
            _sensorEntries.Add(new MockSensorEntry("/cpu/0/load/total", "CPU Total", "Cpu", "Load", true)
            {
                BaseValue = 35f, Variance = 15f, MinValue = 5f, MaxValue = 100f
            });
            _sensorEntries.Add(new MockSensorEntry("/cpu/0/load/max", "CPU Max", "Cpu", "Load", true)
            {
                BaseValue = 45f, Variance = 20f, MinValue = 10f, MaxValue = 100f
            });
            _sensorEntries.Add(new MockSensorEntry("/cpu/0/clock/max", "CPU Max Clock", "Cpu", "Clock", true)
            {
                BaseValue = 4500f, Variance = 200f, MinValue = 3000f, MaxValue = 5000f
            });
            _sensorEntries.Add(new MockSensorEntry("/cpu/0/power/package", "CPU Package Power", "Cpu", "Power", true)
            {
                BaseValue = 95f, Variance = 30f, MinValue = 20f, MaxValue = 150f
            });
            _sensorEntries.Add(new MockSensorEntry("/cpu/0/temperature/package", "CPU Package Temp", "Cpu", "Temperature", true)
            {
                BaseValue = 65f, Variance = 10f, MinValue = 35f, MaxValue = 95f
            });

            // GPU Sensors
            _sensorEntries.Add(new MockSensorEntry("/gpu/0/load/core", "GPU Core Load", "GpuNvidia", "Load", true)
            {
                BaseValue = 85f, Variance = 10f, MinValue = 0f, MaxValue = 100f
            });
            _sensorEntries.Add(new MockSensorEntry("/gpu/0/clock/core", "GPU Core Clock", "GpuNvidia", "Clock", true)
            {
                BaseValue = 1900f, Variance = 100f, MinValue = 300f, MaxValue = 2100f
            });
            _sensorEntries.Add(new MockSensorEntry("/gpu/0/power/total", "GPU Power", "GpuNvidia", "Power", true)
            {
                BaseValue = 280f, Variance = 50f, MinValue = 30f, MaxValue = 350f
            });
            _sensorEntries.Add(new MockSensorEntry("/gpu/0/temperature/core", "GPU Core Temp", "GpuNvidia", "Temperature", true)
            {
                BaseValue = 72f, Variance = 8f, MinValue = 35f, MaxValue = 90f
            });
            _sensorEntries.Add(new MockSensorEntry("/gpu/0/factor/powerlimit", "GPU Power Limit", "GpuNvidia", "Factor", true)
            {
                BaseValue = 100f, Variance = 0f, MinValue = 50f, MaxValue = 120f
            });

            // Memory Sensors
            _sensorEntries.Add(new MockSensorEntry("/memory/0/data/used", "Used Memory Game", "Memory", "Data", true)
            {
                BaseValue = 12.5f, Variance = 2f, MinValue = 4f, MaxValue = 28f
            });
            _sensorEntries.Add(new MockSensorEntry("/gpu/0/smalldata/dedicated", "GPU Memory Dedicated", "GpuNvidia", "SmallData", true)
            {
                BaseValue = 6500f, Variance = 1500f, MinValue = 500f, MaxValue = 10000f
            });
            _sensorEntries.Add(new MockSensorEntry("/gpu/0/data/dedicated", "GPU Memory Dedicated GB", "GpuNvidia", "Data", true)
            {
                BaseValue = 6.5f, Variance = 1.5f, MinValue = 0.5f, MaxValue = 10f
            });
        }

        private void ApplyProfile(HardwareProfile profile)
        {
            switch (profile)
            {
                case HardwareProfile.GamingPC:
                    _cpuName = "AMD Ryzen 9 5900X";
                    _gpuName = "NVIDIA GeForce RTX 3080";
                    SetSensorBaseValues(cpuLoad: 35, gpuLoad: 85, cpuTemp: 65, gpuTemp: 72, gpuPower: 280);
                    break;

                case HardwareProfile.LaptopIntegrated:
                    _cpuName = "Intel Core i7-1165G7";
                    _gpuName = "Intel Iris Xe Graphics";
                    SetSensorBaseValues(cpuLoad: 50, gpuLoad: 60, cpuTemp: 75, gpuTemp: 65, gpuPower: 25);
                    break;

                case HardwareProfile.Workstation:
                    _cpuName = "AMD Threadripper 3990X";
                    _gpuName = "NVIDIA RTX A6000";
                    SetSensorBaseValues(cpuLoad: 25, gpuLoad: 40, cpuTemp: 55, gpuTemp: 55, gpuPower: 150);
                    break;

                case HardwareProfile.HighEndGaming:
                    _cpuName = "Intel Core i9-14900K";
                    _gpuName = "NVIDIA GeForce RTX 4090";
                    SetSensorBaseValues(cpuLoad: 30, gpuLoad: 95, cpuTemp: 70, gpuTemp: 75, gpuPower: 420);
                    break;

                case HardwareProfile.BudgetPC:
                    _cpuName = "AMD Ryzen 5 5600";
                    _gpuName = "NVIDIA GeForce RTX 3060";
                    SetSensorBaseValues(cpuLoad: 45, gpuLoad: 90, cpuTemp: 60, gpuTemp: 68, gpuPower: 170);
                    break;

                case HardwareProfile.ThermalThrottling:
                    _cpuName = "Intel Core i9-12900K";
                    _gpuName = "NVIDIA GeForce RTX 3090";
                    SetSensorBaseValues(cpuLoad: 85, gpuLoad: 70, cpuTemp: 95, gpuTemp: 88, gpuPower: 300);
                    break;
            }
        }

        private void SetSensorBaseValues(float cpuLoad, float gpuLoad, float cpuTemp, float gpuTemp, float gpuPower)
        {
            lock (_lock)
            {
                foreach (var sensor in _sensorEntries)
                {
                    switch (sensor.Identifier)
                    {
                        case "/cpu/0/load/total":
                            sensor.BaseValue = cpuLoad;
                            break;
                        case "/gpu/0/load/core":
                            sensor.BaseValue = gpuLoad;
                            break;
                        case "/cpu/0/temperature/package":
                            sensor.BaseValue = cpuTemp;
                            break;
                        case "/gpu/0/temperature/core":
                            sensor.BaseValue = gpuTemp;
                            break;
                        case "/gpu/0/power/total":
                            sensor.BaseValue = gpuPower;
                            break;
                    }
                }
            }
        }

        private (DateTime, Dictionary<ISensorEntry, float>) GenerateSensorSnapshot()
        {
            var timestamp = DateTime.UtcNow;
            var sensorValues = new Dictionary<ISensorEntry, float>();

            lock (_lock)
            {
                foreach (var sensor in _sensorEntries)
                {
                    float value = GenerateSensorValue(sensor);
                    sensor.Value = value;
                    sensorValues[sensor] = value;
                }
            }

            return (timestamp, sensorValues);
        }

        private float GenerateSensorValue(MockSensorEntry sensor)
        {
            // Apply Gaussian-like variance
            double u1 = 1.0 - _random.NextDouble();
            double u2 = 1.0 - _random.NextDouble();
            double gaussian = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);

            float value = sensor.BaseValue + (float)(gaussian * sensor.Variance);

            // Apply scenario-specific spikes
            if (sensor.SpikeChance > 0 && _random.NextDouble() < sensor.SpikeChance)
            {
                value = sensor.BaseValue + sensor.Variance * 2;
            }

            // Clamp to valid range
            return Math.Max(sensor.MinValue, Math.Min(sensor.MaxValue, value));
        }

        private static ECpuVendor DetectCpuVendorFromName(string cpuName)
        {
            var nameLower = cpuName?.ToLowerInvariant() ?? string.Empty;
            if (nameLower.Contains("intel") || nameLower.Contains("core i"))
                return ECpuVendor.Intel;
            if (nameLower.Contains("amd") || nameLower.Contains("ryzen") || nameLower.Contains("threadripper"))
                return ECpuVendor.Amd;
            return ECpuVendor.Unknown;
        }

        private static EGpuVendor DetectGpuVendorFromName(string gpuName)
        {
            var nameLower = gpuName?.ToLowerInvariant() ?? string.Empty;
            if (nameLower.Contains("nvidia") || nameLower.Contains("geforce") || nameLower.Contains("rtx") || nameLower.Contains("gtx"))
                return EGpuVendor.Nvidia;
            if (nameLower.Contains("amd") || nameLower.Contains("radeon") || nameLower.Contains("rx "))
                return EGpuVendor.Amd;
            if (nameLower.Contains("intel") || nameLower.Contains("arc") || nameLower.Contains("iris") || nameLower.Contains("uhd"))
                return EGpuVendor.Intel;
            return EGpuVendor.Unknown;
        }

        private void LogSensorData((DateTime, Dictionary<ISensorEntry, float>) snapshot)
        {
            var (timestamp, sensorValues) = snapshot;

            // Add measure time
            double measureTimeSeconds = (timestamp - _loggingStartTime).TotalSeconds;
            double betweenMeasureTime = (timestamp - _lastMeasureTime).TotalMilliseconds;
            _lastMeasureTime = timestamp;

            _sessionSensorData.MeasureTime.Values.AddLast(measureTimeSeconds);
            _sessionSensorData.BetweenMeasureTime.Values.AddLast(betweenMeasureTime);

            // Add sensor values
            foreach (var kvp in sensorValues)
            {
                var sensor = kvp.Key;
                var value = kvp.Value;

                string key = sensor.Identifier;
                if (!_sessionSensorData.ContainsKey(key))
                {
                    _sessionSensorData[key] = new SessionSensorEntry(sensor.Name, sensor.SensorType);
                }

                ((SessionSensorEntry)_sessionSensorData[key]).Values.AddLast(value);
            }
        }

        #endregion

        public void Dispose()
        {
            StopAutoEmission();
            _sensorSnapshotSubject?.Dispose();
            _osdUpdateSubject?.Dispose();
            IsLoggingActiveStream?.Dispose();
        }
    }

    /// <summary>
    /// Mock sensor entry with configurable value generation parameters.
    /// </summary>
    public class MockSensorEntry : ISensorEntry
    {
        public string Identifier { get; set; }
        public string SortKey { get; set; }
        public string Name { get; set; }
        public object Value { get; set; }
        public string HardwareType { get; set; }
        public string SensorType { get; set; }
        public bool IsPresentationDefault { get; set; }
        public string HardwareName { get; set; }

        // Value generation parameters
        public float BaseValue { get; set; }
        public float Variance { get; set; }
        public float MinValue { get; set; }
        public float MaxValue { get; set; }
        public double SpikeChance { get; set; }

        public MockSensorEntry(string identifier, string name, string hardwareType, string sensorType, bool isPresentationDefault = false)
        {
            Identifier = identifier;
            Name = name;
            SortKey = identifier;
            HardwareType = hardwareType;
            SensorType = sensorType;
            IsPresentationDefault = isPresentationDefault;
            Value = 0f;
            BaseValue = 50f;
            Variance = 10f;
            MinValue = 0f;
            MaxValue = 100f;
            SpikeChance = 0;
        }
    }

    /// <summary>
    /// Predefined hardware profiles for realistic sensor simulation.
    /// </summary>
    public enum HardwareProfile
    {
        /// <summary>
        /// Typical gaming PC: Ryzen 9 5900X + RTX 3080.
        /// </summary>
        GamingPC,

        /// <summary>
        /// Laptop with integrated graphics: i7-1165G7 + Iris Xe.
        /// </summary>
        LaptopIntegrated,

        /// <summary>
        /// High-end workstation: Threadripper + RTX A6000.
        /// </summary>
        Workstation,

        /// <summary>
        /// Enthusiast gaming: i9-14900K + RTX 4090.
        /// </summary>
        HighEndGaming,

        /// <summary>
        /// Budget gaming PC: Ryzen 5 5600 + RTX 3060.
        /// </summary>
        BudgetPC,

        /// <summary>
        /// System under thermal stress with throttling.
        /// </summary>
        ThermalThrottling
    }
}
