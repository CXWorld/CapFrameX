using CapFrameX.Contracts.PMD;
using CapFrameX.PMD.Benchlab;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using System.Windows;
using BenchlabSensor = CapFrameX.PMD.Benchlab.Sensor;
using BenchlabSensorSample = CapFrameX.PMD.Benchlab.SensorSample;
using BenchlabSensorType = CapFrameX.PMD.Benchlab.SensorType;

namespace CapFrameX.Test.Mocks
{
    /// <summary>
    /// Power profiles for simulating different system configurations
    /// </summary>
    public enum BenchlabPowerProfile
    {
        /// <summary>High-end gaming PC (350W GPU, 125W CPU)</summary>
        HighEndGaming,
        /// <summary>Mid-range gaming PC (200W GPU, 65W CPU)</summary>
        MidRangeGaming,
        /// <summary>Budget gaming PC (120W GPU, 65W CPU)</summary>
        BudgetGaming,
        /// <summary>Workstation (250W GPU, 125W CPU)</summary>
        Workstation,
        /// <summary>System idle state (30W GPU, 15W CPU)</summary>
        Idle,
        /// <summary>Maximum power stress test</summary>
        PowerVirus,
        /// <summary>Transient power spikes simulation</summary>
        TransientSpikes
    }

    /// <summary>
    /// Mock implementation of IBenchlabService for unit testing.
    /// Simulates Benchlab power measurement device with realistic sensor data.
    /// </summary>
    public class MockBenchlabService : IBenchlabService
    {
        private readonly Subject<BenchlabSensorSample> _pmdSensorStream = new Subject<BenchlabSensorSample>();
        private readonly Subject<EPmdServiceStatus> _pmdServiceStatusStream = new Subject<EPmdServiceStatus>();
        private readonly Random _random;
        private readonly List<BenchlabSensor> _sensorTemplate;

        private IDisposable _timerSubscription;
        private bool _isServiceRunning;
        private BenchlabPowerProfile _currentProfile;
        private long _sampleCount;
        private double _transientPhase;

        // Sensor indices (matching Benchlab device)
        private const int CPU_POWER_INDEX = 0;
        private const int GPU_POWER_INDEX = 1;
        private const int MAINBOARD_POWER_INDEX = 2;
        private const int SYSTEM_POWER_INDEX = 3;
        private const int CPU_TEMP_INDEX = 4;
        private const int GPU_TEMP_INDEX = 5;
        private const int AMBIENT_TEMP_INDEX = 6;
        private const int CPU_FAN_RPM_INDEX = 7;
        private const int GPU_FAN_RPM_INDEX = 8;
        private const int CPU_VOLTAGE_INDEX = 9;
        private const int GPU_VOLTAGE_INDEX = 10;

        public int CpuPowerSensorIndex => CPU_POWER_INDEX;
        public int GpuPowerSensorIndex => GPU_POWER_INDEX;
        public int MainboardPowerSensorIndex => MAINBOARD_POWER_INDEX;
        public int SytemPowerSensorIndex => SYSTEM_POWER_INDEX;

        public int MonitoringInterval { get; set; } = 100;
        public int MinMonitoringInterval { get; set; } = 25;
        public bool IsServiceRunning => _isServiceRunning;

        public IObservable<BenchlabSensorSample> PmdSensorStream => _pmdSensorStream.AsObservable();
        public IObservable<EPmdServiceStatus> PmdServiceStatusStream => _pmdServiceStatusStream.AsObservable();

        /// <summary>
        /// Creates a new MockBenchlabService with specified profile and optional deterministic seed.
        /// </summary>
        /// <param name="profile">Power profile to simulate</param>
        /// <param name="seed">Optional seed for deterministic random generation</param>
        public MockBenchlabService(BenchlabPowerProfile profile = BenchlabPowerProfile.MidRangeGaming, int? seed = null)
        {
            _random = seed.HasValue ? new Random(seed.Value) : new Random();
            _currentProfile = profile;
            _sensorTemplate = CreateSensorTemplate();
        }

        private List<BenchlabSensor> CreateSensorTemplate()
        {
            return new List<BenchlabSensor>
            {
                new BenchlabSensor(0, "CPU_P", "CPU Power", BenchlabSensorType.Power),
                new BenchlabSensor(1, "GPU_P", "GPU Power", BenchlabSensorType.Power),
                new BenchlabSensor(2, "MB_P", "Mainboard Power", BenchlabSensorType.Power),
                new BenchlabSensor(3, "SYS_P", "System Power", BenchlabSensorType.Power),
                new BenchlabSensor(4, "CPU_T", "CPU Temperature", BenchlabSensorType.Temperature),
                new BenchlabSensor(5, "GPU_T", "GPU Temperature", BenchlabSensorType.Temperature),
                new BenchlabSensor(6, "AMB_T", "Ambient Temperature", BenchlabSensorType.Temperature),
                new BenchlabSensor(7, "CPU_F", "CPU Fan Speed", BenchlabSensorType.Revolutions),
                new BenchlabSensor(8, "GPU_F", "GPU Fan Speed", BenchlabSensorType.Revolutions),
                new BenchlabSensor(9, "CPU_V", "CPU Voltage", BenchlabSensorType.Voltage),
                new BenchlabSensor(10, "GPU_V", "GPU Voltage", BenchlabSensorType.Voltage),
            };
        }

        /// <summary>
        /// Changes the power profile for subsequent samples.
        /// </summary>
        public void SetProfile(BenchlabPowerProfile profile)
        {
            _currentProfile = profile;
        }

        public Task StartService()
        {
            if (_isServiceRunning) return Task.CompletedTask;

            _isServiceRunning = true;
            _sampleCount = 0;
            _pmdServiceStatusStream.OnNext(EPmdServiceStatus.Running);

            _timerSubscription = Observable.Interval(TimeSpan.FromMilliseconds(MonitoringInterval))
                .Subscribe(_ => EmitSample());

            return Task.CompletedTask;
        }

        public void ShutDownService()
        {
            _isServiceRunning = false;
            _timerSubscription?.Dispose();
            _timerSubscription = null;
            _pmdServiceStatusStream.OnNext(EPmdServiceStatus.Stopped);
        }

        /// <summary>
        /// Manually emit a single sample using current profile settings.
        /// Useful for synchronous tests.
        /// </summary>
        public void EmitSample()
        {
            var sample = GenerateSample();
            _pmdSensorStream.OnNext(sample);
            _sampleCount++;
        }

        /// <summary>
        /// Emit a custom sample with specified power values.
        /// </summary>
        /// <param name="gpuPower">GPU power in watts</param>
        /// <param name="cpuPower">CPU power in watts</param>
        /// <param name="mainboardPower">Mainboard power in watts</param>
        public void EmitCustomSample(double gpuPower, double cpuPower, double mainboardPower = 50)
        {
            var sensors = CloneSensorTemplate();
            var systemPower = gpuPower + cpuPower + mainboardPower;

            sensors[CPU_POWER_INDEX].Value = cpuPower;
            sensors[GPU_POWER_INDEX].Value = gpuPower;
            sensors[MAINBOARD_POWER_INDEX].Value = mainboardPower;
            sensors[SYSTEM_POWER_INDEX].Value = systemPower;

            // Generate correlated temperatures
            sensors[CPU_TEMP_INDEX].Value = 40 + (cpuPower / 125.0) * 45 + NextGaussian() * 2;
            sensors[GPU_TEMP_INDEX].Value = 35 + (gpuPower / 350.0) * 50 + NextGaussian() * 2;
            sensors[AMBIENT_TEMP_INDEX].Value = 25 + NextGaussian() * 1;

            // Generate correlated fan speeds
            sensors[CPU_FAN_RPM_INDEX].Value = 800 + (sensors[CPU_TEMP_INDEX].Value - 40) * 30 + NextGaussian() * 50;
            sensors[GPU_FAN_RPM_INDEX].Value = 1000 + (sensors[GPU_TEMP_INDEX].Value - 35) * 40 + NextGaussian() * 50;

            // Generate voltages
            sensors[CPU_VOLTAGE_INDEX].Value = 1.1 + (cpuPower / 125.0) * 0.25 + NextGaussian() * 0.01;
            sensors[GPU_VOLTAGE_INDEX].Value = 0.9 + (gpuPower / 350.0) * 0.15 + NextGaussian() * 0.01;

            foreach (var sensor in sensors)
            {
                sensor.IsValid = true;
            }

            var sample = new BenchlabSensorSample
            {
                TimeStamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                Sensors = sensors
            };

            _pmdSensorStream.OnNext(sample);
            _sampleCount++;
        }

        /// <summary>
        /// Emit a service status change.
        /// </summary>
        public void EmitServiceStatus(EPmdServiceStatus status)
        {
            if (status == EPmdServiceStatus.Running)
                _isServiceRunning = true;
            else if (status == EPmdServiceStatus.Stopped || status == EPmdServiceStatus.Error)
                _isServiceRunning = false;

            _pmdServiceStatusStream.OnNext(status);
        }

        private BenchlabSensorSample GenerateSample()
        {
            var (baseCpuPower, baseGpuPower, baseMainboardPower, variance) = GetProfileParameters();
            var sensors = CloneSensorTemplate();

            double cpuPower, gpuPower, mainboardPower;

            if (_currentProfile == BenchlabPowerProfile.TransientSpikes)
            {
                _transientPhase += 0.1;
                var spike = Math.Sin(_transientPhase) > 0.8 ? NextGaussian() * 100 : 0;
                cpuPower = baseCpuPower + NextGaussian() * variance + Math.Abs(spike * 0.3);
                gpuPower = baseGpuPower + NextGaussian() * variance + Math.Abs(spike);
                mainboardPower = baseMainboardPower + NextGaussian() * (variance * 0.3);
            }
            else
            {
                cpuPower = Math.Max(5, baseCpuPower + NextGaussian() * variance);
                gpuPower = Math.Max(5, baseGpuPower + NextGaussian() * variance);
                mainboardPower = Math.Max(10, baseMainboardPower + NextGaussian() * (variance * 0.3));
            }

            var systemPower = cpuPower + gpuPower + mainboardPower;

            sensors[CPU_POWER_INDEX].Value = cpuPower;
            sensors[GPU_POWER_INDEX].Value = gpuPower;
            sensors[MAINBOARD_POWER_INDEX].Value = mainboardPower;
            sensors[SYSTEM_POWER_INDEX].Value = systemPower;

            // Generate correlated temperatures based on power
            double cpuTemp = 40 + (cpuPower / 125.0) * 45 + NextGaussian() * 2;
            double gpuTemp = 35 + (gpuPower / 350.0) * 50 + NextGaussian() * 2;
            double ambientTemp = 25 + NextGaussian() * 1;

            sensors[CPU_TEMP_INDEX].Value = Math.Min(95, Math.Max(30, cpuTemp));
            sensors[GPU_TEMP_INDEX].Value = Math.Min(90, Math.Max(25, gpuTemp));
            sensors[AMBIENT_TEMP_INDEX].Value = Math.Max(15, ambientTemp);

            // Generate correlated fan speeds based on temperature
            double cpuFanRpm = 800 + (cpuTemp - 40) * 30 + NextGaussian() * 50;
            double gpuFanRpm = 1000 + (gpuTemp - 35) * 40 + NextGaussian() * 50;

            sensors[CPU_FAN_RPM_INDEX].Value = Math.Max(500, cpuFanRpm);
            sensors[GPU_FAN_RPM_INDEX].Value = Math.Max(800, gpuFanRpm);

            // Generate voltages correlated with power draw
            sensors[CPU_VOLTAGE_INDEX].Value = 1.1 + (cpuPower / 125.0) * 0.25 + NextGaussian() * 0.01;
            sensors[GPU_VOLTAGE_INDEX].Value = 0.9 + (gpuPower / 350.0) * 0.15 + NextGaussian() * 0.01;

            foreach (var sensor in sensors)
            {
                sensor.IsValid = true;
            }

            return new BenchlabSensorSample
            {
                TimeStamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                Sensors = sensors
            };
        }

        private (double cpuPower, double gpuPower, double mainboardPower, double variance) GetProfileParameters()
        {
            switch (_currentProfile)
            {
                case BenchlabPowerProfile.HighEndGaming:
                    return (125, 350, 60, 15);
                case BenchlabPowerProfile.MidRangeGaming:
                    return (65, 200, 50, 10);
                case BenchlabPowerProfile.BudgetGaming:
                    return (65, 120, 45, 8);
                case BenchlabPowerProfile.Workstation:
                    return (125, 250, 55, 12);
                case BenchlabPowerProfile.Idle:
                    return (15, 30, 35, 3);
                case BenchlabPowerProfile.PowerVirus:
                    return (175, 450, 70, 20);
                case BenchlabPowerProfile.TransientSpikes:
                    return (100, 280, 55, 25);
                default:
                    return (65, 200, 50, 10);
            }
        }

        private List<BenchlabSensor> CloneSensorTemplate()
        {
            return _sensorTemplate.Select(s => new BenchlabSensor(s.Id, s.ShortName, s.Name, s.Type)
            {
                Value = s.Value,
                IsValid = s.IsValid
            }).ToList();
        }

        /// <summary>
        /// Generate a Gaussian-distributed random number using Box-Muller transform.
        /// </summary>
        private double NextGaussian()
        {
            double u1 = 1.0 - _random.NextDouble();
            double u2 = 1.0 - _random.NextDouble();
            return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);
        }

        public IEnumerable<Point> GetEPS12VPowerPmdDataPoints(IList<BenchlabSensorSample> sensorData)
        {
            if (sensorData == null || sensorData.Count == 0)
                yield break;

            var minTimeStamp = sensorData.First().TimeStamp;
            foreach (var sample in sensorData)
            {
                yield return new Point((sample.TimeStamp - minTimeStamp) * 1E-03, sample.Sensors[CpuPowerSensorIndex].Value);
            }
        }

        public IEnumerable<Point> GetPciExpressPowerPmdDataPoints(IList<BenchlabSensorSample> sensorData)
        {
            if (sensorData == null || sensorData.Count == 0)
                yield break;

            var minTimeStamp = sensorData.First().TimeStamp;
            foreach (var sample in sensorData)
            {
                yield return new Point((sample.TimeStamp - minTimeStamp) * 1E-03, sample.Sensors[GpuPowerSensorIndex].Value);
            }
        }

        /// <summary>
        /// Generate a batch of samples for bulk testing.
        /// </summary>
        /// <param name="count">Number of samples to generate</param>
        /// <param name="intervalMs">Time interval between samples in milliseconds</param>
        /// <returns>List of sensor samples</returns>
        public IList<BenchlabSensorSample> GenerateSampleBatch(int count, int intervalMs = 100)
        {
            var samples = new List<BenchlabSensorSample>();
            var startTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            for (int i = 0; i < count; i++)
            {
                var sample = GenerateSample();
                sample.TimeStamp = startTime + (i * intervalMs);
                samples.Add(sample);
            }

            return samples;
        }

        /// <summary>
        /// Generate samples with a power ramp (increasing or decreasing power over time).
        /// </summary>
        /// <param name="count">Number of samples</param>
        /// <param name="startGpuPower">Starting GPU power</param>
        /// <param name="endGpuPower">Ending GPU power</param>
        /// <param name="startCpuPower">Starting CPU power</param>
        /// <param name="endCpuPower">Ending CPU power</param>
        /// <param name="intervalMs">Interval between samples</param>
        public IList<BenchlabSensorSample> GeneratePowerRamp(int count, double startGpuPower, double endGpuPower,
            double startCpuPower, double endCpuPower, int intervalMs = 100)
        {
            var samples = new List<BenchlabSensorSample>();
            var startTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            for (int i = 0; i < count; i++)
            {
                double t = (double)i / (count - 1);
                double gpuPower = startGpuPower + (endGpuPower - startGpuPower) * t + NextGaussian() * 5;
                double cpuPower = startCpuPower + (endCpuPower - startCpuPower) * t + NextGaussian() * 3;
                double mainboardPower = 50 + NextGaussian() * 3;

                var sensors = CloneSensorTemplate();
                var systemPower = gpuPower + cpuPower + mainboardPower;

                sensors[CPU_POWER_INDEX].Value = Math.Max(5, cpuPower);
                sensors[GPU_POWER_INDEX].Value = Math.Max(5, gpuPower);
                sensors[MAINBOARD_POWER_INDEX].Value = mainboardPower;
                sensors[SYSTEM_POWER_INDEX].Value = systemPower;

                // Generate correlated values
                sensors[CPU_TEMP_INDEX].Value = 40 + (cpuPower / 125.0) * 45 + NextGaussian() * 2;
                sensors[GPU_TEMP_INDEX].Value = 35 + (gpuPower / 350.0) * 50 + NextGaussian() * 2;
                sensors[AMBIENT_TEMP_INDEX].Value = 25 + NextGaussian() * 1;
                sensors[CPU_FAN_RPM_INDEX].Value = 800 + (sensors[CPU_TEMP_INDEX].Value - 40) * 30;
                sensors[GPU_FAN_RPM_INDEX].Value = 1000 + (sensors[GPU_TEMP_INDEX].Value - 35) * 40;
                sensors[CPU_VOLTAGE_INDEX].Value = 1.1 + (cpuPower / 125.0) * 0.25;
                sensors[GPU_VOLTAGE_INDEX].Value = 0.9 + (gpuPower / 350.0) * 0.15;

                foreach (var sensor in sensors)
                {
                    sensor.IsValid = true;
                }

                samples.Add(new BenchlabSensorSample
                {
                    TimeStamp = startTime + (i * intervalMs),
                    Sensors = sensors
                });
            }

            return samples;
        }
    }
}
