using CapFrameX.Statistics.NetStandard;
using CapFrameX.Statistics.NetStandard.Contracts;
using Microsoft.Extensions.Logging;
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
		private readonly ILogger<OnlineMetricService> _logger;
		private readonly object _lock = new object();
		private string _currentProcess;
		// ToDo: get value from config
		// length in seconds
		private readonly double _maxOnlineIntervalLength = 20d;

		public ISubject<Tuple<string, string>> ProcessDataLineStream { get; }
			= new Subject<Tuple<string, string>>();

		public OnlineMetricService(IStatisticProvider frametimeStatisticProvider, ILogger<OnlineMetricService> logger)
		{
			_frametimeStatisticProvider = frametimeStatisticProvider;
			ProcessDataLineStream.Subscribe(UpdateOnlineMetrics);
			_logger = logger;
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
