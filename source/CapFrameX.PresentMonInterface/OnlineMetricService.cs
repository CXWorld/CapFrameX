using CapFrameX.Capture.Contracts;
using CapFrameX.Contracts.Configuration;
using CapFrameX.Contracts.Overlay;
using CapFrameX.EventAggregation.Messages;
using CapFrameX.PMD;
using CapFrameX.Statistics.NetStandard.Contracts;
using Microsoft.Extensions.Logging;
using Prism.Events;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Linq;

namespace CapFrameX.PresentMonInterface
{
    public class OnlineMetricService : IOnlineMetricService
    {
        private const double STUTTERING_THRESHOLD = 2d;
        private const int LIST_CAPACITY = 20000;
        private const int PMD_BUFFER_CAPACITY = 2200;

        private readonly object _currentProcessLock = new object();

        private readonly IStatisticProvider _frametimeStatisticProvider;
        private readonly ICaptureService _captureService;
        private readonly IEventAggregator _eventAggregator;
        private readonly IOverlayEntryCore _overlayEntryCore;
        private readonly IPmdService _pmdService;
        private readonly ILogger<OnlineMetricService> _logger;
        private readonly IAppConfiguration _appConfiguration;
        private readonly object _lock20SecondsMetric = new object();
        private readonly object _lock5SecondsMetric = new object();
        private readonly object _lockApplicationLatency = new object();
        private readonly object _lockPmdMetrics = new object();
        private List<double> _frametimes20Seconds = new List<double>(LIST_CAPACITY);
        private List<double> _frametimes5Seconds = new List<double>(LIST_CAPACITY / 4);
        private List<double> _measuretimes20Seconds = new List<double>(LIST_CAPACITY);
        private List<double> _measuretimes5Seconds = new List<double>(LIST_CAPACITY / 4);
        private List<double> _measuretimesApplicationLatency = new List<double>(LIST_CAPACITY / 10);
        private List<double> _applicationLatencyValues = new List<double>(LIST_CAPACITY / 10);
        private List<PmdChannel[]> _channelDataBuffer = new List<PmdChannel[]>(PMD_BUFFER_CAPACITY);
        private string _currentProcess;
        private int _currentProcessId;
        private double _droppedFrametimes = 0.0;
        private double _prevDisplayedFrameInputLagTime = double.NaN;
        private readonly double _maxOnlineMetricIntervalLength = 20d;
        private readonly double _maxOnlineStutteringIntervalLength = 5d;
        private readonly double _maxOnlineApplicationLatencyIntervalLength = 2d;

        public OnlineMetricService(IStatisticProvider frametimeStatisticProvider,
            ICaptureService captureServive,
            IEventAggregator eventAggregator,
            IOverlayEntryCore oerlayEntryCore,
            IPmdService pmdService,
            ILogger<OnlineMetricService> logger,
            IAppConfiguration appConfiguration)
        {
            _captureService = captureServive;
            _eventAggregator = eventAggregator;
            _overlayEntryCore = oerlayEntryCore;
            _pmdService = pmdService;
            _logger = logger;
            _appConfiguration = appConfiguration;

            _frametimeStatisticProvider = frametimeStatisticProvider;

            SubscribeToUpdateSession();
            ConnectOnlineMetricDataStream();
        }

        private void SubscribeToUpdateSession()
        {
            _eventAggregator
                .GetEvent<PubSubEvent<ViewMessages.CurrentProcessToCapture>>()
                .Subscribe(msg =>
                {
                    lock (_currentProcessLock)
                    {
                        if (_currentProcess == null
                        || _currentProcess != msg.Process)
                        {
                            ResetMetrics();
                            ResetApplicationLatency();
                        }

                        _currentProcess = msg.Process;
                        _currentProcessId = msg.ProcessId;
                    }
                });
        }

        private void ConnectOnlineMetricDataStream()
        {
            _captureService
                .FrameDataStream
                .Skip(1)
                .ObserveOn(new EventLoopScheduler())
                .Where(x => EvaluateRealtimeMetrics())
                .Subscribe(UpdateOnlineMetrics);

            _captureService
                .FrameDataStream
                .Skip(1)
                .ObserveOn(new EventLoopScheduler())
                .Where(line => EvaluateRealtimeInputLag())
                .Scan(new List<string[]>(), (acc, current) =>
                {
                    if (acc.Count > 1)
                    {
                        acc.RemoveAt(0);
                    }
                    acc.Add(current);
                    return acc;
                })
                .Where(acc => acc.Count == 2)
                .Subscribe(UpdateOnlineApplicationLatency);

            _pmdService.PmdChannelStream
                .ObserveOn(new EventLoopScheduler())
                .Where(_ => EvaluatePmdMetrics())
                .Buffer(TimeSpan.FromMilliseconds(50))
                .Subscribe(metricsData => UpdatePmdMetrics(metricsData));
        }

        private bool EvaluateRealtimeMetrics()
        {
            try
            {
                return _overlayEntryCore.RealtimeMetricEntryDict["OnlineAverage"].ShowOnOverlay
                    || _overlayEntryCore.RealtimeMetricEntryDict["OnlineP1"].ShowOnOverlay
                    || _overlayEntryCore.RealtimeMetricEntryDict["OnlineP0dot2"].ShowOnOverlay
                    || _overlayEntryCore.RealtimeMetricEntryDict["OnlineStutteringPercentage"].ShowOnOverlay
                    || _overlayEntryCore.RealtimeMetricEntryDict["PmdGpuPowerCurrent"].ShowOnOverlay
                    || _overlayEntryCore.RealtimeMetricEntryDict["PmdCpuPowerCurrent"].ShowOnOverlay
                    || _overlayEntryCore.RealtimeMetricEntryDict["PmdSystemPowerCurrent"].ShowOnOverlay;
            }
            catch { return false; }
        }

        private bool EvaluateRealtimeInputLag()
        {
            try
            {
                return _overlayEntryCore.RealtimeMetricEntryDict["OnlineApplicationLatency"].ShowOnOverlay;
            }
            catch { return false; }
        }

        private bool EvaluatePmdMetrics()
        {
            try
            {
                return _overlayEntryCore.RealtimeMetricEntryDict["PmdGpuPowerCurrent"].ShowOnOverlay
                    || _overlayEntryCore.RealtimeMetricEntryDict["PmdCpuPowerCurrent"].ShowOnOverlay
                    || _overlayEntryCore.RealtimeMetricEntryDict["PmdSystemPowerCurrent"].ShowOnOverlay;
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

            if (!double.TryParse(lineSplit[PresentMonCaptureService.TimeInSeconds_INDEX], NumberStyles.Any, CultureInfo.InvariantCulture, out double startTime))
            {
                ResetMetrics();
                return;
            }

            if (!double.TryParse(lineSplit[PresentMonCaptureService.MsBetweenPresents_INDEX], NumberStyles.Any, CultureInfo.InvariantCulture, out double frameTime))
            {
                ResetMetrics();
                return;
            }

            // it makes no sense to calculate fps metrics with
            // frame times above the stuttering threshold
            // filtering high frame times caused by focus lost for example
            if (frameTime > STUTTERING_THRESHOLD * 1E03 && !_overlayEntryCore.RealtimeMetricEntryDict["OnlineStutteringPercentage"].ShowOnOverlay)
                return;

            try
            {
                lock (_lock20SecondsMetric)
                {
                    // 20 sceconds window
                    _measuretimes20Seconds.Add(startTime);
                    _frametimes20Seconds.Add(frameTime);

                    if (startTime - _measuretimes20Seconds.First() > _maxOnlineMetricIntervalLength)
                    {
                        int position = 0;
                        while (position < _measuretimes20Seconds.Count &&
                            startTime - _measuretimes20Seconds[position] > _maxOnlineMetricIntervalLength)
                            position++;

                        if (position > 0)
                        {
                            _frametimes20Seconds.RemoveRange(0, position);
                            _measuretimes20Seconds.RemoveRange(0, position);
                        }
                    }
                }

                lock (_lock5SecondsMetric)
                {
                    // 5 sceconds window
                    _measuretimes5Seconds.Add(startTime);
                    _frametimes5Seconds.Add(frameTime);

                    if (startTime - _measuretimes5Seconds.First() > _maxOnlineStutteringIntervalLength)
                    {
                        int position = 0;
                        while (position < _measuretimes5Seconds.Count &&
                            startTime - _measuretimes5Seconds[position] > _maxOnlineStutteringIntervalLength)
                            position++;

                        if (position > 0)
                        {
                            _frametimes5Seconds.RemoveRange(0, position);
                            _measuretimes5Seconds.RemoveRange(0, position);
                        }
                    }
                }
            }
            catch { ResetMetrics(); }
        }

        private void UpdateOnlineApplicationLatency(List<string[]> lineSplits)
        {
            string process;
            try
            {
                process = lineSplits[1][PresentMonCaptureService.ApplicationName_INDEX].Replace(".exe", "");
            }
            catch { return; }

            lock (_currentProcessLock)
            {
                if (process != _currentProcess)
                    return;
            }

            if (!int.TryParse(lineSplits[1][PresentMonCaptureService.ProcessID_INDEX], out int processId))
            {
                ResetApplicationLatency();
                return;
            }

            lock (_currentProcessLock)
            {
                if (_currentProcessId != processId)
                    return;
            }

            if (!double.TryParse(lineSplits[1][PresentMonCaptureService.TimeInSeconds_INDEX], NumberStyles.Any, CultureInfo.InvariantCulture, out double startTime))
            {
                ResetApplicationLatency();
                return;
            }

            if (!double.TryParse(lineSplits[1][PresentMonCaptureService.MsBetweenPresents_INDEX], NumberStyles.Any, CultureInfo.InvariantCulture, out double frameTime))
            {
                ResetApplicationLatency();
                return;
            }

            // it makes no sense to calculate fps metrics with
            // frame times above the stuttering threshold
            // filtering high frame times caused by focus lost for example
            if (frameTime > STUTTERING_THRESHOLD * 1E03) return;

            try
            {
                var frameTime_a = Convert.ToDouble(lineSplits[1][PresentMonCaptureService.MsBetweenPresents_INDEX], CultureInfo.InvariantCulture);
                var untilDisplayedTimes_a = Convert.ToDouble(lineSplits[1][PresentMonCaptureService.UntilDisplayedTimes_INDEX], CultureInfo.InvariantCulture);
                var inPresentAPITimes_b = Convert.ToDouble(lineSplits[0][PresentMonCaptureService.MsInPresentAPI_INDEX], CultureInfo.InvariantCulture);
                var appMissed_a = Convert.ToInt32(lineSplits[1][PresentMonCaptureService.Dropped_INDEX], CultureInfo.InvariantCulture) == 1;

                lock (_lockApplicationLatency)
                {
                    _measuretimesApplicationLatency.Add(startTime);

                    if (appMissed_a)
                        _droppedFrametimes += frameTime_a;
                    else
                    {
                        _applicationLatencyValues.Add(0.5 * frameTime_a + untilDisplayedTimes_a + 0.5 * (_prevDisplayedFrameInputLagTime + _droppedFrametimes));
                        _droppedFrametimes = 0.0;
                        _prevDisplayedFrameInputLagTime = frameTime_a - inPresentAPITimes_b;
                    }

                    if (startTime - _measuretimesApplicationLatency.First() > _maxOnlineApplicationLatencyIntervalLength)
                    {
                        int position = 0;
                        while (position < _measuretimesApplicationLatency.Count &&
                            startTime - _measuretimesApplicationLatency[position] > _maxOnlineApplicationLatencyIntervalLength)
                            position++;

                        if (position > 0)
                        {
                            _applicationLatencyValues.RemoveRange(0, position);
                            _measuretimesApplicationLatency.RemoveRange(0, position);
                        }
                    }
                }
            }
            catch { ResetApplicationLatency(); }
        }

        private void UpdatePmdMetrics(IList<PmdChannel[]> metricsData)
        {
            lock (_lockPmdMetrics)
            {
                _channelDataBuffer.AddRange(metricsData);
            }
        }

        private void ResetMetrics()
        {
            lock (_lock20SecondsMetric)
            {
                _frametimes20Seconds = new List<double>(LIST_CAPACITY);
                _measuretimes20Seconds = new List<double>(LIST_CAPACITY);
            }
        }

        private void ResetApplicationLatency()
        {
            lock (_lockApplicationLatency)
            {
                _applicationLatencyValues = new List<double>(LIST_CAPACITY / 10);
                _measuretimesApplicationLatency = new List<double>(LIST_CAPACITY / 10);
            }
        }

        public double GetOnlineFpsMetricValue(EMetric metric)
        {
            lock (_lock20SecondsMetric)
            {
                return _frametimeStatisticProvider
                    .GetFpsMetricValue(_frametimes20Seconds, metric);
            }
        }

        public double GetOnlineApplicationLatencyValue()
        {
            lock (_lockApplicationLatency)
            {
                if (!_applicationLatencyValues.Any())
                    return 0;

                return _applicationLatencyValues.Average();
            }
        }

        public double GetOnlineStutteringPercentageValue()
        {
            lock (_lock5SecondsMetric)
            {
                if (!_frametimes5Seconds.Any())
                    return 0;

                return _frametimeStatisticProvider
                    .GetOnlineStutteringTimePercentage(_frametimes5Seconds, _appConfiguration.StutteringFactor);
            }
        }

        public OnlinePmdMetrics GetPmdMetricsPowerCurrent()
        {
            OnlinePmdMetrics pmdMetrics;

            lock (_lockPmdMetrics)
            {
                pmdMetrics = new OnlinePmdMetrics()
                {
                    GpuPowerCurrent = GetPmdCurrentPowerByIndexGroup(_channelDataBuffer, PmdChannelExtensions.GPUPowerIndexGroup),
                    CpuPowerCurrent = GetPmdCurrentPowerByIndexGroup(_channelDataBuffer, PmdChannelExtensions.EPSPowerIndexGroup),
                    SystemPowerCurrent = GetPmdCurrentPowerByIndexGroup(_channelDataBuffer, PmdChannelExtensions.SystemPowerIndexGroup),
                };

                _channelDataBuffer = new List<PmdChannel[]>(PMD_BUFFER_CAPACITY);
            }

            return pmdMetrics;
        }

        private float GetPmdCurrentPowerByIndexGroup(IList<PmdChannel[]> channelData, int[] indexGroup)
        {
            double sum = 0;

            foreach (var channel in channelData)
            {
                var currentChannlesSumPower = indexGroup.Sum(index => channel[index].Value);
                sum += currentChannlesSumPower;
            }

            return (float)(sum / channelData.Count);
        }
    }
}
