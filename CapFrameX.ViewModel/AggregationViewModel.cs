using CapFrameX.Contracts.Configuration;
using CapFrameX.Contracts.OcatInterface;
using Prism.Events;

namespace CapFrameX.ViewModel
{
	public class AggregationViewModel
	{
		private readonly IRecordDirectoryObserver _recordObserver;
		private readonly IEventAggregator _eventAggregator;
		private readonly IAppConfiguration _appConfiguration;

		public AggregationViewModel(IRecordDirectoryObserver recordObserver, 
			IEventAggregator eventAggregator, IAppConfiguration appConfiguration)
		{
			_recordObserver = recordObserver;
			_eventAggregator = eventAggregator;
			_appConfiguration = appConfiguration;
		}
	}
}
