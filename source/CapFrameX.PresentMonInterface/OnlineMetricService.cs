using CapFrameX.Contracts.Overlay;
using CapFrameX.Contracts.PresentMonInterface;
using CapFrameX.EventAggregation.Messages;
using CapFrameX.Statistics.NetStandard;
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
        private readonly object _lock = new object();
        private List<double> _frametimes = new List<double>(LIST_CAPACITY);
        private List<double> _measuretimes = new List<double>(LIST_CAPACITY);
        private string _currentProcess;
        private int _currentProcessId;
        private readonly double _maxOnlineIntervalLength = 20d;

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
                                        ResetMetrics();

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
                .Where(lineSplit => lineSplit.Length > 1)
                .Subscribe(UpdateOnlineMetrics);
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

        private void UpdateOnlineMetrics(string[] lineSplit)
        {
            string process;
            try
            {
                process = lineSplit[0].Replace(".exe", "");
            }
            catch { return; }

            lock (_currentProcessLock)
            {
                if (process != _currentProcess)
                    return;
            }

            if (!int.TryParse(lineSplit[1], out int processId))
            {
                ResetMetrics();
                return;
            }

            lock (_currentProcessLock)
            {
                if (_currentProcessId != processId)
                    return;
            }

            if (lineSplit.Length <= 12)
            {
                _logger.LogInformation("{dataLine} string unusable for online metrics.", string.Join(",", lineSplit));
                return;
            }

            if (!double.TryParse(lineSplit[11], NumberStyles.Any, CultureInfo.InvariantCulture, out double startTime))
            {
                ResetMetrics();
                return;
            }

            if (!double.TryParse(lineSplit[12], NumberStyles.Any, CultureInfo.InvariantCulture, out double frameTime))
            {
                ResetMetrics();
                return;
            }

            // it makes no sense to calculate fps metrics with
            // frame times above the stuttering threshold
            // filtering high frame times caused by focus lost for example
            if (frameTime > STUTTERING_THRESHOLD * 1E03)
            {
                return;
            }

            try
            {
                lock (_lock)
                {
                    _measuretimes.Add(startTime);
                    _frametimes.Add(frameTime);

                    if (startTime - _measuretimes.First() > _maxOnlineIntervalLength)
                    {
                        int position = 0;
                        while (position < _measuretimes.Count &&
                            startTime - _measuretimes[position] > _maxOnlineIntervalLength)
                            position++;

                        if (position > 0)
                        {
                            _frametimes.RemoveRange(0, position);
                            _measuretimes.RemoveRange(0, position);
                        }
                    }
                }
            }
            catch { ResetMetrics(); }
        }

        private void ResetMetrics()
        {
            lock (_lock)
            {
                _frametimes = new List<double>(LIST_CAPACITY);
                _measuretimes = new List<double>(LIST_CAPACITY);
            }
        }

        public double GetOnlineFpsMetricValue(EMetric metric)
        {
            lock (_lock)
            {
                return _frametimeStatisticProvider
                    .GetFpsMetricValue(_frametimes, metric);
            }
        }
    }
}
