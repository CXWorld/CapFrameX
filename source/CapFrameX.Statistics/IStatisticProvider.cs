using System.Collections.Generic;

namespace CapFrameX.Statistics
{
	public interface IStatisticProvider
	{
		IList<double> GetOutlierAdjustedSequence(IList<double> sequence, ERemoveOutlierMethod method);

		double GetAdaptiveStandardDeviation(IList<double> sequence, int windowSize);

		double GetStutteringCountPercentage(IList<double> sequence, double stutteringFactor);

		double GetStutteringTimePercentage(IList<double> sequence, double stutteringFactor);

		IList<double> GetMovingAverage(IList<double> sequence, int windowSize);

		double GetPQuantileSequence(IList<double> sequence, double pQuantile);

		double GetPAverageLowSequence(IList<double> sequence, double pQuantile);

		double GetPAverageHighSequence(IList<double> sequence, double pQuantile);

		double GetFpsMetricValue(IList<double> sequence, EMetric metric);

		List<double>[] GetDiscreteDistribution(IList<double> sequence);

		MetricAnalysis GetMetricAnalysis(List<double> frametimes, string secondMetric, string thirdMetric);
	}
}
