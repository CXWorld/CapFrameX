using CapFrameX.Contracts.Configuration;
using CapFrameX.Contracts.Overlay;
using CapFrameX.Contracts.Sensor;
using CapFrameX.Extensions.NetStandard;
using CapFrameX.Hotkey;
using CapFrameX.Overlay;
using CapFrameX.Statistics;
using CapFrameX.Statistics.NetStandard.Contracts;
using Gma.System.MouseKeyHook;
using GongSolutions.Wpf.DragDrop;
using Prism.Commands;
using Prism.Events;
using Prism.Mvvm;
using Prism.Regions;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace CapFrameX.ViewModel
{
    public class OverlayViewModel : BindableBase, INavigationAware, IDropTarget
    {
        private readonly IOverlayService _overlayService;
        private readonly IOverlayEntryProvider _overlayEntryProvider;
        private readonly IAppConfiguration _appConfiguration;
        private readonly IEventAggregator _eventAggregator;
        private readonly ISensorService _sensorService;
        private IKeyboardMouseEvents _globalOverlayHookEvent;
        private IKeyboardMouseEvents _globalResetHistoryHookEvent;
        private int _selectedOverlayEntryIndex = -1;
        private string _updateHpyerlinkText;

        public bool IsOverlayActive
        {
            get { return _appConfiguration.IsOverlayActive; }
            set
            {
                if (IsRTSSInstalled)
                {
                    _appConfiguration.IsOverlayActive = value;
                    _overlayService.IsOverlayActiveStream.OnNext(value);
                }

                RaisePropertyChanged();
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
                RaisePropertyChanged();
            }
        }

        public int SelectedOutlierPercentage
        {
            get
            {
                return _appConfiguration
                  .OutlierPercentageOverlay;
            }
            set
            {
                _appConfiguration.OutlierPercentageOverlay =
                    value;
                _overlayService.ResetHistory();
                RaisePropertyChanged();
            }
        }

        public EOutlierHandling SelectedOutlierHandling
        {
            get
            {
                return _appConfiguration
                  .OutlierHandling
                  .ConvertToEnum<EOutlierHandling>();
            }
            set
            {
                _appConfiguration.OutlierHandling =
                    value.ConvertToString();
                _overlayService.ResetHistory();
                RaisePropertyChanged();
            }
        }

        public int OSDRefreshPeriod
        {
            get
            {
                return _appConfiguration
                  .OSDRefreshPeriod;
            }
            set
            {
                _appConfiguration
                   .OSDRefreshPeriod = value;
                _sensorService.SetOSDInterval(TimeSpan.FromMilliseconds(value));
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
                _appConfiguration.UseRunHistory = value;
                OnUseRunHistoryChanged();
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

        public bool SaveAggregationOnly
        {
            get
            {
                return _appConfiguration
                  .SaveAggregationOnly;
            }
            set
            {
                _appConfiguration.SaveAggregationOnly =
                    value;
                RaisePropertyChanged();
            }
        }

        public int SelectedOverlayEntryIndex
        {
            get { return _selectedOverlayEntryIndex; }
            set
            {
                _selectedOverlayEntryIndex = value;
                RaisePropertyChanged();
            }
        }

        public string UpdateHpyerlinkText
        {
            get { return _updateHpyerlinkText; }
            set
            {
                _updateHpyerlinkText = value;
                RaisePropertyChanged();
            }
        }

        public string SelectedRelatedMetric
        {
            get
            {
                return _appConfiguration.RelatedMetricOverlay;
            }
            set
            {
                _appConfiguration.RelatedMetricOverlay = value;
                _overlayService.ResetHistory();
                RaisePropertyChanged();
            }
        }

        public bool IsConfig0Checked
        {
            get
            {
                return _appConfiguration.OverlayEntryConfigurationFile == 0;
            }
            set
            {
                if (value)
                    _appConfiguration.OverlayEntryConfigurationFile = 0;
                RaisePropertyChanged();
            }
        }

        public bool IsConfig1Checked
        {
            get
            {
                return _appConfiguration.OverlayEntryConfigurationFile == 1;
            }
            set
            {
                if (value)
                    _appConfiguration.OverlayEntryConfigurationFile = 1;
                RaisePropertyChanged();
            }
        }

        public bool IsConfig2Checked
        {
            get
            {
                return _appConfiguration.OverlayEntryConfigurationFile == 2;
            }
            set
            {
                if (value)
                    _appConfiguration.OverlayEntryConfigurationFile = 2;
                RaisePropertyChanged();
            }
        }

        public ICommand ConfigSwitchCommand { get; }

        public ICommand SaveConfigCommand { get; }

        public ICommand ResetDefaultsCommand { get; }


        public bool IsRTSSInstalled
            => !string.IsNullOrEmpty(RTSSUtils.GetRTSSFullPath());

        public IAppConfiguration AppConfiguration => _appConfiguration;

        public Array SecondMetricItems => Enum.GetValues(typeof(EMetric))
                                              .Cast<EMetric>()
                                              .Where(metric => metric != EMetric.Average && metric != EMetric.None)
                                              .ToArray();
        public Array ThirdMetricItems => Enum.GetValues(typeof(EMetric))
                                             .Cast<EMetric>()
                                             .Where(metric => metric != EMetric.Average)
                                             .ToArray();

        public Array NumberOfRunsItemsSource => Enumerable.Range(2, 9).ToArray();

        public Array OutlierPercentageItemsSource => Enumerable.Range(2, 9).ToArray();

        public Array OutlierHandlingItems => Enum.GetValues(typeof(EOutlierHandling))
                                                 .Cast<EOutlierHandling>()
                                                 .ToArray();

        public Array RelatedMetricItemsSource => new[] { "Average", "Second", "Third" };

        public Array RefreshPeriodItemsSource => new[] { 500, 1000, 1500, 2000 };

        public ObservableCollection<IOverlayEntry> OverlayEntries { get; private set; }
            = new ObservableCollection<IOverlayEntry>();


        public OverlayViewModel(IOverlayService overlayService, IOverlayEntryProvider overlayEntryProvider,
            IAppConfiguration appConfiguration, IEventAggregator eventAggregator, ISensorService sensorService)
        {
            _overlayService = overlayService;
            _overlayEntryProvider = overlayEntryProvider;
            _appConfiguration = appConfiguration;
            _eventAggregator = eventAggregator;
            _sensorService = sensorService;

            var configSubject = new Subject<object>();
            ConfigSwitchCommand = new DelegateCommand<object>(configSubject.OnNext);
            configSubject
                .Select(obj => Convert.ToInt32(obj)).DistinctUntilChanged()
                .SelectMany(index =>
                {
                    return Observable.FromAsync(() => _overlayEntryProvider.SwitchConfigurationTo(index))
                        .SelectMany(_ => _sensorService.OnDictionaryUpdated.Take(1));
                })
                .StartWith(Enumerable.Empty<IOverlayEntry>())
                .SelectMany(_ => _overlayEntryProvider.GetOverlayEntries())
                .ObserveOnDispatcher()
                .Subscribe(entries =>
                {
                    OverlayEntries.Clear();
                    OverlayEntries.AddRange(entries);
                    OnUseRunHistoryChanged();
                });

            SaveConfigCommand = new DelegateCommand(
                () => _overlayEntryProvider.SaveOverlayEntriesToJson());

            ResetDefaultsCommand = new DelegateCommand(
                async () => await OnResetDefaults());

            UpdateHpyerlinkText = "To use the overlay, install the latest" + Environment.NewLine +
                "RivaTuner  Statistics  Server  (RTSS)";

            SetGlobalHookEventOverlayHotkey();
            SetGlobalHookEventResetHistoryHotkey();
        }

        private async Task OnResetDefaults()
        {
            var overlayEntries = await _overlayEntryProvider.GetDefaultOverlayEntries();
            OverlayEntries.Clear();
            OverlayEntries.AddRange(overlayEntries);
            OnUseRunHistoryChanged();
        }

        private void OnUseRunHistoryChanged()
        {
            var historyEntry = _overlayEntryProvider.GetOverlayEntry("RunHistory");

            if (!UseRunHistory)
            {
                UseAggregation = false;

                // don't show history on overlay
                if (historyEntry != null)
                {
                    historyEntry.ShowOnOverlay = false;
                    historyEntry.ShowOnOverlayIsEnabled = false;
                }
            }
            else
            {
                if (historyEntry != null)
                {
                    historyEntry.ShowOnOverlay = true;
                    historyEntry.ShowOnOverlayIsEnabled = true;
                }
            }
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

            var onCombinationDictionary = new Dictionary<CXHotkeyCombination, Action>
            {
                {CXHotkeyCombination.FromString(OverlayHotkeyString), () =>
                {
                    IsOverlayActive = !IsOverlayActive;
                }}
            };

            _globalOverlayHookEvent = Hook.GlobalEvents();
            _globalOverlayHookEvent.OnCXCombination(onCombinationDictionary);
        }

        private void SetGlobalHookEventResetHistoryHotkey()
        {
            if (!CXHotkey.IsValidHotkey(ResetHistoryHotkeyString))
                return;

            var onCombinationDictionary = new Dictionary<CXHotkeyCombination, Action>
            {
                {CXHotkeyCombination.FromString(ResetHistoryHotkeyString), () =>
                {
                    _overlayService.ResetHistory();
                }}
            };

            _globalResetHistoryHookEvent = Hook.GlobalEvents();
            _globalResetHistoryHookEvent.OnCXCombination(onCombinationDictionary);
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

        async void IDropTarget.Drop(IDropInfo dropInfo)
        {
            if (dropInfo != null)
            {
                if (dropInfo.VisualTarget is FrameworkElement frameworkElement)
                {
                    if (frameworkElement.Name == "OverlayItemDataGrid")
                    {
                        if (dropInfo.Data is IOverlayEntry overlayEntry)
                        {
                            // get source index
                            int sourceIndex = OverlayEntries.IndexOf(overlayEntry);
                            int targetIndex = dropInfo.InsertIndex;

                            _overlayEntryProvider.MoveEntry(sourceIndex, targetIndex);

                            OverlayEntries.Clear();
                            OverlayEntries.AddRange(await _overlayEntryProvider.GetOverlayEntries());
                        }
                        else if (dropInfo.Data is IEnumerable<IOverlayEntry> overlayEntries)
                        {
                            // get source index
                            int count = overlayEntries.Count();
                            int sourceIndex = OverlayEntries.IndexOf(overlayEntries.First());
                            int targetIndex = dropInfo.InsertIndex;

                            if (sourceIndex < targetIndex)
                            {
                                for (int i = 0; i < count; i++)
                                {
                                    _overlayEntryProvider.MoveEntry(sourceIndex, targetIndex - 1);
                                }
                            }
                            else
                            {
                                for (int i = 0; i < count; i++)
                                {
                                    _overlayEntryProvider.MoveEntry(sourceIndex + i, targetIndex);
                                }
                            }

                            OverlayEntries.Clear();
                            OverlayEntries.AddRange(await _overlayEntryProvider.GetOverlayEntries());
                        }
                    }
                }
            }
        }

        void IDropTarget.DragOver(IDropInfo dropInfo)
        {
            if (dropInfo != null)
            {
                // standard behavior
                dropInfo.DropTargetAdorner = DropTargetAdorners.Insert;
                dropInfo.Effects = DragDropEffects.Move;
            }
        }
    }
}
