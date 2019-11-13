using CapFrameX.Contracts.Configuration;
using CapFrameX.OcatInterface;
using CapFrameX.Statistics;
using Prism.Mvvm;

namespace CapFrameX.ViewModel.DataContext
{
	public class GraphDataContextBase : BindableBase
	{
		public const int SCALE_RESOLUTION = 200;

		protected IAppConfiguration AppConfiguration { get; }

		protected IRecordDataServer RecordDataServer { get; }

		protected IStatisticProvider FrametimesStatisticProvider { get; }

		public Session RecordSession
		{
			get => RecordDataServer.CurrentSession;
			set
			{
				RecordDataServer.CurrentSession = value;
			}
		}

		public GraphDataContextBase(IRecordDataServer recordDataServer,
			IAppConfiguration appConfiguration, IStatisticProvider frametimesStatisticProvider)
		{
			RecordDataServer = recordDataServer;
			AppConfiguration = appConfiguration;
			FrametimesStatisticProvider = frametimesStatisticProvider;
		}
	}
}
