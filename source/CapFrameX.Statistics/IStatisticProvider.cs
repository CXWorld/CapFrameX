using CapFrameX.Contracts.Statistics;
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

		IList<double>[] GetDiscreteDistribution(IList<double> sequence);

		IMetricAnalysis GetMetricAnalysis(IList<double> frametimes, string secondMetric, string thirdMetric);

		bool[] GetOutlierAnalysis(IList<IMetricAnalysis> metricAnalysisSet, string relatedMetric, int outlierPercentage);

		IList<int> GetFpsThresholdCounts(IList<double> frametimes, bool isReversed);
	}
}
