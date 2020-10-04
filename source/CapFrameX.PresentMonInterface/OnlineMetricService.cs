using CapFrameX.Contracts.PresentMonInterface;
using CapFrameX.Contracts.RTSS;
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

        private readonly IStatisticProvider _frametimeStatisticProvider;
        private readonly IRTSSService _rTSSService;
        private readonly ICaptureService _captureServive;
        private readonly IEventAggregator _eventAggregator;
        private readonly List<double> _frametimes = new List<double>();
        private readonly List<double> _measuretimes = new List<double>();
        private readonly ILogger<OnlineMetricService> _logger;
        private readonly object _lock = new object();
        private string _currentProcess;
        // ToDo: get value from config
        // length in seconds
        private readonly double _maxOnlineIntervalLength = 20d;

        public OnlineMetricService(IStatisticProvider frametimeStatisticProvider,
            IRTSSService rTSSService,
            ICaptureService captureServive,
            IEventAggregator eventAggregator,
            ILogger<OnlineMetricService> logger)
        {
            _rTSSService = rTSSService;
            _captureServive = captureServive;
            _eventAggregator = eventAggregator;
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
                                _currentProcess = msg.Process;
                            });
        }

        private void ConnectOnlineMetricDataStream()
        {
            _captureServive.RedirectedOutputDataStream
            .Skip(5)
            .ObserveOn(new EventLoopScheduler()).Subscribe(dataLine =>
            {
                if (string.IsNullOrWhiteSpace(dataLine))
                    return;

                UpdateOnlineMetrics(Tuple.Create(_currentProcess, dataLine));

                var lineSplit = dataLine.Split(',');

                if (lineSplit.Length < 2)
                {
                    _logger.LogInformation("Unusable string {dataLine}.", dataLine);
                    return;
                }

                if (_currentProcess == lineSplit[0].Replace(".exe", ""))
                {
                    if (uint.TryParse(lineSplit[1], out uint processID))
                    {
                        _rTSSService.ProcessIdStream.OnNext(processID);
                    }
                }
            });
        }

        private void UpdateOnlineMetrics(Tuple<string, string> dataSet)
        {
            if (dataSet == null)
                return;

            if (dataSet.Item1 == null || dataSet.Item1 != _currentProcess)
                ResetMetrics();

            _currentProcess = dataSet.Item1;

            var lineSplit = dataSet.Item2.Split(',');

            if (lineSplit.Length <= 12)
            {
                _logger.LogInformation("{dataLine} string unusable for online metrics.", dataSet.Item2);
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

            string processName = lineSplit[0].Replace(".exe", "");

            if (_currentProcess == processName)
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
        }

        private void ResetMetrics()
        {
            lock (_lock)
            {
                _frametimes.Clear();
                _measuretimes.Clear();
            }
        }

        public double GetOnlineFpsMetricValue(EMetric metric)
        {
            lock (_lock)
                return _frametimeStatisticProvider
                    .GetFpsMetricValue(_frametimes, metric);
        }
    }
}
