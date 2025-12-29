using CapFrameX.Statistics.NetStandard.Contracts;
using System;

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

        OnlinePmdMetrics GetPmdMetricsPowerCurrent();

        void ResetRealtimeMetrics();

		void SetMetricInterval();
	}
}
