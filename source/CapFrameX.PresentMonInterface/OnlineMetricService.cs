using CapFrameX.Statistics.NetStandard;
using CapFrameX.Statistics.NetStandard.Contracts;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reactive.Subjects;

namespace CapFrameX.PresentMonInterface
{
    public class OnlineMetricService : IOnlineMetricService
    {
        private const double STUTTERING_THRESHOLD = 2d;

        private readonly IStatisticProvider _frametimeStatisticProvider;
        private readonly List<double> _frametimes = new List<double>();
        private readonly object _lock = new object();
        private string _currentProcess;
        private double _referenceTime = 0;
        private double _prevTime = 0;
        // ToDo: get value from config
        // length in seconds
        private readonly double _maxOnlineIntervalLength = 30d;

        public ISubject<Tuple<string, string>> ProcessDataLineStream { get; }
            = new Subject<Tuple<string, string>>();

        public OnlineMetricService(IStatisticProvider frametimeStatisticProvider)
        {
            _frametimeStatisticProvider = frametimeStatisticProvider;
            ProcessDataLineStream.Subscribe(UpdateOnlineMetrics);
        }

        private void UpdateOnlineMetrics(Tuple<string, string> dataSet)
        {
            if (dataSet == null)
                return;

            if (dataSet.Item1 == null || dataSet.Item1 != _currentProcess)
                ResetMetrics(dataSet.Item2);

            _currentProcess = dataSet.Item1;

            var lineSplit = dataSet.Item2.Split(',');

            if (lineSplit.Length < 12)
                return;

            double.TryParse(lineSplit[11], NumberStyles.Any, CultureInfo.InvariantCulture, out double variable);
            var startTime = variable;

            // if there's break in the frame times sequence, do a reset
            // this is usually the case when the game has lost focus
            // threshold should be greater than stuttering time
            if (startTime - _prevTime > STUTTERING_THRESHOLD)
                ResetMetrics(dataSet.Item2);

            _prevTime = startTime;

            double.TryParse(lineSplit[12], NumberStyles.Any, CultureInfo.InvariantCulture, out variable);
            var frameTime = variable;

            // it makes no sense to calculate fps metrics with
            // frame times above the stuttering threshold
            // filtering high frame times caused by focus lost for example
            if (frameTime > STUTTERING_THRESHOLD * 1E03)
            {
                ResetMetrics(dataSet.Item2);
                return;
            }

            string processName = lineSplit[0].Replace(".exe", "");

            if (_currentProcess == processName)
            {
                lock (_lock)
                {
                    if (startTime - _referenceTime <= _maxOnlineIntervalLength)
                        _frametimes.Add(frameTime);
                    else
                    {
                        _frametimes.Add(frameTime);
                        _frametimes.RemoveAt(0);
                    }
                }
            }
        }

        private void ResetMetrics(string dataLine)
        {
            var lineSplit = dataLine.Split(',');
            if (lineSplit.Length >= 12)
            {
                double.TryParse(lineSplit[11], NumberStyles.Any, CultureInfo.InvariantCulture, out double variable);
                _referenceTime = variable;
            }

            lock (_lock)
                _frametimes.Clear();
        }

        public double GetOnlineFpsMetricValue(EMetric metric)
        {
            lock (_lock)
                return _frametimeStatisticProvider.GetFpsMetricValue(_frametimes, metric);
        }
    }
}
