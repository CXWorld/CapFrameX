using CapFrameX.Contracts.Configuration;
using CapFrameX.Contracts.PMD;
using CapFrameX.PMD.Powenetics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Windows;

namespace CapFrameX.Test.Mocks
{
    /// <summary>
    /// Mock implementation of IPoweneticsService for unit testing.
    /// Simulates realistic power measurement data from a Powenetics device.
    /// </summary>
    public class MockPoweneticsService : IPoweneticsService, IDisposable
    {
        private const int CHANNEL_COUNT = 42;

        private readonly Random _random;
        private readonly Subject<PoweneticsChannel[]> _pmdChannelSubject;
        private readonly Subject<EPmdDriverStatus> _pmdStatusSubject;
        private readonly Subject<int> _pmdThroughputSubject;
        private readonly Subject<int> _lostPacketsSubject;
        private readonly List<string> _availablePorts;

        private Timer _emissionTimer;
        private bool _isRunning;
        private long _currentTimestamp;
        private int _sampleCount;
        private PowerProfile _profile;

        // Channel base values for simulation
        private float[] _channelBaseValues;
        private float[] _channelVariances;

        public IObservable<PoweneticsChannel[]> PmdChannelStream => _pmdChannelSubject.AsObservable();
        public IObservable<EPmdDriverStatus> PmdStatusStream => _pmdStatusSubject.AsObservable();
        public IObservable<int> PmdThroughput => _pmdThroughputSubject.AsObservable();
        public IObservable<int> LostPacketsCounterStream => _lostPacketsSubject.AsObservable();

        public string PortName { get; set; } = "COM3";
        public int DownSamplingSize { get; set; } = 1;
        public PmdSampleFilterMode DownSamplingMode { get; set; } = PmdSampleFilterMode.Average;
        public bool IsServiceRunning => _isRunning;

        /// <summary>
        /// Current power profile for simulation.
        /// </summary>
        public PowerProfile Profile
        {
            get => _profile;
            set
            {
                _profile = value;
                ApplyProfile(value);
            }
        }

        /// <summary>
        /// Interval in milliseconds between sample emissions.
        /// Default is 1ms (1000 samples/sec) to match real device.
        /// Set to 0 for manual emission control.
        /// </summary>
        public int EmissionIntervalMs { get; set; } = 1;

        /// <summary>
        /// Simulated sample rate (samples per second) for throughput reporting.
        /// </summary>
        public int SimulatedSampleRate { get; set; } = 1000;

        /// <summary>
        /// Creates a new MockPoweneticsService with optional random seed.
        /// </summary>
        public MockPoweneticsService(int? seed = null)
        {
            _random = seed.HasValue ? new Random(seed.Value) : new Random();
            _pmdChannelSubject = new Subject<PoweneticsChannel[]>();
            _pmdStatusSubject = new Subject<EPmdDriverStatus>();
            _pmdThroughputSubject = new Subject<int>();
            _lostPacketsSubject = new Subject<int>();

            _availablePorts = new List<string> { "COM1", "COM3", "COM4" };
            _channelBaseValues = new float[CHANNEL_COUNT];
            _channelVariances = new float[CHANNEL_COUNT];
            _profile = PowerProfile.HighEndGaming;

            // Initialize channel mapping if not already done
            if (PoweneticsChannelExtensions.PmdChannelIndexMapping == null)
            {
                PoweneticsChannelExtensions.Initialize();
            }

            ApplyProfile(_profile);
        }

        /// <summary>
        /// Adds a COM port to the available ports list.
        /// </summary>
        public void AddAvailablePort(string portName)
        {
            if (!_availablePorts.Contains(portName))
            {
                _availablePorts.Add(portName);
            }
        }

        /// <summary>
        /// Clears and sets the available COM ports.
        /// </summary>
        public void SetAvailablePorts(params string[] ports)
        {
            _availablePorts.Clear();
            _availablePorts.AddRange(ports);
        }

        /// <summary>
        /// Manually emit a single power sample for synchronous testing.
        /// </summary>
        public void EmitSample()
        {
            var channels = GenerateChannelData();
            _pmdChannelSubject.OnNext(channels);
            _sampleCount++;
        }

        /// <summary>
        /// Emit multiple samples synchronously.
        /// </summary>
        public void EmitSamples(int count)
        {
            for (int i = 0; i < count; i++)
            {
                EmitSample();
            }
        }

        /// <summary>
        /// Emit a sample with custom power values.
        /// </summary>
        public void EmitCustomSample(float gpuPower, float cpuPower, float systemPower)
        {
            var channels = GenerateChannelData();

            // Distribute GPU power across PCIe channels
            DistributePower(channels, PoweneticsChannelExtensions.GPUPowerIndexGroup, gpuPower);

            // Distribute CPU power across EPS channels
            DistributePower(channels, PoweneticsChannelExtensions.EPSPowerIndexGroup, cpuPower);

            // Calculate remaining system power for ATX
            float atxPower = systemPower - gpuPower - cpuPower;
            if (atxPower > 0)
            {
                DistributePower(channels, PoweneticsChannelExtensions.ATXPowerIndexGroup, atxPower);
            }

            _pmdChannelSubject.OnNext(channels);
            _sampleCount++;
        }

        /// <summary>
        /// Simulates packet loss for testing error handling.
        /// </summary>
        public void SimulatePacketLoss(int lostCount)
        {
            _lostPacketsSubject.OnNext(lostCount);
        }

        /// <summary>
        /// Emits a driver status change.
        /// </summary>
        public void EmitStatus(EPmdDriverStatus status)
        {
            _pmdStatusSubject.OnNext(status);
        }

        /// <summary>
        /// Emits throughput data.
        /// </summary>
        public void EmitThroughput(int samplesPerSecond)
        {
            _pmdThroughputSubject.OnNext(samplesPerSecond);
        }

        #region IPoweneticsService Implementation

        public bool StartDriver()
        {
            if (_isRunning) return false;
            if (!_availablePorts.Contains(PortName)) return false;

            _isRunning = true;
            _currentTimestamp = 0;
            _sampleCount = 0;

            _pmdStatusSubject.OnNext(EPmdDriverStatus.Connected);

            if (EmissionIntervalMs > 0)
            {
                _emissionTimer = new Timer(
                    _ =>
                    {
                        EmitSample();

                        // Emit throughput every second
                        if (_sampleCount % SimulatedSampleRate == 0)
                        {
                            _pmdThroughputSubject.OnNext(SimulatedSampleRate);
                        }
                    },
                    null,
                    EmissionIntervalMs,
                    EmissionIntervalMs);
            }

            return true;
        }

        public bool ShutDownDriver()
        {
            if (!_isRunning) return false;

            _isRunning = false;
            _emissionTimer?.Dispose();
            _emissionTimer = null;

            _pmdStatusSubject.OnNext(EPmdDriverStatus.Ready);
            return true;
        }

        public string[] GetPortNames()
        {
            return _availablePorts.ToArray();
        }

        public IEnumerable<Point> GetEPS12VPowerPmdDataPoints(IList<PoweneticsChannel[]> channelData)
        {
            if (channelData == null || channelData.Count == 0)
                yield break;

            var epsPowerIndices = PoweneticsChannelExtensions.EPSPowerIndexGroup;

            foreach (var channels in channelData)
            {
                if (channels == null || channels.Length < CHANNEL_COUNT)
                    continue;

                float totalPower = 0;
                long timestamp = 0;

                foreach (var idx in epsPowerIndices)
                {
                    totalPower += channels[idx].Value;
                    timestamp = channels[idx].TimeStamp;
                }

                yield return new Point(timestamp, totalPower);
            }
        }

        public IEnumerable<Point> GetPciExpressPowerPmdDataPoints(IList<PoweneticsChannel[]> channelData)
        {
            if (channelData == null || channelData.Count == 0)
                yield break;

            var gpuPowerIndices = PoweneticsChannelExtensions.GPUPowerIndexGroup;

            foreach (var channels in channelData)
            {
                if (channels == null || channels.Length < CHANNEL_COUNT)
                    continue;

                float totalPower = 0;
                long timestamp = 0;

                foreach (var idx in gpuPowerIndices)
                {
                    totalPower += channels[idx].Value;
                    timestamp = channels[idx].TimeStamp;
                }

                yield return new Point(timestamp, totalPower);
            }
        }

        #endregion

        #region Private Methods

        private void ApplyProfile(PowerProfile profile)
        {
            // Reset all channels
            Array.Clear(_channelBaseValues, 0, CHANNEL_COUNT);
            Array.Clear(_channelVariances, 0, CHANNEL_COUNT);

            switch (profile)
            {
                case PowerProfile.HighEndGaming:
                    // RTX 4090 class: ~450W GPU, ~150W CPU
                    SetGpuPower(350f, 50f);      // Base 350W, variance 50W
                    SetCpuPower(120f, 20f);      // Base 120W, variance 20W
                    SetAtxPower(30f, 5f);        // Base 30W, variance 5W
                    break;

                case PowerProfile.MidRangeGaming:
                    // RTX 3070 class: ~220W GPU, ~100W CPU
                    SetGpuPower(180f, 30f);
                    SetCpuPower(85f, 15f);
                    SetAtxPower(25f, 5f);
                    break;

                case PowerProfile.BudgetGaming:
                    // RTX 3060 class: ~170W GPU, ~65W CPU
                    SetGpuPower(140f, 20f);
                    SetCpuPower(55f, 10f);
                    SetAtxPower(20f, 3f);
                    break;

                case PowerProfile.Workstation:
                    // Professional GPU + high-end CPU
                    SetGpuPower(250f, 40f);
                    SetCpuPower(180f, 30f);
                    SetAtxPower(35f, 5f);
                    break;

                case PowerProfile.Idle:
                    // System at idle
                    SetGpuPower(15f, 3f);
                    SetCpuPower(20f, 5f);
                    SetAtxPower(15f, 2f);
                    break;

                case PowerProfile.PowerVirus:
                    // Maximum stress test
                    SetGpuPower(450f, 30f);
                    SetCpuPower(200f, 20f);
                    SetAtxPower(40f, 5f);
                    break;

                case PowerProfile.TransientSpikes:
                    // Gaming with transient power spikes
                    SetGpuPower(280f, 100f);     // High variance for spikes
                    SetCpuPower(100f, 40f);
                    SetAtxPower(25f, 8f);
                    break;
            }

            // Set voltage base values (typically stable)
            SetVoltages();
        }

        private void SetGpuPower(float basePower, float variance)
        {
            // Distribute across GPU power channels
            var gpuPowerIndices = PoweneticsChannelExtensions.GPUPowerIndexGroup;
            float perChannelPower = basePower / gpuPowerIndices.Length;
            float perChannelVariance = variance / gpuPowerIndices.Length;

            foreach (var idx in gpuPowerIndices)
            {
                _channelBaseValues[idx] = perChannelPower;
                _channelVariances[idx] = perChannelVariance;
            }
        }

        private void SetCpuPower(float basePower, float variance)
        {
            var epsPowerIndices = PoweneticsChannelExtensions.EPSPowerIndexGroup;
            float perChannelPower = basePower / epsPowerIndices.Length;
            float perChannelVariance = variance / epsPowerIndices.Length;

            foreach (var idx in epsPowerIndices)
            {
                _channelBaseValues[idx] = perChannelPower;
                _channelVariances[idx] = perChannelVariance;
            }
        }

        private void SetAtxPower(float basePower, float variance)
        {
            var atxPowerIndices = PoweneticsChannelExtensions.ATXPowerIndexGroup;
            float perChannelPower = basePower / atxPowerIndices.Length;
            float perChannelVariance = variance / atxPowerIndices.Length;

            foreach (var idx in atxPowerIndices)
            {
                _channelBaseValues[idx] = perChannelPower;
                _channelVariances[idx] = perChannelVariance;
            }
        }

        private void SetVoltages()
        {
            // PCIe Slot 12V
            _channelBaseValues[0] = 12.1f; _channelVariances[0] = 0.1f;
            // PCIe Slot 3.3V
            _channelBaseValues[3] = 3.3f; _channelVariances[3] = 0.05f;

            // PCIe 12V (5 connectors)
            for (int i = 6; i <= 10; i++)
            {
                _channelBaseValues[i] = 12.0f;
                _channelVariances[i] = 0.1f;
            }

            // EPS 12V (3 connectors)
            for (int i = 21; i <= 23; i++)
            {
                _channelBaseValues[i] = 12.0f;
                _channelVariances[i] = 0.1f;
            }

            // ATX voltages
            _channelBaseValues[30] = 12.1f; _channelVariances[30] = 0.1f;  // ATX 12V
            _channelBaseValues[33] = 5.05f; _channelVariances[33] = 0.05f; // ATX 5V
            _channelBaseValues[36] = 3.32f; _channelVariances[36] = 0.03f; // ATX 3.3V
            _channelBaseValues[39] = 5.0f; _channelVariances[39] = 0.05f;  // ATX 5VSB
        }

        private PoweneticsChannel[] GenerateChannelData()
        {
            _currentTimestamp++;
            var channels = new PoweneticsChannel[CHANNEL_COUNT];
            var mapping = PoweneticsChannelExtensions.PmdChannelIndexMapping;

            for (int i = 0; i < CHANNEL_COUNT; i++)
            {
                var pos = mapping[i];

                // Generate value with Gaussian variance
                float value = GenerateValue(_channelBaseValues[i], _channelVariances[i]);

                // For current channels, calculate from power and voltage
                if (pos.Measurand == PoweneticsMeasurand.Current)
                {
                    // Find corresponding voltage and power to calculate current
                    value = CalculateCurrentFromPower(i);
                }

                channels[i] = new PoweneticsChannel
                {
                    Name = pos.Name,
                    PmdChannelType = pos.PmdChannelType,
                    Measurand = pos.Measurand,
                    Value = Math.Max(0, value),
                    TimeStamp = _currentTimestamp
                };
            }

            return channels;
        }

        private float GenerateValue(float baseValue, float variance)
        {
            if (variance == 0) return baseValue;

            // Box-Muller transform for Gaussian distribution
            double u1 = 1.0 - _random.NextDouble();
            double u2 = 1.0 - _random.NextDouble();
            double gaussian = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);

            return baseValue + (float)(gaussian * variance);
        }

        private float CalculateCurrentFromPower(int currentIndex)
        {
            // Use power dependency mapping to calculate current
            var powerDeps = PoweneticsChannelExtensions.PowerDependcyIndices;

            foreach (var kvp in powerDeps)
            {
                int powerIdx = kvp.Key;
                int[] deps = kvp.Value;

                if (deps.Length >= 2 && deps[1] == currentIndex)
                {
                    int voltageIdx = deps[0];
                    float voltage = _channelBaseValues[voltageIdx];
                    float power = GenerateValue(_channelBaseValues[powerIdx], _channelVariances[powerIdx]);

                    if (voltage > 0)
                    {
                        return power / voltage;
                    }
                }
            }

            return 0;
        }

        private void DistributePower(PoweneticsChannel[] channels, int[] powerIndices, float totalPower)
        {
            if (powerIndices == null || powerIndices.Length == 0) return;

            float perChannelPower = totalPower / powerIndices.Length;
            foreach (var idx in powerIndices)
            {
                if (idx < channels.Length)
                {
                    channels[idx].Value = perChannelPower + (float)(_random.NextDouble() - 0.5) * 5;
                }
            }
        }

        #endregion

        public void Dispose()
        {
            ShutDownDriver();
            _pmdChannelSubject?.Dispose();
            _pmdStatusSubject?.Dispose();
            _pmdThroughputSubject?.Dispose();
            _lostPacketsSubject?.Dispose();
        }
    }

    /// <summary>
    /// Predefined power profiles for realistic PMD simulation.
    /// </summary>
    public enum PowerProfile
    {
        /// <summary>
        /// High-end gaming: RTX 4090 + i9 class (~600W total).
        /// </summary>
        HighEndGaming,

        /// <summary>
        /// Mid-range gaming: RTX 3070 + Ryzen 7 class (~350W total).
        /// </summary>
        MidRangeGaming,

        /// <summary>
        /// Budget gaming: RTX 3060 + Ryzen 5 class (~250W total).
        /// </summary>
        BudgetGaming,

        /// <summary>
        /// Workstation: Professional GPU + high-core CPU (~500W total).
        /// </summary>
        Workstation,

        /// <summary>
        /// System at idle (~50W total).
        /// </summary>
        Idle,

        /// <summary>
        /// Maximum power stress test (~700W total).
        /// </summary>
        PowerVirus,

        /// <summary>
        /// Gaming with significant power transients/spikes.
        /// </summary>
        TransientSpikes
    }
}
