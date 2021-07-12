using CapFrameX.Contracts.Configuration;
using CapFrameX.Data;
using CapFrameX.Data.Session.Contracts;
using CapFrameX.EventAggregation.Messages;
using CapFrameX.Statistics.NetStandard;
using CapFrameX.Statistics.NetStandard.Contracts;
using CapFrameX.Statistics.PlotBuilder.Contracts;
using OxyPlot;
using Prism.Events;
using Prism.Mvvm;
using System.Linq;

namespace CapFrameX.ViewModel.DataContext
{
	public class GraphDataContextBase : BindableBase
	{
		public const int SCALE_RESOLUTION = 200;

		protected readonly IStatisticProvider _frametimesStatisticProvider;
		protected readonly IEventAggregator _eventAggregator;

		protected PlotModel PlotModel { get; set; }

		protected IAppConfiguration AppConfiguration { get; }

		protected IRecordDataServer RecordDataServer { get; }

		public GraphDataContextBase(IAppConfiguration appConfiguration, 
									IRecordDataServer recordDataServer, 
									IStatisticProvider frametimesStatisticProvider,
									IEventAggregator eventAggregator)
		{
			AppConfiguration = appConfiguration;
			RecordDataServer = recordDataServer;
			_frametimesStatisticProvider = frametimesStatisticProvider;
			_eventAggregator = eventAggregator;

			SubscribeToAggregatorEvents();
		}

        public ISession RecordSession
		{
			get => RecordDataServer.CurrentSession;
			set
			{
				RecordDataServer.CurrentSession = value;
			}
		}

		protected void OnSavePlotAsImage(string plotType, string fileFormat)
		{
			var filename = string.Join("-", new string[] {
					string.IsNullOrWhiteSpace(RecordSession.Info.GameName) ? RecordSession.Info.ProcessName: RecordSession.Info.GameName,
					RecordSession.Info.Processor,
					RecordSession.Info.GPU,
					RecordSession.Info.SystemRam,
					RecordSession.Info.Comment,
					plotType
				}.Where(filenamePart => !string.IsNullOrWhiteSpace(filenamePart)));
			if (fileFormat == "svg")
			{
				ImageExport.SavePlotAsSVG(PlotModel, filename, AppConfiguration.HorizontalGraphExportRes, AppConfiguration.VerticalGraphExportRes);
			} else if(fileFormat == "png")
            {
				ImageExport.SavePlotAsPNG(PlotModel, filename, AppConfiguration.HorizontalGraphExportRes, AppConfiguration.VerticalGraphExportRes, AppConfiguration.UseDarkMode);
			}
		}

		private void SubscribeToAggregatorEvents()
		{
			_eventAggregator.GetEvent<PubSubEvent<ViewMessages.ThemeChanged>>()
							.Subscribe(msg =>
							{
								PlotModel.TextColor = AppConfiguration.UseDarkMode ? OxyColors.White : OxyColors.Black;
								PlotModel.InvalidatePlot(false);
							});
		}
	}

	public class VisibleGraphs: IPlotSettings
	{
		public bool ShowGpuLoad { get; private set; }
		public bool ShowCpuLoad { get; private set; }
		public bool ShowCpuMaxThreadLoad { get; private set; }
		public bool ShowGpuPowerLimit { get; private set; }
		public bool ShowAggregationSeparators { get; private set; }

		public bool IsAnyPercentageGraphVisible => ShowGpuLoad || ShowCpuLoad || ShowCpuMaxThreadLoad || ShowGpuPowerLimit;

		public VisibleGraphs(bool gpuLoad, bool cpuLoad, bool cpuMaxThreadLoad, bool gpuPowerLimit, bool aggregationSeparators)
		{
			ShowGpuLoad = gpuLoad;
			ShowCpuLoad = cpuLoad;
			ShowCpuMaxThreadLoad = cpuMaxThreadLoad;
			ShowGpuPowerLimit = gpuPowerLimit;
			ShowAggregationSeparators = aggregationSeparators;
		}

	}
}
