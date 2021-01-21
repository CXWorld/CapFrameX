using CapFrameX.Contracts.Configuration;
using CapFrameX.Data;
using CapFrameX.Data.Session.Contracts;
using CapFrameX.Statistics.NetStandard;
using CapFrameX.Statistics.PlotBuilder.Contracts;
using OxyPlot;
using Prism.Mvvm;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;

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

		protected void OnSavePlotAsImage(string plotType)
		{
			var exporter = new SvgExporter { Width = AppConfiguration.HorizontalGraphExportRes, Height = AppConfiguration.VerticalGraphExportRes};

			using (var memStream = new MemoryStream())
			{
				var illegalFilenameCharsRegex = new Regex(@"[/:*?<>""|]");

				exporter.Export(PlotModel, memStream);
				var filename = string.Join("-", new string[] {
					string.IsNullOrWhiteSpace(RecordSession.Info.GameName) ? RecordSession.Info.ProcessName: RecordSession.Info.GameName,
					RecordSession.Info.Processor,
					RecordSession.Info.GPU,
					RecordSession.Info.SystemRam,
					RecordSession.Info.Comment
				}.Where(filenamePart => !string.IsNullOrWhiteSpace(filenamePart)));

				SaveFileDialog dialog = new SaveFileDialog()
				{
					Filter = "SVG files|*.svg",
					FileName = $"{illegalFilenameCharsRegex.Replace(filename, string.Empty)}_{plotType}",
					DefaultExt = "svg",
				};

				if (dialog.ShowDialog() == DialogResult.OK)
				{
					File.WriteAllBytes(dialog.FileName, memStream.ToArray());
				}
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
