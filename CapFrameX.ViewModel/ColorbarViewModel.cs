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

			SubscribeToOverlayActivate();
			SubscribeToOverlayDeactivate();
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

		private void SubscribeToOverlayActivate()
		{
			_eventAggregator.GetEvent<PubSubEvent<ViewMessages.ShowOverlay>>()
							.Subscribe(msg =>
							{
								var controlView = _regionManager.Regions["ControlRegion"].Views.FirstOrDefault();
								_regionManager.Regions["ControlRegion"].Deactivate(controlView);
								var colorbarView = _regionManager.Regions["ColorbarRegion"].Views.FirstOrDefault();
								_regionManager.Regions["ColorbarRegion"].Deactivate(colorbarView);								

								var dataRegionViews = _regionManager.Regions["DataRegion"].ActiveViews;

								foreach (var view in dataRegionViews)
								{
									_regionManager.Regions["DataRegion"].Deactivate(view);
								}

								_regionManager.RequestNavigate("OverlayRegion", "OverlayView");
							});
		}

		private void SubscribeToOverlayDeactivate()
		{
			_eventAggregator.GetEvent<PubSubEvent<ViewMessages.HideOverlay>>()
							.Subscribe(msg =>
							{
								var overlayView = _regionManager.Regions["OverlayRegion"].Views.FirstOrDefault();
								_regionManager.Regions["OverlayRegion"].Deactivate(overlayView);

								_regionManager.RequestNavigate("ControlRegion", "ControlView");
								_regionManager.RequestNavigate("ColorbarRegion", "ColorbarView");

								if (SingleRecordIsChecked)
								{
									_regionManager.RequestNavigate("DataRegion", "DataView");
								}

								if (RecordComparisonIsChecked)
								{
									_regionManager.RequestNavigate("DataRegion", "ComparisonDataView");
								}

								if (ReportIsChecked)
								{
									_regionManager.RequestNavigate("DataRegion", "ReportView");
								}
							});
		}
	}
}
