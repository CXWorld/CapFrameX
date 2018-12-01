using System.Collections.Generic;

namespace CapFrameX.Statistics
{
	public interface IStatisticProvider
	{
		IList<double> GetOutlierAdjustedSequence(IList<double> sequence, ERemoveOutlierMethod method);

		double GetPQuantileSequence(IList<double> sequence, double pQuantile);
	}
}
