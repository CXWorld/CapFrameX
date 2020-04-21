using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using CapFrameX.Contracts.Configuration;
using CapFrameX.Contracts.Data;
using CapFrameX.Data;
using CapFrameX.Data.Session.Contracts;
using CapFrameX.Extensions.NetStandard;
using CapFrameX.Statistics;
using CapFrameX.Statistics.NetStandard;
using CapFrameX.Statistics.PlotBuilder.Contracts;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;
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

		public GraphDataContextBase(IAppConfiguration appConfiguration, IRecordDataServer recordDataServer, IStatisticProvider frametimesStatisticProvider)
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

		public bool IsAnyGraphVisible => ShowGpuLoad || ShowCpuLoad || ShowCpuMaxThreadLoad;

		public VisibleGraphs(bool gpuLoad, bool cpuLoad, bool cpuMaxThreadLoad)
		{
			ShowGpuLoad = gpuLoad;
			ShowCpuLoad = cpuLoad;
			ShowCpuMaxThreadLoad = cpuMaxThreadLoad;
		}
	}


}
