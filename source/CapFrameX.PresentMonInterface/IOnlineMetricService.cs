using CapFrameX.Statistics;
using System;
using System.Reactive.Subjects;

namespace CapFrameX.PresentMonInterface
{
    public interface IOnlineMetricService
    {
        ISubject<Tuple<string, string>> ProcessDataLineStream { get; }

        double GetOnlineFpsMetricValue(EMetric metric);
    }
}
