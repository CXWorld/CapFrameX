using MathNet.Numerics.Statistics;
using System.Collections.Generic;
using System.Linq;

namespace CapFrameX.Statistics
{
	public class FrametimeStatisticProvider : IStatisticProvider
	{
		private const double TAU = 0.999;

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
							if(element < deciPercentile)
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
