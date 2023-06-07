using CapFrameX.Statistics.NetStandard.Contracts;
using System;

namespace CapFrameX.PresentMonInterface
{
    public interface IOnlineMetricService
    {
        double GetOnlineFpsMetricValue(EMetric metric);

        double GetOnlineApplicationLatencyValue();

        double GetOnlineStutteringPercentageValue();

        OnlinePmdMetrics GetPmdMetricsPowerCurrent();

        void ResetRealtimeMetrics();

		void SetMetricInterval();
	}
}
