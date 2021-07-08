using CapFrameX.Statistics.NetStandard.Contracts;

namespace CapFrameX.PresentMonInterface
{
    public interface IOnlineMetricService
    {
        double GetOnlineFpsMetricValue(EMetric metric);

        double GetOnlineApplicationLatencyValue();
    }
}
