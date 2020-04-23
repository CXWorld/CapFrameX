using System;
using System.Collections.Generic;
using System.Text;

namespace CapFrameX.Statistics.PlotBuilder.Contracts
{
	public interface IPlotSettings
	{
		bool ShowGpuLoad { get; }
		bool ShowCpuLoad { get; }
		bool ShowCpuMaxThreadLoad { get; }

		bool IsAnyGraphVisible { get; }
	}
}
