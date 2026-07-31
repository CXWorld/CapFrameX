using CapFrameX.Capture.Contracts;
using CapFrameX.Contracts.Configuration;
using CapFrameX.Contracts.Latency;
using CapFrameX.Contracts.Overlay;
using CapFrameX.EventAggregation.Messages;
using CapFrameX.PMD.Benchlab;
using CapFrameX.PMD.Powenetics;
using CapFrameX.Statistics.NetStandard;
using CapFrameX.Statistics.NetStandard.Contracts;
using Prism.Events;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Linq;

namespace CapFrameX.PresentMonInterface
{
    public class OnlineMetricService : IOnlineMetricService
    {
        private const int LIST_CAPACITY = 30000;
        private const int PMD_BUFFER_CAPACITY = 3000;
        private const double FIVE_SECONDS_INTERVAL_LENGTH = 5.0;
        private const double ANIMATION_ERROR_INTERVAL_LENGTH = 0.5;

        private readonly object _currentProcessLock = new object();

        private readonly IStatisticProvider _frametimeStatisticProvider;
        private readonly ICaptureService _captureService;
        private readonly IEventAggregator _eventAggregator;
        private readonly IOverlayEntryCore _overlayEntryCore;
        private readonly IPoweneticsService _poweneticsService;
        private readonly IBenchlabService _benchlabService;
        private readonly IAppConfiguration _appConfiguration;
        private readonly IAmdFlmService _amdFlmService;

        private readonly object _lockRealtimeMetric = new object();
        private readonly object _lock5SecondsMetric = new object();
        private readonly object _lock1SecondMetric = new object();
        private readonly object _lockAmdFlmMetric = new object();
        private readonly object _lockAnimationErrorMetric = new object();
        private readonly object _lockPmdMetrics = new object();

        // Circular buffers for realtime metrics (avoid RemoveRange memory shifting)
        private CircularBuffer<double> _frametimesRealtimeSeconds;
        private CircularBuffer<double> _displayedtimesRealtimeSeconds;
        private CircularBuffer<double> _gpuActiveTimesRealtimeSeconds;
        private CircularBuffer<double> _cpuActiveTimesRealtimeSeconds;
        private CircularBuffer<double> _measuretimesRealtimeSeconds;

        // Circular buffers for 5-second window metrics
        private CircularBuffer<double> _frametimes5Seconds;
        private CircularBuffer<double> _displaytimes5Seconds;
        private CircularBuffer<double> _measuretimes5Seconds;

        // Circular buffers for 1-second window metrics
        private CircularBuffer<double> _pcLatency1Second;
        private CircularBuffer<double> _measuretimes1Second;

        // FLM emits independently from PresentMon, so it uses its own QPC-based window.
        private CircularBuffer<double> _amdFlmLatency1Second;
        private CircularBuffer<double> _amdFlmMeasuretimes1Second;

        // Circular buffers for 250ms animation error window metrics
        private CircularBuffer<double> _animationError500Ms;
        private CircularBuffer<double> _measuretimes500Ms;

        // PMD buffers (kept as lists since they're cleared after consumption)
        private List<PoweneticsChannel[]> _channelDataBuffer = new List<PoweneticsChannel[]>(PMD_BUFFER_CAPACITY);
        private List<SensorSample> _sensorDataBuffer = new List<SensorSample>(PMD_BUFFER_CAPACITY);

        // Reusable list buffers to avoid allocations during metric calculations
        private List<double> _reusableListBufferA;
        private List<double> _reusableListBufferB;
        // The five-second path is protected by a different lock and therefore
        // must not share the realtime path's mutable scratch buffer.
        private List<double> _reusableListBuffer5Seconds;
        private List<double> _reusableAmdFlmBuffer;

        // Disposable resources
        private IDisposable _frameDataSubscription;
        private IDisposable _poweneticsSubscription;
        private IDisposable _benchlabSubscription;
        private IDisposable _amdFlmSubscription;
        private IDisposable _amdFlmConfigurationSubscription;
        private EventLoopScheduler _frameDataScheduler;
        private EventLoopScheduler _poweneticsScheduler;
        private EventLoopScheduler _benchlabScheduler;
        private EventLoopScheduler _amdFlmScheduler;
        private bool _disposed;

        private string _currentProcess;
        private int _currentProcessId;

        private int MetricInterval => _appConfiguration.MetricInterval == 0 ? 20 : _appConfiguration.MetricInterval;

        public OnlineMetricService(IStatisticProvider frametimeStatisticProvider,
            ICaptureService captureServive,
            IEventAggregator eventAggregator,
            IOverlayEntryCore oerlayEntryCore,
            IPoweneticsService poweneticsService,
            IBenchlabService benchlabService,
            IAppConfiguration appConfiguration,
            IAmdFlmService amdFlmService)
        {
            _captureService = captureServive;
            _eventAggregator = eventAggregator;
            _overlayEntryCore = oerlayEntryCore;
            _poweneticsService = poweneticsService;
            _benchlabService = benchlabService;
            _appConfiguration = appConfiguration;
            _amdFlmService = amdFlmService;

            _frametimeStatisticProvider = frametimeStatisticProvider;

            // Initialize reusable buffers
            _reusableListBufferA = new List<double>(LIST_CAPACITY);
            _reusableListBufferB = new List<double>(LIST_CAPACITY);
            _reusableListBuffer5Seconds = new List<double>(LIST_CAPACITY);
            _reusableAmdFlmBuffer = new List<double>(LIST_CAPACITY / 20);

            SubscribeToUpdateSession();
            ConnectOnlineMetricDataStream();
            ResetMetrics();
        }

        private void SubscribeToUpdateSession()
        {
            _eventAggregator
                .GetEvent<PubSubEvent<ViewMessages.CurrentProcessToCapture>>()
                .Subscribe(msg =>
                {
                    lock (_currentProcessLock)
                    {
                        if (_currentProcess != msg.Process)
                        {
                            ResetMetrics();
                        }

                        _currentProcess = msg.Process;
                        _currentProcessId = msg.ProcessId;
                    }
                });
        }

        private void ConnectOnlineMetricDataStream()
        {
            // Create schedulers that we can dispose later
            _frameDataScheduler = new EventLoopScheduler();
            _poweneticsScheduler = new EventLoopScheduler();
            _benchlabScheduler = new EventLoopScheduler();
            _amdFlmScheduler = new EventLoopScheduler();

            _frameDataSubscription = _captureService
                .FrameDataStream
                .Skip(1)
                .ObserveOn(_frameDataScheduler)
                .Where(x => EvaluateRealtimeMetrics())
                .Subscribe(UpdateOnlineMetrics);

            _poweneticsSubscription = _poweneticsService.PmdChannelStream
                .ObserveOn(_poweneticsScheduler)
                .Where(_ => EvaluatePmdMetrics())
                .Buffer(TimeSpan.FromMilliseconds(50))
                .Subscribe(metricsData => UpdatePmdMetrics(metricsData));

            _benchlabSubscription = _benchlabService.PmdSensorStream
                .ObserveOn(_benchlabScheduler)
                .Where(_ => EvaluatePmdMetrics())
                .Buffer(TimeSpan.FromMilliseconds(50))
                .Subscribe(metricsData => UpdatePmdMetrics(metricsData));

            _amdFlmSubscription = _amdFlmService.SampleStream
                .ObserveOn(_amdFlmScheduler)
                .Subscribe(UpdateAmdFlmMetric);

            _amdFlmConfigurationSubscription = _appConfiguration.OnValueChanged
                .Where(change => change.key == nameof(IAppConfiguration.UseAmdFlmLatency)
                    || change.key == nameof(IAppConfiguration.AmdFlmFrameGeneration))
                .ObserveOn(_amdFlmScheduler)
                .Subscribe(_ => ClearAmdFlmMetric());
        }

        private void UpdateAmdFlmMetric(AmdFlmSample sample)
        {
            if (sample.FrameQpc <= 0 || sample.LatencyMs <= 0 ||
                double.IsNaN(sample.LatencyMs) || double.IsInfinity(sample.LatencyMs))
                return;

            double sampleTime = sample.FrameQpc / (double)Stopwatch.Frequency;
            lock (_lockAmdFlmMetric)
            {
                if (_amdFlmMeasuretimes1Second == null || _amdFlmLatency1Second == null)
                    return;

                _amdFlmMeasuretimes1Second.Add(sampleTime);
                _amdFlmLatency1Second.Add(sample.LatencyMs);

                while (_amdFlmMeasuretimes1Second.Count > 0 &&
                    sampleTime - _amdFlmMeasuretimes1Second.PeekFirst() > 1.0)
                {
                    _amdFlmMeasuretimes1Second.RemoveFirst();
                    _amdFlmLatency1Second.RemoveFirst();
                }
            }
        }

        private void ClearAmdFlmMetric()
        {
            lock (_lockAmdFlmMetric)
            {
                _amdFlmLatency1Second?.Clear();
                _amdFlmMeasuretimes1Second?.Clear();
                _reusableAmdFlmBuffer?.Clear();
            }
        }

        private bool EvaluateRealtimeMetrics()
        {
            try
            {
                return (_overlayEntryCore.GetRealtimeMetricEntry("OnlineAverage")?.ShowOnOverlay ?? false)
                    || (_overlayEntryCore.GetRealtimeMetricEntry("OnlineP1")?.ShowOnOverlay ?? false)
                    || (_overlayEntryCore.GetRealtimeMetricEntry("OnlineP0dot1")?.ShowOnOverlay ?? false)
                    || (_overlayEntryCore.GetRealtimeMetricEntry("OnlineP0dot2")?.ShowOnOverlay ?? false)
                    || (_overlayEntryCore.GetRealtimeMetricEntry("Online1PercentLow")?.ShowOnOverlay ?? false)
                    || (_overlayEntryCore.GetRealtimeMetricEntry("Online0dot1PercentLow")?.ShowOnOverlay ?? false)
                    || (_overlayEntryCore.GetRealtimeMetricEntry("Online0dot2PercentLow")?.ShowOnOverlay ?? false)
                    || (_overlayEntryCore.GetRealtimeMetricEntry("OnlineStutteringPercentage")?.ShowOnOverlay ?? false)
                    || (_overlayEntryCore.GetRealtimeMetricEntry("OnlineGpuActiveTimeAverage")?.ShowOnOverlay ?? false)
                    || (_overlayEntryCore.GetRealtimeMetricEntry("OnlineCpuActiveTimeAverage")?.ShowOnOverlay ?? false)
                    || (_overlayEntryCore.GetRealtimeMetricEntry("OnlineFrameTimeAverage")?.ShowOnOverlay ?? false)
                    || (_overlayEntryCore.GetRealtimeMetricEntry("OnlineGpuActiveTimePercentageDeviation")?.ShowOnOverlay ?? false)
                    || (_overlayEntryCore.GetRealtimeMetricEntry("OnlinePcLatency")?.ShowOnOverlay ?? false)
                    || (_overlayEntryCore.GetRealtimeMetricEntry("OnlineAnimationError")?.ShowOnOverlay ?? false);
            }
            catch { return true; }
        }

        private bool EvaluatePmdMetrics()
        {
            try
            {
                return (_overlayEntryCore.GetRealtimeMetricEntry("PmdGpuPowerCurrent")?.ShowOnOverlay ?? false)
                    || (_overlayEntryCore.GetRealtimeMetricEntry("PmdCpuPowerCurrent")?.ShowOnOverlay ?? false)
                    || (_overlayEntryCore.GetRealtimeMetricEntry("PmdSystemPowerCurrent")?.ShowOnOverlay ?? false);

            }
            catch { return false; }
        }

        private void UpdateOnlineMetrics(string[] lineSplit)
        {
            string process;
            try
            {
                process = lineSplit[PresentMonCaptureService.ApplicationName_INDEX].Replace(".exe", "");
            }
            catch { return; }

            lock (_currentProcessLock)
            {
                if (process != _currentProcess)
                    return;
            }

            if (!int.TryParse(lineSplit[PresentMonCaptureService.ProcessID_INDEX], out int processId))
            {
                ResetMetrics();
                return;
            }

            lock (_currentProcessLock)
            {
                if (_currentProcessId != processId)
                    return;
            }

            // Get dynamic indices based on configuration
            int startTimeIndex = _captureService.CPUStartQPCTimeInMs_Index;
            int gpuBusyIndex = _captureService.GpuBusy_Index;
            int cpuBusyIndex = _captureService.CpuBusy_Index;

            if (!double.TryParse(lineSplit[startTimeIndex], NumberStyles.Any, CultureInfo.InvariantCulture, out double startTime))
            {
                ResetMetrics();
                return;
            }

            // Convert start time to seconds
            startTime *= 1E-03;


            if (!double.TryParse(lineSplit[PresentMonCaptureService.MsBetweenPresents_INDEX], NumberStyles.Any, CultureInfo.InvariantCulture, out double frameTime))
            {
                ResetMetrics();
                return;
            }

            double displayedTime = 0;
            if (_appConfiguration.UseDisplayChangeMetrics)
            {
                if (!double.TryParse(lineSplit[PresentMonCaptureService.MsBetweenDisplayChange_INDEX], NumberStyles.Any, CultureInfo.InvariantCulture, out displayedTime))
                {
                    // Don't reset metrics if display change time is not available
                    displayedTime = double.NaN;
                }
            }

            if (!double.TryParse(lineSplit[gpuBusyIndex], NumberStyles.Any, CultureInfo.InvariantCulture, out double gpuActiveTime))
            {
                ResetMetrics();
                return;
            }

            if (!double.TryParse(lineSplit[cpuBusyIndex], NumberStyles.Any, CultureInfo.InvariantCulture, out double cpuActiveTime))
            {
                ResetMetrics();
                return;
            }

            double pcLatency = double.NaN;
            if (_appConfiguration.UsePcLatency)
            {
                if (!double.TryParse(lineSplit[PresentMonCaptureService.MsPCLatency_INDEX], NumberStyles.Any, CultureInfo.InvariantCulture, out pcLatency))
                {
                    // Don't reset metrics if PC latency if not available
                    pcLatency = double.NaN;
                }
            }

            double animationError = double.NaN;
            int animationErrorIndex = _captureService.AnimationError_Index;
            if (animationErrorIndex >= 0 && animationErrorIndex < lineSplit.Length)
            {
                if (!double.TryParse(lineSplit[animationErrorIndex], NumberStyles.Any, CultureInfo.InvariantCulture, out animationError))
                {
                    // Don't reset metrics if animation error is not available
                    animationError = double.NaN;
                }
            }

            try
            {
                lock (_lockRealtimeMetric)
                {
                    // n seconds window - using circular buffer for O(1) add and efficient removal
                    _measuretimesRealtimeSeconds.Add(startTime);
                    _frametimesRealtimeSeconds.Add(frameTime);
                    _displayedtimesRealtimeSeconds.Add(displayedTime);
                    _gpuActiveTimesRealtimeSeconds.Add(gpuActiveTime);
                    _cpuActiveTimesRealtimeSeconds.Add(cpuActiveTime);

                    // Remove old entries that exceed the metric interval
                    if (_measuretimesRealtimeSeconds.Any() &&
                        startTime - _measuretimesRealtimeSeconds.PeekFirst() > MetricInterval)
                    {
                        while (_measuretimesRealtimeSeconds.Count > 0 &&
                            startTime - _measuretimesRealtimeSeconds.PeekFirst() > MetricInterval)
                        {
                            _measuretimesRealtimeSeconds.RemoveFirst();
                            _frametimesRealtimeSeconds.RemoveFirst();
                            _displayedtimesRealtimeSeconds.RemoveFirst();
                            _gpuActiveTimesRealtimeSeconds.RemoveFirst();
                            _cpuActiveTimesRealtimeSeconds.RemoveFirst(); ;
                        }
                    }
                }

                lock (_lock1SecondMetric)
                {
                    // 1 second window - using circular buffer for O(1) add and efficient removal
                    _measuretimes1Second.Add(startTime);
                    _pcLatency1Second.Add(pcLatency);

                    // Remove old entries that exceed the 1 second interval
                    if (_measuretimes1Second.Any() &&
                        startTime - _measuretimes1Second.PeekFirst() > 1.0)
                    {
                        while (_measuretimes1Second.Count > 0 &&
                            startTime - _measuretimes1Second.PeekFirst() > 1.0)
                        {
                            _measuretimes1Second.RemoveFirst();
                            _pcLatency1Second.RemoveFirst();
                        }
                    }
                }

                lock (_lock5SecondsMetric)
                {
                    // 5 seconds window - using circular buffer for O(1) add and efficient removal
                    _measuretimes5Seconds.Add(startTime);
                    _frametimes5Seconds.Add(frameTime);
                    _displaytimes5Seconds.Add(displayedTime);

                    // Remove old entries that exceed the 5 second interval
                    if (_measuretimes5Seconds.Any() &&
                        startTime - _measuretimes5Seconds.PeekFirst() > FIVE_SECONDS_INTERVAL_LENGTH)
                    {
                        while (_measuretimes5Seconds.Count > 0 &&
                            startTime - _measuretimes5Seconds.PeekFirst() > FIVE_SECONDS_INTERVAL_LENGTH)
                        {
                            _measuretimes5Seconds.RemoveFirst();
                            _frametimes5Seconds.RemoveFirst();
                            _displaytimes5Seconds.RemoveFirst();
                        }
                    }
                }

                lock (_lockAnimationErrorMetric)
                {
                    // 250ms window - using circular buffer for O(1) add and efficient removal
                    _measuretimes500Ms.Add(startTime);
                    _animationError500Ms.Add(animationError);

                    // Remove old entries that exceed the 250ms interval
                    if (_measuretimes500Ms.Any() &&
                        startTime - _measuretimes500Ms.PeekFirst() > ANIMATION_ERROR_INTERVAL_LENGTH)
                    {
                        while (_measuretimes500Ms.Count > 0 &&
                            startTime - _measuretimes500Ms.PeekFirst() > ANIMATION_ERROR_INTERVAL_LENGTH)
                        {
                            _measuretimes500Ms.RemoveFirst();
                            _animationError500Ms.RemoveFirst();
                        }
                    }
                }
            }
            catch { ResetMetrics(); }
        }

        private void UpdatePmdMetrics(IList<PoweneticsChannel[]> metricsData)
        {
            lock (_lockPmdMetrics)
            {
                // check for max capacity to avoid memory issues
                if (_channelDataBuffer.Count + metricsData.Count > PMD_BUFFER_CAPACITY)
                {
                    int itemsToRemove = (_channelDataBuffer.Count + metricsData.Count) - PMD_BUFFER_CAPACITY;
                    _channelDataBuffer.RemoveRange(0, itemsToRemove);
                }

                _channelDataBuffer.AddRange(metricsData);
            }
        }

        private void UpdatePmdMetrics(IList<SensorSample> metricsData)
        {
            lock (_lockPmdMetrics)
            {
                // check for max capacity to avoid memory issues
                if (_sensorDataBuffer.Count + metricsData.Count > PMD_BUFFER_CAPACITY)
                {
                    int itemsToRemove = (_sensorDataBuffer.Count + metricsData.Count) - PMD_BUFFER_CAPACITY;
                    _sensorDataBuffer.RemoveRange(0, itemsToRemove);
                }

                _sensorDataBuffer.AddRange(metricsData);
            }
        }

        private void ResetMetrics()
        {
            lock (_lockRealtimeMetric)
            {
                int capacity = (int)(LIST_CAPACITY * MetricInterval / 20d);

                _frametimesRealtimeSeconds = new CircularBuffer<double>(capacity);
                _displayedtimesRealtimeSeconds = new CircularBuffer<double>(capacity);
                _measuretimesRealtimeSeconds = new CircularBuffer<double>(capacity);
                _gpuActiveTimesRealtimeSeconds = new CircularBuffer<double>(capacity);
                _cpuActiveTimesRealtimeSeconds = new CircularBuffer<double>(capacity);
            }

            lock (_lock1SecondMetric)
            {
                int capacity1Second = LIST_CAPACITY / 20;
                _measuretimes1Second = new CircularBuffer<double>(capacity1Second);
                _pcLatency1Second = new CircularBuffer<double>(capacity1Second);
            }

            lock (_lockAmdFlmMetric)
            {
                int capacity1Second = LIST_CAPACITY / 20;
                _amdFlmMeasuretimes1Second = new CircularBuffer<double>(capacity1Second);
                _amdFlmLatency1Second = new CircularBuffer<double>(capacity1Second);
            }

            lock (_lock5SecondsMetric)
            {
                int capacity5Seconds = LIST_CAPACITY / 4;

                _frametimes5Seconds = new CircularBuffer<double>(capacity5Seconds);
                _displaytimes5Seconds = new CircularBuffer<double>(capacity5Seconds);
                _measuretimes5Seconds = new CircularBuffer<double>(capacity5Seconds);
            }

            lock (_lockAnimationErrorMetric)
            {
                // 500ms at high framerate (e.g., 500fps = 250 frames per 500ms)
                int capacity500Ms = 600;

                _animationError500Ms = new CircularBuffer<double>(capacity500Ms);
                _measuretimes500Ms = new CircularBuffer<double>(capacity500Ms);
            }
        }

        public double GetOnlineFpsMetricValue(EMetric metric)
        {
            lock (_lockRealtimeMetric)
            {
                // Use frame times when calculating average fps
                var useDisplayTimes = _appConfiguration.UseDisplayChangeMetrics && metric != EMetric.Average;
                var buffer = useDisplayTimes
                    ? _displayedtimesRealtimeSeconds : _frametimesRealtimeSeconds;

                if (buffer == null || buffer.Count == 0)
                    return double.NaN;

                var samples = CopyValidTimings(buffer, _reusableListBufferA);
                if (samples.Count == 0 && useDisplayTimes)
                    samples = CopyValidTimings(_frametimesRealtimeSeconds, _reusableListBufferA);

                if (samples.Count == 0)
                    return double.NaN;

                return _frametimeStatisticProvider
                    .GetFpsMetricValue(samples, metric);
            }
        }

        public double GetOnlineFrameTimeMetricValue(EMetric metric)
        {
            lock (_lockRealtimeMetric)
            {
                if (_frametimesRealtimeSeconds == null || _frametimesRealtimeSeconds.Count == 0)
                    return double.NaN;

                var samples = _frametimesRealtimeSeconds.ToList(_reusableListBufferA);

                return _frametimeStatisticProvider
                    .GetFrametimeMetricValue(samples, metric);
            }
        }

        public double GetOnlineGpuActiveTimeMetricValue(EMetric metric)
        {
            lock (_lockRealtimeMetric)
            {
                if (_gpuActiveTimesRealtimeSeconds == null || _gpuActiveTimesRealtimeSeconds.Count == 0)
                    return double.NaN;

                var samples = _gpuActiveTimesRealtimeSeconds.ToList(_reusableListBufferA);

                return _frametimeStatisticProvider
                    .GetFrametimeMetricValue(samples, metric);
            }
        }

        public double GetOnlineCpuActiveTimeMetricValue(EMetric metric)
        {
            lock (_lockRealtimeMetric)
            {
                if (_cpuActiveTimesRealtimeSeconds == null || _cpuActiveTimesRealtimeSeconds.Count == 0)
                    return double.NaN;

                var samples = _cpuActiveTimesRealtimeSeconds.ToList(_reusableListBufferA);

                return _frametimeStatisticProvider
                    .GetFrametimeMetricValue(samples, metric);
            }
        }

        public double GetOnlineGpuActiveTimeDeviationMetricValue()
        {
            lock (_lockRealtimeMetric)
            {
                if (_frametimesRealtimeSeconds == null || _frametimesRealtimeSeconds.Count == 0 ||
                    _gpuActiveTimesRealtimeSeconds == null || _gpuActiveTimesRealtimeSeconds.Count == 0)
                    return double.NaN;

                var frametimeSamples = _frametimesRealtimeSeconds.ToList(_reusableListBufferA);
                var gpuActiveSamples = _gpuActiveTimesRealtimeSeconds.ToList(_reusableListBufferB);

                var frameTimeAverage = _frametimeStatisticProvider
                    .GetFrametimeMetricValue(frametimeSamples, EMetric.Average);
                var gpuActiveTimeAverage = _frametimeStatisticProvider
                    .GetFrametimeMetricValue(gpuActiveSamples, EMetric.GpuActiveAverage);

                return Math.Round(Math.Abs((gpuActiveTimeAverage - frameTimeAverage) / frameTimeAverage * 100), MidpointRounding.AwayFromZero);
            }
        }

        public double GetOnlineStutteringPercentageValue()
        {
            lock (_lock5SecondsMetric)
            {
                var useDisplayTimes = _appConfiguration.UseDisplayChangeMetrics;
                var buffer = useDisplayTimes ? _displaytimes5Seconds : _frametimes5Seconds;

                if (buffer == null || buffer.Count == 0)
                    return double.NaN;

                var samples = CopyValidTimings(buffer, _reusableListBuffer5Seconds);
                if (samples.Count == 0 && useDisplayTimes)
                    samples = CopyValidTimings(_frametimes5Seconds, _reusableListBuffer5Seconds);

                if (samples.Count == 0)
                    return double.NaN;

                return _frametimeStatisticProvider
                    .GetOnlineStutteringTimePercentage(samples, _appConfiguration.StutteringFactor);
            }
        }

        private static List<double> CopyValidTimings(IEnumerable<double> source, List<double> target)
        {
            target.Clear();
            if (source == null)
                return target;

            foreach (double timing in source)
            {
                if (timing > 0 && !double.IsNaN(timing) && !double.IsInfinity(timing))
                    target.Add(timing);
            }

            return target;
        }

        public double GetOnlinePcLatencyAverageValue()
        {
            lock (_lock1SecondMetric)
            {
                // Return NaN if no valid pc latency samples are available
                if (_pcLatency1Second == null || _pcLatency1Second.Count == 0)
                    return double.NaN;

                var validSamples = _pcLatency1Second.Where(s => !double.IsNaN(s) && s > 0).ToList();

                // Allow 60% invalid values before returning NaN
                if (validSamples.Count < _pcLatency1Second.Count * 0.4)
                    return double.NaN;

                return _frametimeStatisticProvider
                    .GetFrametimeMetricValue(validSamples, EMetric.Average);
            }
        }

        public double GetOnlineAmdFlmLatencyAverageValue()
        {
            if (!_appConfiguration.UseAmdFlmLatency || !_amdFlmService.IsRunning)
                return double.NaN;

            lock (_lockAmdFlmMetric)
            {
                if (_amdFlmLatency1Second == null || _amdFlmLatency1Second.Count == 0)
                    return double.NaN;

                double now = Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency;
                if (_amdFlmMeasuretimes1Second == null ||
                    _amdFlmMeasuretimes1Second.Count == 0 ||
                    now - _amdFlmMeasuretimes1Second.PeekLast() > 1.0)
                {
                    _amdFlmLatency1Second.Clear();
                    _amdFlmMeasuretimes1Second?.Clear();
                    _reusableAmdFlmBuffer?.Clear();
                    return double.NaN;
                }

                var samples = CopyValidTimings(_amdFlmLatency1Second, _reusableAmdFlmBuffer);
                if (samples.Count == 0)
                    return double.NaN;

                return _frametimeStatisticProvider
                    .GetFrametimeMetricValue(samples, EMetric.Average);
            }
        }

        public double GetOnlineAnimationErrorValue()
        {
            lock (_lockAnimationErrorMetric)
            {
                // Return NaN if no valid animation error samples are available
                if (_animationError500Ms == null || _animationError500Ms.Count == 0)
                    return double.NaN;

                double maxAbsValue = 0d;
                double resultValue = double.NaN;

                foreach (var sample in _animationError500Ms)
                {
                    if (double.IsNaN(sample))
                        continue;

                    double absValue = Math.Abs(sample);
                    if (absValue >= maxAbsValue)
                    {
                        maxAbsValue = absValue;
                        resultValue = sample;
                    }
                }

                // Clamp to ±1000ms (1 second) to avoid extreme outliers
                if (!double.IsNaN(resultValue))
                {
                    resultValue = Math.Max(-1000d, Math.Min(1000d, resultValue));
                }

                return resultValue;
            }
        }

        public OnlinePmdMetrics GetPmdMetricsPowerCurrent()
        {
            OnlinePmdMetrics pmdMetrics;

            lock (_lockPmdMetrics)
            {
                if (_channelDataBuffer.Any())
                {
                    pmdMetrics = new OnlinePmdMetrics()
                    {
                        GpuPowerCurrent = GetPmdCurrentPowerByIndexGroup(_channelDataBuffer, PoweneticsChannelExtensions.GPUPowerIndexGroup),
                        CpuPowerCurrent = GetPmdCurrentPowerByIndexGroup(_channelDataBuffer, PoweneticsChannelExtensions.EPSPowerIndexGroup),
                        SystemPowerCurrent = GetPmdCurrentPowerByIndexGroup(_channelDataBuffer, PoweneticsChannelExtensions.SystemPowerIndexGroup),
                    };
                    _channelDataBuffer = new List<PoweneticsChannel[]>(PMD_BUFFER_CAPACITY);
                }
                else if (_sensorDataBuffer.Any())
                {
                    pmdMetrics = new OnlinePmdMetrics()
                    {
                        GpuPowerCurrent = GetPmdCurrentPowerByIndex(_sensorDataBuffer, _benchlabService.GpuPowerSensorIndex),
                        CpuPowerCurrent = GetPmdCurrentPowerByIndex(_sensorDataBuffer, _benchlabService.CpuPowerSensorIndex),
                        SystemPowerCurrent = GetPmdCurrentPowerByIndex(_sensorDataBuffer, _benchlabService.SytemPowerSensorIndex),
                    };
                    _sensorDataBuffer = new List<SensorSample>(PMD_BUFFER_CAPACITY);
                }
                else
                {
                    pmdMetrics = new OnlinePmdMetrics();
                }
            }

            return pmdMetrics;
        }

        private float GetPmdCurrentPowerByIndexGroup(IList<PoweneticsChannel[]> channelData, int[] indexGroup)
        {
            double sum = 0;

            foreach (var channel in channelData)
            {
                var currentChannlesSumPower = indexGroup.Sum(index => channel[index].Value);
                sum += currentChannlesSumPower;
            }

            return (float)(sum / channelData.Count);
        }

        private float GetPmdCurrentPowerByIndex(IList<SensorSample> sensorData, int index)
        {
            double sum = 0;
            foreach (var sample in sensorData)
            {
                var currentChannlesSumPower = sample.Sensors[index].Value;
                sum += currentChannlesSumPower;
            }
            return (float)(sum / sensorData.Count);
        }

        public void ResetRealtimeMetrics() => ResetMetrics();

        public void SetMetricInterval() => ResetMetrics();

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            if (disposing)
            {
                // Dispose subscriptions
                _frameDataSubscription?.Dispose();
                _poweneticsSubscription?.Dispose();
                _benchlabSubscription?.Dispose();
                _amdFlmSubscription?.Dispose();
                _amdFlmConfigurationSubscription?.Dispose();

                // Dispose schedulers (EventLoopScheduler implements IDisposable)
                _frameDataScheduler?.Dispose();
                _poweneticsScheduler?.Dispose();
                _benchlabScheduler?.Dispose();
                _amdFlmScheduler?.Dispose();

                // Clear buffers
                lock (_lockRealtimeMetric)
                {
                    _frametimesRealtimeSeconds?.Clear();
                    _displayedtimesRealtimeSeconds?.Clear();
                    _gpuActiveTimesRealtimeSeconds?.Clear();
                    _cpuActiveTimesRealtimeSeconds?.Clear();
                    _measuretimesRealtimeSeconds?.Clear();
                    _reusableListBufferA?.Clear();
                    _reusableListBufferB?.Clear();
                }

                lock (_lock5SecondsMetric)
                {
                    _frametimes5Seconds?.Clear();
                    _displaytimes5Seconds?.Clear();
                    _measuretimes5Seconds?.Clear();
                    _reusableListBuffer5Seconds?.Clear();
                }

                lock (_lock1SecondMetric)
                {
                    _pcLatency1Second?.Clear();
                    _measuretimes1Second?.Clear();
                }

                lock (_lockAmdFlmMetric)
                {
                    _amdFlmLatency1Second?.Clear();
                    _amdFlmMeasuretimes1Second?.Clear();
                    _reusableAmdFlmBuffer?.Clear();
                }

                lock (_lockAnimationErrorMetric)
                {
                    _animationError500Ms?.Clear();
                    _measuretimes500Ms?.Clear();
                }

                lock (_lockPmdMetrics)
                {
                    _channelDataBuffer?.Clear();
                    _sensorDataBuffer?.Clear();
                }
            }

            _disposed = true;
        }
    }
}
