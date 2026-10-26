using System;
using CapFrameX.Statistics.NetStandard.Contracts;

namespace CapFrameX.PresentMonInterface
{
    public interface IOnlineMetricService : IDisposable
    {
        /// <summary>Enables metric evaluation for an independent telemetry consumer.</summary>
        IDisposable AcquireTelemetry();

        /// <summary>UTC arrival time of the latest accepted frame, or MinValue without data.</summary>
        DateTime LastFrameTimestampUtc { get; }

        /// <summary>Latest PMD sample window without consuming the classic overlay buffer.</summary>
        OnlinePmdMetrics GetPmdTelemetrySnapshot();

        OnlineFrameTelemetrySnapshot GetFrameTelemetrySnapshot();

        double GetOnlineFpsMetricValue(EMetric metric);

        double GetOnlineGpuActiveTimeMetricValue(EMetric metric);

        double GetOnlineCpuActiveTimeMetricValue(EMetric metric);

        double GetOnlineFrameTimeMetricValue(EMetric metric);

        double GetOnlineGpuActiveTimeDeviationMetricValue();

        double GetOnlineStutteringPercentageValue();

        double GetOnlinePcLatencyAverageValue();

        double GetOnlineAnimationErrorValue();

        /// <summary>
        /// PresentMon's present mode of most of the target's presents within the last second, e.g.
        /// "Composed: Flip". Null while the target presented nothing for 2 s.
        /// </summary>
        string GetOnlinePresentMode();

        OnlinePmdMetrics GetPmdMetricsPowerCurrent();

        void ResetRealtimeMetrics();

		void SetMetricInterval();
	}
}
