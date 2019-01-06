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
	}
}
