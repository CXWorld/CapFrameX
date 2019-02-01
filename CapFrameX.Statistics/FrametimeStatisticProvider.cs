using MathNet.Numerics.Statistics;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace CapFrameX.Statistics
{
	public class FrametimeStatisticProvider : IStatisticProvider
	{
		private const double TAU = 0.999;

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

		public double GetStutteringPercentage(IList<double> sequence, double stutteringFactor)
		{
			var average = sequence.Average();
			var stutteringCount = sequence.Count(element => element > stutteringFactor * average);

			return Math.Round(100 * (double)stutteringCount / sequence.Count, 3);
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

		public List<double>[] GetDiscreteDistribution(IList<double> sequence)
		{
			var min = sequence.Min();
			var max = sequence.Max();

			var binWidth = CalculateOptimalBinWidth(sequence);
			int count = (int)Math.Round((max - min) / binWidth, 0);

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
				double[] ki = Histogram(sequence, binIntervals);
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

		private double[] Histogram(IList<double> data, double[] binEdges)
		{
			double[] counts = new double[binEdges.Length - 1];

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

		private List<double>[] Distribution(IList<double> data, double[] binEdges)
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
	}
}
