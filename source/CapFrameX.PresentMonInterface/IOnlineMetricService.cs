using CapFrameX.Statistics.NetStandard.Contracts;
using System;

namespace CapFrameX.PresentMonInterface
{
    public interface IOnlineMetricService
    {
        double GetOnlineFpsMetricValue(EMetric metric);

        double GetOnlineGpuActiveTimeMetricValue(EMetric metric);

        double GetOnlineFrameTimeMetricValue(EMetric metric);

        double GetOnlineGpuActiveTimeDeviationMetricValue();

        double GetOnlineStutteringPercentageValue();

        OnlinePmdMetrics GetPmdMetricsPowerCurrent();

        void ResetRealtimeMetrics();

		void SetMetricInterval();
	}
}
