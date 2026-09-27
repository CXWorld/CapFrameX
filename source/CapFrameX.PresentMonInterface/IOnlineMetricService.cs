using System;
using CapFrameX.Statistics.NetStandard.Contracts;

namespace CapFrameX.PresentMonInterface
{
    public interface IOnlineMetricService : IDisposable
    {
        double GetOnlineFpsMetricValue(EMetric metric);

        double GetOnlineGpuActiveTimeMetricValue(EMetric metric);

        double GetOnlineCpuActiveTimeMetricValue(EMetric metric);

        double GetOnlineFrameTimeMetricValue(EMetric metric);

        double GetOnlineGpuActiveTimeDeviationMetricValue();

        double GetOnlineStutteringPercentageValue();

        double GetOnlinePcLatencyAverageValue();

        double GetOnlineAnimationErrorValue();

        OnlinePmdMetrics GetPmdMetricsPowerCurrent();

        void ResetRealtimeMetrics();

		void SetMetricInterval();
	}
}
