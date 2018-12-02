using System.Collections.Generic;

namespace CapFrameX.Statistics
{
	public interface IStatisticProvider
	{
		IList<double> GetOutlierAdjustedSequence(IList<double> sequence, ERemoveOutlierMethod method);

		double GetAdaptiveStandardDeviation(IList<double> sequence, int windowSize);

		IList<double> GetMovingAverage(IList<double> sequence, int windowSize);

		double GetPQuantileSequence(IList<double> sequence, double pQuantile);
	}
}
