using CapFrameX.Extensions;
using CapFrameX.Contracts.Configuration;
using CapFrameX.EventAggregation.Messages;
using LiveCharts.Geared;
using Prism.Events;
using Prism.Mvvm;
using Prism.Regions;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CapFrameX.ViewModel
{
	public class ColorbarViewModel : BindableBase
	{
		private readonly IRegionManager _regionManager;
		private readonly IEventAggregator _eventAggregator;
		private readonly IAppConfiguration _appConfiguration;

		private PubSubEvent<ViewMessages.ResetRecord> _resetRecordEvent;
		private Quality _selectedChartQualityLevel;
		private bool _singleRecordIsChecked = true;
		private bool _recordComparisonIsChecked;
		private bool _reportIsChecked;
		private int _selectWindowSize;
		private double _stutteringFactor;

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

		public int SelectWindowSize
		{
			get { return _selectWindowSize; }
			set
			{
				_selectWindowSize = value;
				_appConfiguration.MovingAverageWindowSize = value;
				RaisePropertyChanged();
			}
		}

		public double StutteringFactor
		{
			get { return _stutteringFactor; }
			set
			{
				_stutteringFactor = value;
				_appConfiguration.StutteringFactor = value;
				RaisePropertyChanged();
			}
		}

		public Quality SelectedChartQualityLevel
		{
			get { return _selectedChartQualityLevel; }
			set
			{
				_selectedChartQualityLevel = value;
				_appConfiguration.ChartQualityLevel = value.ConvertToString();
				RaisePropertyChanged();
			}
		}

		public IList<int> WindowSizes { get; }

		public string ObservedDirectory => _appConfiguration.ObservedDirectory;

		public Array ChartQualityLevels => Enum.GetValues(typeof(Quality));

		public ColorbarViewModel(IRegionManager regionManager,
								 IEventAggregator eventAggregator,
								 IAppConfiguration appConfiguration)
		{
			_regionManager = regionManager;
			_eventAggregator = eventAggregator;
			_appConfiguration = appConfiguration;

			StutteringFactor = _appConfiguration.StutteringFactor;
			SelectWindowSize = _appConfiguration.MovingAverageWindowSize;
			SelectedChartQualityLevel = _appConfiguration.ChartQualityLevel.ConverToEnum<Quality>();
			WindowSizes = new List<int>(Enumerable.Range(4, 100 - 4));

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
