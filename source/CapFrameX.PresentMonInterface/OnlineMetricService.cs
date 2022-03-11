using CapFrameX.Contracts.Configuration;
using CapFrameX.Contracts.Overlay;
using CapFrameX.Contracts.PresentMonInterface;
using CapFrameX.EventAggregation.Messages;
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

        private readonly object _currentProcessLock = new object();

        private readonly IStatisticProvider _frametimeStatisticProvider;
        private readonly ICaptureService _captureService;
        private readonly IEventAggregator _eventAggregator;
        private readonly IOverlayEntryCore _overlayEntryCore;
        private readonly ILogger<OnlineMetricService> _logger;
        private readonly IAppConfiguration _appConfiguration;
        private readonly object _lockMetric = new object();
        private readonly object _lockApplicationLatency = new object();
        private List<double> _frametimes = new List<double>(LIST_CAPACITY);
        private List<double> _measuretimesMetrics = new List<double>(LIST_CAPACITY);
        private List<double> _measuretimesApplicationLatency = new List<double>(LIST_CAPACITY / 10);
        private List<double> _applicationLatencyValues = new List<double>(LIST_CAPACITY / 10);
        private string _currentProcess;
        private int _currentProcessId;
        private double _droppedFrametimes = 0.0;
        private double _prevDisplayedFrameInputLagTime = double.NaN;
        private readonly double _maxOnlineMetricIntervalLength = 20d;
        private readonly double _maxOnlineApplicationLatencyIntervalLength = 2d;

        public OnlineMetricService(IStatisticProvider frametimeStatisticProvider,
            ICaptureService captureServive,
            IEventAggregator eventAggregator,
            IOverlayEntryCore oerlayEntryCore,
            ILogger<OnlineMetricService> logger,
            IAppConfiguration appConfiguration)
        {
            _captureService = captureServive;
            _eventAggregator = eventAggregator;
            _overlayEntryCore = oerlayEntryCore;
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
                .RedirectedOutputDataStream
                .Skip(1)
                .ObserveOn(new EventLoopScheduler())
                .Where(x => EvaluateRealtimeMetrics())
                .Subscribe(UpdateOnlineMetrics);

            _captureService
                .RedirectedOutputDataStream
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
        }

        private bool EvaluateRealtimeMetrics()
        {
            try
            {
                return _overlayEntryCore.RealtimeMetricEntryDict["OnlineAverage"].ShowOnOverlay
                    || _overlayEntryCore.RealtimeMetricEntryDict["OnlineP1"].ShowOnOverlay
                    || _overlayEntryCore.RealtimeMetricEntryDict["OnlineP0dot2"].ShowOnOverlay
                    || _overlayEntryCore.RealtimeMetricEntryDict["OnlineStutteringPercentage"].ShowOnOverlay;
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
            if (frameTime > STUTTERING_THRESHOLD * 1E03) return;

            try
            {
                lock (_lockMetric)
                {
                    _measuretimesMetrics.Add(startTime);
                    _frametimes.Add(frameTime);

                    if (startTime - _measuretimesMetrics.First() > _maxOnlineMetricIntervalLength)
                    {
                        int position = 0;
                        while (position < _measuretimesMetrics.Count &&
                            startTime - _measuretimesMetrics[position] > _maxOnlineMetricIntervalLength)
                            position++;

                        if (position > 0)
                        {
                            _frametimes.RemoveRange(0, position);
                            _measuretimesMetrics.RemoveRange(0, position);
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

        private void ResetMetrics()
        {
            lock (_lockMetric)
            {
                _frametimes = new List<double>(LIST_CAPACITY);
                _measuretimesMetrics = new List<double>(LIST_CAPACITY);
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
            lock (_lockMetric)
            {
                return _frametimeStatisticProvider
                    .GetFpsMetricValue(_frametimes, metric);
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
            lock (_lockMetric)
            {
                return _frametimeStatisticProvider
                    .GetStutteringTimePercentage(_frametimes, _appConfiguration.StutteringFactor);
            }
        }
    }
}
