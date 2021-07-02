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
        private readonly object _lockMetric = new object();
        private readonly object _lockRenderLag = new object();
        private List<double> _frametimes = new List<double>(LIST_CAPACITY);
        private List<double> _measuretimesMetrics = new List<double>(LIST_CAPACITY);
        private List<double> _measuretimesRenderLag = new List<double>(LIST_CAPACITY / 10);
        private List<double> _renderLagValues = new List<double>(LIST_CAPACITY / 10);
        private string _currentProcess;
        private int _currentProcessId;
        private readonly double _maxOnlineMetricIntervalLength = 20d;
        private readonly double _maxOnlineRenderLagIntervalLength = 2d;

        public OnlineMetricService(IStatisticProvider frametimeStatisticProvider,
            ICaptureService captureServive,
            IEventAggregator eventAggregator,
            IOverlayEntryCore oerlayEntryCore,
            ILogger<OnlineMetricService> logger)
        {
            _captureService = captureServive;
            _eventAggregator = eventAggregator;
            _overlayEntryCore = oerlayEntryCore;
            _logger = logger;

            _frametimeStatisticProvider = frametimeStatisticProvider;

            SubscribeToUpdateSession();
            ConnectOnlineMetricDataStream();
        }

        private void SubscribeToUpdateSession()
        {
            _eventAggregator.GetEvent<PubSubEvent<ViewMessages.CurrentProcessToCapture>>()
                            .Subscribe(msg =>
                            {
                                lock (_currentProcessLock)
                                {
                                    if (_currentProcess == null
                                    || _currentProcess != msg.Process)
                                    {
                                        ResetMetrics();
                                        ResetRenderLag();
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
                .Where(line => IsNotDropped(line))
                .Scan(new List<string[]>(), (acc, current) =>
                {
                    if (acc.Count > 2)
                    {
                        acc.RemoveAt(0);
                    }
                    acc.Add(current);
                    return acc;
                })
                .Where(acc => acc.Count == 3)
                .Subscribe(UpdateOnlineRenderLag);
        }

        private bool IsNotDropped(string[] line)
        {
            try
            {
                return Convert.ToInt32(line[PresentMonCaptureService.Dropped_INDEX], CultureInfo.InvariantCulture) == 0;
            }
            catch { return false; }
        }

        private bool EvaluateRealtimeMetrics()
        {
            try
            {
                return _overlayEntryCore.RealtimeMetricEntryDict["OnlineAverage"].ShowOnOverlay
                    || _overlayEntryCore.RealtimeMetricEntryDict["OnlineP1"].ShowOnOverlay
                    || _overlayEntryCore.RealtimeMetricEntryDict["OnlineP0dot2"].ShowOnOverlay;
            }
            catch { return false; }
        }

        private bool EvaluateRealtimeInputLag()
        {
            try
            {
                return _overlayEntryCore.RealtimeMetricEntryDict["OnlineRenderLag"].ShowOnOverlay;
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

        private void UpdateOnlineRenderLag(List<string[]> lineSplits)
        {
            string process;
            try
            {
                process = lineSplits[2][PresentMonCaptureService.ApplicationName_INDEX].Replace(".exe", "");
            }
            catch { return; }

            lock (_currentProcessLock)
            {
                if (process != _currentProcess)
                    return;
            }

            if (!int.TryParse(lineSplits[2][PresentMonCaptureService.ProcessID_INDEX], out int processId))
            {
                ResetRenderLag();
                return;
            }

            lock (_currentProcessLock)
            {
                if (_currentProcessId != processId)
                    return;
            }

            if (!double.TryParse(lineSplits[2][PresentMonCaptureService.TimeInSeconds_INDEX], NumberStyles.Any, CultureInfo.InvariantCulture, out double startTime))
            {
                ResetRenderLag();
                return;
            }

            if (!double.TryParse(lineSplits[2][PresentMonCaptureService.MsBetweenPresents_INDEX], NumberStyles.Any, CultureInfo.InvariantCulture, out double frameTime))
            {
                ResetRenderLag();
                return;
            }

            // it makes no sense to calculate fps metrics with
            // frame times above the stuttering threshold
            // filtering high frame times caused by focus lost for example
            if (frameTime > STUTTERING_THRESHOLD * 1E03) return;

            try
            {
                var frameTime_a = Convert.ToDouble(lineSplits[2][PresentMonCaptureService.MsBetweenPresents_INDEX], CultureInfo.InvariantCulture);
                var frameTime_b = Convert.ToDouble(lineSplits[1][PresentMonCaptureService.MsBetweenPresents_INDEX], CultureInfo.InvariantCulture);
                var untilDisplayedTimes_a = Convert.ToDouble(lineSplits[2][PresentMonCaptureService.UntilDisplayedTimes_INDEX], CultureInfo.InvariantCulture);
                var inPresentAPITimes_b = Convert.ToDouble(lineSplits[1][PresentMonCaptureService.MsInPresentAPI_INDEX], CultureInfo.InvariantCulture);
                var inPresentAPITimes_c = Convert.ToDouble(lineSplits[0][PresentMonCaptureService.MsInPresentAPI_INDEX], CultureInfo.InvariantCulture);

                lock (_lockRenderLag)
                {
                    _measuretimesRenderLag.Add(startTime);
                    _renderLagValues.Add(frameTime_a + untilDisplayedTimes_a + 0.5 * (frameTime_b - inPresentAPITimes_b - inPresentAPITimes_c));

                    if (startTime - _measuretimesRenderLag.First() > _maxOnlineRenderLagIntervalLength)
                    {
                        int position = 0;
                        while (position < _measuretimesRenderLag.Count &&
                            startTime - _measuretimesRenderLag[position] > _maxOnlineRenderLagIntervalLength)
                            position++;

                        if (position > 0)
                        {
                            _renderLagValues.RemoveRange(0, position);
                            _measuretimesRenderLag.RemoveRange(0, position);
                        }
                    }
                }
            }
            catch { ResetRenderLag(); }
        }

        private void ResetMetrics()
        {
            lock (_lockMetric)
            {
                _frametimes = new List<double>(LIST_CAPACITY);
                _measuretimesMetrics = new List<double>(LIST_CAPACITY);
            }
        }

        private void ResetRenderLag()
        {
            lock (_lockRenderLag)
            {
                _renderLagValues = new List<double>(LIST_CAPACITY / 10);
                _measuretimesRenderLag = new List<double>(LIST_CAPACITY / 10);
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

        public double GetOnlineRenderLagValue()
        {
            lock (_lockRenderLag)
            {
                if (!_renderLagValues.Any())
                    return 0;

                return _renderLagValues.Average();
            }
        }
    }
}
