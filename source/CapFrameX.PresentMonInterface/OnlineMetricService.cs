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
        private readonly IStatisticProvider _frametimeStatisticProvider;
        private readonly List<double> _frametimes = new List<double>();
        private readonly object _lock = new object();
        private string _currentProcess;
        private double _currentTime = 0;
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

            if (lineSplit.Length <= 12)
                return;

            var startTime = Convert.ToDouble(lineSplit[11], CultureInfo.InvariantCulture);
            var frameTime = Convert.ToDouble(lineSplit[12], CultureInfo.InvariantCulture);
            string processName = lineSplit[0].Replace(".exe", "");

            if (_currentProcess == processName)
            {
                lock (_lock)
                {
                    if (startTime - _currentTime <= _maxOnlineIntervalLength)
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
            _currentTime = Convert.ToDouble(lineSplit[11], CultureInfo.InvariantCulture);

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
