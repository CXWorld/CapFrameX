using CapFrameX.Contracts.Configuration;
using CapFrameX.Contracts.Overlay;
using CapFrameX.Extensions;
using CapFrameX.Hotkey;
using CapFrameX.Statistics;
using Gma.System.MouseKeyHook;
using Prism.Events;
using Prism.Mvvm;
using Prism.Regions;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CapFrameX.ViewModel
{
	public class OverlayViewModel : BindableBase, INavigationAware
	{
		private readonly IOverlayService _overlayService;
		private readonly IAppConfiguration _appConfiguration;
		private readonly IEventAggregator _eventAggregator;

		private IKeyboardMouseEvents _globalOverlayHookEvent;
		private IKeyboardMouseEvents _globalResetHistoryHookEvent;

		private bool IsOverlayActive
		{
			get { return _appConfiguration.IsOverlayActive; }
			set
			{
				_appConfiguration.IsOverlayActive = value;
				_overlayService.IsOverlayActiveStream.OnNext(value);
			}
		}

		public string OverlayHotkeyString
		{
			get { return _appConfiguration.OverlayHotKey; }
			set
			{
				if (!CXHotkey.IsValidHotkey(value))
					return;

				_appConfiguration.OverlayHotKey = value;
				UpdateGlobalOverlayHookEvent();
				RaisePropertyChanged();
			}
		}

		public string ResetHistoryHotkeyString
		{
			get { return _appConfiguration.ResetHistoryHotkey; }
			set
			{
				if (!CXHotkey.IsValidHotkey(value))
					return;

				_appConfiguration.ResetHistoryHotkey = value;
				UpdateGlobalResetHistoryHookEvent();
				RaisePropertyChanged();
			}
		}

		public EMetric SelectedSecondMetric
		{
			get
			{
				return _appConfiguration
				  .SecondMetricOverlay
				  .ConvertToEnum<EMetric>();
			}
			set
			{
				_appConfiguration.SecondMetricOverlay =
					value.ConvertToString();
				_overlayService.SecondMetric = value.ConvertToString();
				RaisePropertyChanged();
			}
		}

		public EMetric SelectedThirdMetric
		{
			get
			{
				return _appConfiguration
				  .ThirdMetricOverlay
				  .ConvertToEnum<EMetric>(); 
			}
			set
			{
				_appConfiguration.ThirdMetricOverlay =
					value.ConvertToString();
				_overlayService.ThirdMetric = value.ConvertToString();
				RaisePropertyChanged();
			}
		}

		public int SelectedNumberOfRuns
		{
			get
			{
				return _appConfiguration
				  .SelectedHistoryRuns; 
			}
			set
			{
				_appConfiguration.SelectedHistoryRuns =
					value;

				_overlayService.UpdateNumberOfRuns(value);

				if (SelectedNumberOfAggregationRuns > SelectedNumberOfRuns)
					SelectedNumberOfAggregationRuns = SelectedNumberOfRuns;

				RaisePropertyChanged();
				RaisePropertyChanged(nameof(NumberOfAggregationRunsItemsSource));
				RaisePropertyChanged(nameof(SelectedNumberOfAggregationRuns));
			}
		}

		public int SelectedNumberOfAggregationRuns
		{
			get
			{
				return _appConfiguration
				  .SelectedAggregationRuns;
			}
			set
			{
				_appConfiguration.SelectedAggregationRuns =
					value;
				RaisePropertyChanged();
			}
		}

		public bool UseRunHistory
		{
			get
			{
				return _appConfiguration
				  .UseRunHistory;
			}
			set
			{
				_appConfiguration.UseRunHistory =
					value;
				if(!value)
				{
					UseAggregation = false;
				}
				RaisePropertyChanged();
			}
		}

		public bool UseAggregation
		{
			get
			{
				return _appConfiguration
				  .UseAggregation;
			}
			set
			{
				_appConfiguration.UseAggregation =
					value;
				RaisePropertyChanged();
			}
		}

		public IAppConfiguration AppConfiguration => _appConfiguration;

		public Array SecondMetricItems => Enum.GetValues(typeof(EMetric))
											  .Cast<EMetric>()
											  .Where(metric => metric != EMetric.Average && metric != EMetric.None)
											  .ToArray();
		public Array ThirdMetricItems => Enum.GetValues(typeof(EMetric))
											 .Cast<EMetric>()
											 .Where(metric => metric != EMetric.Average)
											 .ToArray();
		public Array NumberOfRunsItemsSource => Enumerable.Range(2, 4).ToArray();

		public Array NumberOfAggregationRunsItemsSource => Enumerable.Range(2, SelectedNumberOfRuns - 1).ToArray();

		public OverlayViewModel(IOverlayService overlayService, IAppConfiguration appConfiguration, IEventAggregator eventAggregator)
		{
			_overlayService = overlayService;
			_appConfiguration = appConfiguration;
			_eventAggregator = eventAggregator;

			OverlayHotkeyString = _appConfiguration.OverlayHotKey.ToString();

			// initialize overlay service
			_overlayService.UpdateNumberOfRuns(SelectedNumberOfRuns);
			_overlayService.SecondMetric = SelectedSecondMetric.ConvertToString();
			_overlayService.ThirdMetric = SelectedThirdMetric.ConvertToString();

			if (IsOverlayActive)
				_overlayService.ShowOverlay();

			_overlayService.IsOverlayActiveStream.OnNext(_appConfiguration.IsOverlayActive);

			SetGlobalHookEventOverlayHotkey();
			SetGlobalHookEventResetHistoryHotkey();
		}

		private void UpdateGlobalOverlayHookEvent()
		{
			if (_globalOverlayHookEvent != null)
			{
				_globalOverlayHookEvent.Dispose();
				SetGlobalHookEventOverlayHotkey();
			}
		}

		private void UpdateGlobalResetHistoryHookEvent()
		{
			if (_globalResetHistoryHookEvent != null)
			{
				_globalResetHistoryHookEvent.Dispose();
				SetGlobalHookEventResetHistoryHotkey();
			}
		}

		private void SetGlobalHookEventOverlayHotkey()
		{
			if (!CXHotkey.IsValidHotkey(OverlayHotkeyString))
				return;

			var onCombinationDictionary = new Dictionary<Combination, Action>
			{
				{Combination.FromString(OverlayHotkeyString), () =>
				{
					SetOverlayMode();
				}}
			};

			_globalOverlayHookEvent = Hook.GlobalEvents();
			_globalOverlayHookEvent.OnCombination(onCombinationDictionary);
		}

		private void SetGlobalHookEventResetHistoryHotkey()
		{
			if (!CXHotkey.IsValidHotkey(ResetHistoryHotkeyString))
				return;

			var onCombinationDictionary = new Dictionary<Combination, Action>
			{
				{Combination.FromString(ResetHistoryHotkeyString), () =>
				{
					_overlayService.ResetHistory();
				}}
			};

			_globalResetHistoryHookEvent = Hook.GlobalEvents();
			_globalResetHistoryHookEvent.OnCombination(onCombinationDictionary);
		}

		private void SetOverlayMode()
		{
			IsOverlayActive = !IsOverlayActive;

			if (IsOverlayActive)
				_overlayService.ShowOverlay();
			else
				_overlayService.HideOverlay();
		}

		public bool IsNavigationTarget(NavigationContext navigationContext)
		{
			return true;
		}

		public void OnNavigatedFrom(NavigationContext navigationContext)
		{
		}

		public void OnNavigatedTo(NavigationContext navigationContext)
		{
		}
	}
}
