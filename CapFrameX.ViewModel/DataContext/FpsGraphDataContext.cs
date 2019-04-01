using CapFrameX.Contracts.Configuration;
using CapFrameX.OcatInterface;
using CapFrameX.Statistics;

namespace CapFrameX.ViewModel.DataContext
{
	public class FpsGraphDataContext : GraphDataContextBase
	{
		public FpsGraphDataContext(IRecordDataServer recordDataServer,
			IAppConfiguration appConfiguration, IStatisticProvider frametimesStatisticProvider) : 
			base(recordDataServer, appConfiguration, frametimesStatisticProvider)
		{
		}
    }
}
