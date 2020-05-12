using CapFrameX.Contracts.Configuration;
using CapFrameX.Data;
using CapFrameX.Data.Session.Contracts;
using CapFrameX.Statistics.NetStandard;
using CapFrameX.Statistics.PlotBuilder.Contracts;
using OxyPlot;
using Prism.Mvvm;

namespace CapFrameX.ViewModel.DataContext
{
	public class GraphDataContextBase : BindableBase
	{
		public const int SCALE_RESOLUTION = 200;

		protected readonly IStatisticProvider FrrametimesStatisticProvider;

		protected PlotModel PlotModel { get; set; }

		protected IAppConfiguration AppConfiguration { get; }

		protected IRecordDataServer RecordDataServer { get; }

		public GraphDataContextBase(IAppConfiguration appConfiguration, 
			IRecordDataServer recordDataServer, IStatisticProvider frametimesStatisticProvider)
		{
			AppConfiguration = appConfiguration;
			RecordDataServer = recordDataServer;
			FrrametimesStatisticProvider = frametimesStatisticProvider;
		}

		public ISession RecordSession
		{
			get => RecordDataServer.CurrentSession;
			set
			{
				RecordDataServer.CurrentSession = value;
			}
		}
	}

	public class VisibleGraphs: IPlotSettings
	{
		public bool ShowGpuLoad { get; private set; }
		public bool ShowCpuLoad { get; private set; }
		public bool ShowCpuMaxThreadLoad { get; private set; }
		public bool ShowGpuPowerLimit { get; private set; }

		public bool IsAnyGraphVisible => ShowGpuLoad || ShowCpuLoad || ShowCpuMaxThreadLoad || ShowGpuPowerLimit;

		public VisibleGraphs(bool gpuLoad, bool cpuLoad, bool cpuMaxThreadLoad, bool gpuPowerLimit)
		{
			ShowGpuLoad = gpuLoad;
			ShowCpuLoad = cpuLoad;
			ShowCpuMaxThreadLoad = cpuMaxThreadLoad;
			ShowGpuPowerLimit = gpuPowerLimit;
		}
	}
}
