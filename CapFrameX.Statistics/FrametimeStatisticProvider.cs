using MathNet.Numerics.Statistics;
using System.Collections.Generic;

namespace CapFrameX.Statistics
{
	public class FrametimeStatisticProvider : IStatisticProvider
	{
		public IList<double> GetOutlierAdjustedSequence(IList<double> sequence, ERemoveOutlierMethod method)
		{
			IList<double> adjustedSequence = null;

			switch (method)
			{
				case ERemoveOutlierMethod.DeciPercentile:
					{
						var deciPercentile = sequence.Quantile(0.999);
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
