using CapFrameX.Statistics.NetStandard;
using CapFrameX.Statistics.NetStandard.Contracts;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reactive.Subjects;

namespace CapFrameX.PresentMonInterface
{
	public class OnlineMetricService : IOnlineMetricService
	{
		private const double STUTTERING_THRESHOLD = 2d;

		private readonly IStatisticProvider _frametimeStatisticProvider;
		private readonly List<double> _frametimes = new List<double>();
		private readonly List<double> _measuretimes = new List<double>();
		private readonly object _lock = new object();
		private string _currentProcess;
		// ToDo: get value from config
		// length in seconds
		private readonly double _maxOnlineIntervalLength = 20d;

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

			if (!double.TryParse(lineSplit[11], NumberStyles.Any, CultureInfo.InvariantCulture, out double startTime))
			{
				ResetMetrics(dataSet.Item2);
				return;
			}

			if (!double.TryParse(lineSplit[12], NumberStyles.Any, CultureInfo.InvariantCulture, out double frameTime))
			{
				ResetMetrics(dataSet.Item2);
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
					if (startTime - _measuretimes.First() <= _maxOnlineIntervalLength)
					{
						_frametimes.Add(frameTime);
					}
					else
					{
						int position = 0;
						while (position < _measuretimes.Count && startTime - _measuretimes[position] > _maxOnlineIntervalLength) position++;
						_frametimes.Add(frameTime);
						_frametimes.RemoveRange(0, position);
						_measuretimes.RemoveRange(0, position);
					}
				}
			}
		}

		private void ResetMetrics(string dataLine)
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
				return _frametimeStatisticProvider.GetFpsMetricValue(_frametimes, metric);
		}
	}
}
