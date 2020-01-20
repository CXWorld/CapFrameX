using CapFrameX.Contracts.Configuration;
using CapFrameX.Contracts.Statistics;
using CapFrameX.Extensions;
using MathNet.Numerics.Statistics;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace CapFrameX.Statistics
{
	public class FrametimeStatisticProvider : IStatisticProvider
	{
		public static readonly double[] FPSTHRESHOLDS = new double[] { 10, 15, 30, 45, 60, 75, 90, 120, 144, 240 }.Reverse().ToArray();

		private const double TAU = 0.999;
		private readonly IAppConfiguration _appConfiguration;

		public FrametimeStatisticProvider(IAppConfiguration appConfiguration)
		{
			_appConfiguration = appConfiguration;
		}

		public double GetAdaptiveStandardDeviation(IList<double> sequence, int windowSize)
		{
			var movingAverage = sequence.MovingAverage(windowSize).ToList();

			if (movingAverage.Count != sequence.Count)
			{
				throw new InvalidDataException("Different sample count data vs. filtered data");
			}

			var sumResidualSquares = sequence.Select((val, i) => Math.Pow(val - movingAverage[i], 2)).Sum();
			return Math.Sqrt(sumResidualSquares / (sequence.Count - 1));
		}

		public double GetStutteringCountPercentage(IList<double> sequence, double stutteringFactor)
		{
			var average = sequence.Average();
			var stutteringCount = sequence.Count(element => element > stutteringFactor * average);

			return 100 * (double)stutteringCount / sequence.Count;
		}

		public double GetStutteringTimePercentage(IList<double> sequence, double stutteringFactor)
		{
			var average = sequence.Average();
			var stutteringTime = sequence.Where(element => element > stutteringFactor * average).Sum();

			return 100 * stutteringTime / sequence.Sum();
		}

		public IList<double> GetMovingAverage(IList<double> sequence, int windowSize)
		{
			return sequence.MovingAverage(windowSize).ToList();
		}

		public IList<double> GetOutlierAdjustedSequence(IList<double> sequence, ERemoveOutlierMethod method)
		{
			IList<double> adjustedSequence = null;

			switch (method)
			{
				case ERemoveOutlierMethod.DeciPercentile:
					{
						var deciPercentile = sequence.Quantile(TAU);
						adjustedSequence = new List<double>();

						foreach (var element in sequence)
						{
							if (element < deciPercentile)
								adjustedSequence.Add(element);
						}
					}
					break;
				case ERemoveOutlierMethod.InterquartileRange:
					break;
				case ERemoveOutlierMethod.ThreeSigma:
					break;
				case ERemoveOutlierMethod.TwoDotFiveSigma:
					break;
				case ERemoveOutlierMethod.None:
					adjustedSequence = sequence;
					break;
				default:
					adjustedSequence = sequence;
					break;
			}

			return adjustedSequence;
		}

		public double GetPQuantileSequence(IList<double> sequence, double pQuantile)
		{
			return sequence.Quantile(pQuantile);
		}

		public double GetPAverageLowSequence(IList<double> sequence, double pQuantile)
		{
			var pQuantileValue = sequence.Quantile(pQuantile);
			var subSequence = sequence.Where(element => element <= pQuantileValue);

			if (!subSequence.Any())
				return double.NaN;

			return subSequence.Average();
		}

		public double GetPAverageHighSequence(IList<double> sequence, double pQuantile)
		{
			var pQuantileValue = sequence.Quantile(pQuantile);
			var subSequence = sequence.Where(element => element >= pQuantileValue);

			if (!subSequence.Any())
				return double.NaN;

			return subSequence.Average();
		}

		/// <summary>
		/// Calculate FPS metric values.
		/// frametimes.
		/// </summary>
		/// <param name="sequence"></param>
		/// <param name="metric"></param>
		/// <returns>metric value</returns>
		public double GetFpsMetricValue(IList<double> sequence, EMetric metric)
		{
			double metricValue;
			IList<double> fps;
			switch (metric)
			{
				case EMetric.Max:
					fps = sequence.Select(ft => 1000 / ft).ToList();
					metricValue = fps.Max();
					break;
				case EMetric.P99:
					fps = sequence.Select(ft => 1000 / ft).ToList();
					metricValue = GetPQuantileSequence(fps, 0.99);
					break;
				case EMetric.P95:
					fps = sequence.Select(ft => 1000 / ft).ToList();
					metricValue = GetPQuantileSequence(fps, 0.95);
					break;
				case EMetric.Average:
					metricValue = sequence.Count * 1000 / sequence.Sum();
					break;
				case EMetric.P5:
					fps = sequence.Select(ft => 1000 / ft).ToList();
					metricValue = GetPQuantileSequence(fps, 0.05);
					break;
				case EMetric.P1:
					fps = sequence.Select(ft => 1000 / ft).ToList();
					metricValue = GetPQuantileSequence(fps, 0.01);
					break;
				case EMetric.P0dot2:
					fps = sequence.Select(ft => 1000 / ft).ToList();
					metricValue = GetPQuantileSequence(fps, 0.002);
					break;
				case EMetric.P0dot1:
					fps = sequence.Select(ft => 1000 / ft).ToList();
					metricValue = GetPQuantileSequence(fps, 0.001);
					break;
				case EMetric.OnePercentLow:
					metricValue = 1000 / GetPAverageHighSequence(sequence, 1 - 0.01);
					break;
				case EMetric.ZerodotOnePercentLow:
					metricValue = 1000 / GetPAverageHighSequence(sequence, 1 - 0.001);
					break;
				case EMetric.Min:
					fps = sequence.Select(ft => 1000 / ft).ToList();
					metricValue = fps.Min();
					break;
				case EMetric.AdaptiveStd:
					fps = sequence.Select(ft => 1000 / ft).ToList();
					metricValue = GetAdaptiveStandardDeviation(fps, _appConfiguration.MovingAverageWindowSize);
					break;
				default:
					metricValue = double.NaN;
					break;
			}

			return Math.Round(metricValue, _appConfiguration.FpsValuesRoundingDigits);
		}

		public IList<double>[] GetDiscreteDistribution(IList<double> sequence)
		{
			var min = sequence.Min();
			var max = sequence.Max();

			var binWidth = CalculateOptimalBinWidth(sequence);
			int count = (int)Math.Round((max - min) / binWidth, 0);

			if (count == 4)
			{
				double[] minimalBinIntervals = LinearSpace(min, max, count + 1);
				var histogram = Histogram(sequence, minimalBinIntervals);
				count -= histogram.Count(bin => bin == 0);
			}

			double[] binIntervals = LinearSpace(min, max, count + 1);
			return Distribution(sequence, binIntervals);
		}

		private double CalculateOptimalBinWidth(IList<double> sequence)
		{
			double xMax = sequence.Max(), xMin = sequence.Min();
			int minBins = 4, maxBins = 16;
			double[] N = Enumerable.Range(minBins, maxBins - minBins)
				.Select(v => (double)v).ToArray();
			double[] D = N.Select(v => (xMax - xMin) / v).ToArray();
			double[] C = new double[D.Length];

			for (int i = 0; i < N.Length; i++)
			{
				double[] binIntervals = LinearSpace(xMin, xMax, (int)N[i] + 1);
				int[] ki = Histogram(sequence, binIntervals);
				ki = ki.Skip(1).Take(ki.Length - 2).ToArray();

				double mean = ki.Average();
				double variance = ki.Select(v => Math.Pow(v - mean, 2)).Sum() / N[i];

				C[i] = (2 * mean - variance) / (Math.Pow(D[i], 2));
			}

			double minC = C.Min();
			int index = C.Select((c, ix) => new { Value = c, Index = ix })
				.Where(c => c.Value == minC).First().Index;

			// optimal bin width
			return D[index];
		}

		private int[] Histogram(IList<double> data, double[] binEdges)
		{
			int[] counts = new int[binEdges.Length - 1];

			for (int i = 0; i < binEdges.Length - 1; i++)
			{
				double lower = binEdges[i], upper = binEdges[i + 1];

				for (int j = 0; j < data.Count; j++)
				{
					if (data[j] >= lower && data[j] <= upper)
					{
						counts[i]++;
					}
				}
			}

			return counts;
		}

		private IList<double>[] Distribution(IList<double> data, double[] binEdges)
		{
			List<double>[] counts = new List<double>[binEdges.Length - 1];

			for (int i = 0; i < binEdges.Length - 1; i++)
			{
				counts[i] = new List<double>();
				double lower = binEdges[i];
				double upper = binEdges[i + 1];

				for (int j = 0; j < data.Count; j++)
				{
					if (data[j] >= lower && data[j] <= upper)
					{
						counts[i].Add(data[j]);
					}
				}
			}

			return counts;
		}

		private double[] LinearSpace(double a, double b, int count)
		{
			double[] output = new double[count];

			for (int i = 0; i < count; i++)
			{
				output[i] = a + ((i * (b - a)) / (count - 1));
			}

			return output;
		}

		public IMetricAnalysis GetMetricAnalysis(IList<double> frametimes, string secondMetric, string thirdMetric)
		{
			var average = GetFpsMetricValue(frametimes, EMetric.Average);
			var secondMetricValue = GetFpsMetricValue(frametimes, secondMetric.ConvertToEnum<EMetric>());
			var thrirdMetricValue = GetFpsMetricValue(frametimes, thirdMetric.ConvertToEnum<EMetric>());
			string numberFormat = string.Format("F{0}", _appConfiguration.FpsValuesRoundingDigits);
			var cultureInfo = CultureInfo.InvariantCulture;

			string secondMetricString =
				secondMetric.ConvertToEnum<EMetric>() != EMetric.None ?
				$"{secondMetric.ConvertToEnum<EMetric>().GetShortDescription()}=" +
				$"{secondMetricValue.ToString(numberFormat, cultureInfo)} " +
				$"FPS | " : string.Empty;

			string thirdMetricString =
				thirdMetric.ConvertToEnum<EMetric>() != EMetric.None ?
				$"{thirdMetric.ConvertToEnum<EMetric>().GetShortDescription()}=" +
				$"{thrirdMetricValue.ToString(numberFormat, cultureInfo)} " +
				$"FPS" : string.Empty;

			return new MetricAnalysis()
			{
				ResultString = $"Avg={average.ToString(numberFormat, cultureInfo)} " +
				$"FPS | " + secondMetricString + thirdMetricString,
				Average = average,
				Second = secondMetricValue,
				Third = thrirdMetricValue
			};
		}

		public bool[] GetOutlierAnalysis(IList<IMetricAnalysis> metricAnalysisSet, string relatedMetric, int outlierPercentage)
		{
			var averageValues = metricAnalysisSet.Select(analysis => analysis.Average).ToList();
			var secondMetricValues = metricAnalysisSet.Select(analysis => analysis.Second).ToList();
			var thirdMetricValues = metricAnalysisSet.Select(analysis => analysis.Third).ToList();

			bool[] outlierFlags = Enumerable.Repeat(false, metricAnalysisSet.Count).ToArray();

			if (relatedMetric == "Average")
			{
				outlierFlags = GetOutlierFlags(averageValues, outlierPercentage);
			}
			else if (relatedMetric == "Second")
			{
				outlierFlags = GetOutlierFlags(secondMetricValues, outlierPercentage);
			}
			else if (relatedMetric == "Third")
			{
				outlierFlags = GetOutlierFlags(thirdMetricValues, outlierPercentage);
			}

			return outlierFlags;
		}

		private bool[] GetOutlierFlags(IList<double> metricValues, int outlierPercentage)
		{
			bool[] outlierFlags = Enumerable.Repeat(false, metricValues.Count).ToArray();
			var median = GetPQuantileSequence(metricValues, 0.5);

			for (int i = 0; i < metricValues.Count; i++)
			{
				if ((Math.Abs(metricValues[i] - median) / median) * 100d > outlierPercentage)
				{
					outlierFlags[i] = true;
				}
			}

			return outlierFlags;
		}

		public IList<int> GetFpsThresholdCounts(IList<double> frametimes)
		{
			var fps = frametimes.Select(ft => 1000 / ft);

			var thresholds = new List<int>(FPSTHRESHOLDS.Length);

			foreach (var threshold in FPSTHRESHOLDS)
			{
				thresholds.Add(fps.Count(val => val < threshold));
			}

			return thresholds;
		}
	}
}
