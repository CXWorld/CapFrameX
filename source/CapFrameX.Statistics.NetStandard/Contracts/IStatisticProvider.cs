using System.Collections.Generic;

namespace CapFrameX.Statistics.NetStandard.Contracts
{
    public interface IStatisticProvider
    {
        IList<double> GetOutlierAdjustedSequence(IList<double> sequence, ERemoveOutlierMethod method);

        double GetAdaptiveStandardDeviation(IList<double> sequence, double timeWindow);

        double GetStutteringCountPercentage(IList<double> sequence, double stutteringFactor);

        double GetStutteringTimePercentage(IList<double> sequence, double stutteringFactor);

        double GetLowFPSTimePercentage(IList<double> sequence, double stutteringFactor, double lowFPSThreshold);

        IList<double> GetMovingAverage(IList<double> sequence);

        double GetPQuantileSequence(IList<double> sequence, double pQuantile);

        double GetPercentageHighSequence(IList<double> sequence, double pQuantile);

        double GetFpsMetricValue(IList<double> sequence, EMetric metric);

        double GetPhysicalMetricValue(IList<double> sequence, EMetric metric, double coefficient);

        IList<double>[] GetDiscreteDistribution(IList<double> sequence);

        IMetricAnalysis GetMetricAnalysis(IList<double> frametimes, string secondMetric, string thirdMetric);

        bool[] GetOutlierAnalysis(IList<IMetricAnalysis> metricAnalysisSet, string relatedMetric, int outlierPercentage);

        IList<int> GetFpsThresholdCounts(IList<double> frametimes, bool isReversed);

        IList<double> GetFpsThresholdTimes(IList<double> frametimes, bool isReversed);

        IList<double> GetFrametimeVariancePercentages(IList<double> frametimes);
    }
}
