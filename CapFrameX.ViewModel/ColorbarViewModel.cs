using CapFrameX.EventAggregation.Messages;
using Prism.Events;
using Prism.Mvvm;
using Prism.Regions;

namespace CapFrameX.ViewModel
{
	public class ColorbarViewModel : BindableBase
	{
		private readonly IRegionManager _regionManager;
		private readonly IEventAggregator _eventAggregator;

		private PubSubEvent<ViewMessages.ResetRecord> _resetRecordEvent;
		private bool _singleRecordIsChecked = true;
		private bool _recordComparisonIsChecked;
		private bool _reportIsChecked;

		public bool SingleRecordIsChecked
		{
			get { return _singleRecordIsChecked; }
			set
			{
				_singleRecordIsChecked = value;
				RaisePropertyChanged();

				if (value == true)
					OnSingleRecordIsCheckedChanged();
			}
		}

		public bool RecordComparisonIsChecked
		{
			get { return _recordComparisonIsChecked; }
			set
			{
				_recordComparisonIsChecked = value;
				RaisePropertyChanged();

				if (value == true)
					OnRecordComparisonIsCheckedChanged();
			}
		}

		public bool ReportIsChecked
		{
			get { return _reportIsChecked; }
			set
			{
				_reportIsChecked = value;
				RaisePropertyChanged();

				if (value == true)
					OnReportIsCheckedChanged();
			}
		}

		public ColorbarViewModel(IRegionManager regionManager, IEventAggregator eventAggregator)
		{
			_regionManager = regionManager;
			_eventAggregator = eventAggregator;

			SetAggregatorEvents();
		}


		private void OnSingleRecordIsCheckedChanged()
		{
			_regionManager.RequestNavigate("DataRegion", "DataView");
			_resetRecordEvent.Publish(new ViewMessages.ResetRecord());
		}

		private void OnRecordComparisonIsCheckedChanged()
		{
			_regionManager.RequestNavigate("DataRegion", "ComparisonDataView");
			_resetRecordEvent.Publish(new ViewMessages.ResetRecord());
		}

		private void OnReportIsCheckedChanged()
		{
			_regionManager.RequestNavigate("DataRegion", "ReportView");
			_resetRecordEvent.Publish(new ViewMessages.ResetRecord());
		}

		private void SetAggregatorEvents()
		{
			_resetRecordEvent = _eventAggregator.GetEvent<PubSubEvent<ViewMessages.ResetRecord>>();
		}
	}
}
