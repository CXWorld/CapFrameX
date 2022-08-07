﻿namespace CapFrameX.Statistics.PlotBuilder.Contracts
{
	public interface IPlotSettings
	{
		bool ShowGpuLoad { get; }
		bool ShowCpuLoad { get; }
		bool ShowCpuMaxThreadLoad { get; }
		bool ShowGpuPowerLimit { get; }

		bool IsAnyPercentageGraphVisible { get; }

		bool ShowAggregationSeparators { get; }

		bool ShowThresholds { get; }
		double StutteringFactor { get;}
		double LowFPSThreshold { get; }
	}
}
