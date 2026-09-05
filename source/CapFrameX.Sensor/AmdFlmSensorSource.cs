using CapFrameX.Contracts.Configuration;
using CapFrameX.Contracts.Latency;
using CapFrameX.Contracts.Sensor;
using LibreHardwareMonitor.Hardware;
using System;
using System.Diagnostics;
using System.Reactive.Disposables;
using System.Reactive.Linq;

namespace CapFrameX.Sensor
{
    internal sealed class AmdFlmSensorSource : IDisposable
    {
        private static readonly long MaxSampleAgeTicks = Stopwatch.Frequency * 2;

        private readonly IAmdFlmService _amdFlmService;
        private readonly IAppConfiguration _appConfiguration;
        private readonly IDisposable _sampleSubscription;
        private readonly IDisposable _configurationSubscription;
        private readonly object _sampleLock = new object();

        private AmdFlmSample? _latestSample;

        internal AmdFlmSensorSource(
            IAmdFlmService amdFlmService,
            IAppConfiguration appConfiguration)
        {
            _amdFlmService = amdFlmService;
            _appConfiguration = appConfiguration;
            _sampleSubscription = _amdFlmService.SampleStream.Subscribe(UpdateSample);

            var configurationChanges = _appConfiguration.OnValueChanged;
            _configurationSubscription = configurationChanges == null
                ? Disposable.Empty
                : configurationChanges
                    .Where(change => AmdFlmSettings.IsConfigurationKey(change.key))
                    .Subscribe(_ => ClearSample());
        }

        internal ISensorEntry CreateEntry()
        {
            float value = GetCurrentValue();
            return new SensorEntry
            {
                Identifier = AmdFlmSensorMetadata.Identifier,
                SortKey = "0_99_0",
                Value = value,
                Name = AmdFlmSensorMetadata.Name,
                SensorType = SensorType.Latency.ToString(),
                HardwareType = HardwareType.GpuAmd.ToString(),
                HardwareName = AmdFlmSensorMetadata.HardwareName,
                IsPresentationDefault = false
            };
        }

        internal float GetCurrentValue()
        {
            if (!_appConfiguration.UseAmdFlmLatency || !_amdFlmService.IsRunning)
                return float.NaN;

            AmdFlmSample? sample;
            lock (_sampleLock)
                sample = _latestSample;

            if (!sample.HasValue)
                return float.NaN;

            long age = Stopwatch.GetTimestamp() - sample.Value.FrameQpc;
            if (age < 0 || age > MaxSampleAgeTicks)
                return float.NaN;

            return (float)sample.Value.LatencyMs;
        }

        private void UpdateSample(AmdFlmSample sample)
        {
            if (sample.FrameQpc <= 0 || sample.LatencyMs <= 0
                || double.IsNaN(sample.LatencyMs)
                || double.IsInfinity(sample.LatencyMs)
                || sample.LatencyMs > float.MaxValue)
            {
                return;
            }

            lock (_sampleLock)
                _latestSample = sample;
        }

        private void ClearSample()
        {
            lock (_sampleLock)
                _latestSample = null;
        }

        public void Dispose()
        {
            _configurationSubscription.Dispose();
            _sampleSubscription.Dispose();
        }
    }
}
