using CapFrameX.Contracts.Configuration;
using CapFrameX.Contracts.Overlay;
using CapFrameX.Contracts.RTSS;
using CapFrameX.Contracts.Sensor;
using CapFrameX.Data;
using CapFrameX.Extensions;
using CapFrameX.Extensions.NetStandard;
using CapFrameX.Hotkey;
using CapFrameX.Overlay;
using CapFrameX.Statistics.NetStandard.Contracts;
using CapFrameX.ViewModel.SubModels;
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
        private readonly IRTSSService _rTSSService;
        private int _selectedOverlayEntryIndex = -1;
        private IOverlayEntry _selectedOverlayEntry;
        private IOverlayEntryFormatChange _checkboxes = new OverlayEntryFormatChange();
        private string _updateHpyerlinkText;
        private bool _setSensorTypeButtonEnabled;
        private bool _setGroupButtonEnabled;
        private bool _overlayItemsOptionsEnabled = false;
        private bool _saveButtonIsEnable;
        private Subject<object> _configSubject = new Subject<object>();

        public bool OverlayItemsOptionsEnabled
        {
            get { return _overlayItemsOptionsEnabled; }
            set
            {
                _overlayItemsOptionsEnabled = value;
                RaisePropertyChanged();
            }
        }

        public bool SetGroupButtonEnabled
        {
            get => _setGroupButtonEnabled;
            set
            {
                _setGroupButtonEnabled = value;
                RaisePropertyChanged();
            }
        }

        public bool SetSensorTypeButtonEnabled
        {
            get => _setSensorTypeButtonEnabled;
            set
            {
                _setSensorTypeButtonEnabled = value;
                RaisePropertyChanged();
            }
        }

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

        //public bool ToggleGlobalRTSSOSD
        //{
        //    get { return _appConfiguration.ToggleGlobalRTSSOSD; }
        //    set
        //    {
        //        _appConfiguration.ToggleGlobalRTSSOSD = value;
        //        RaisePropertyChanged();
        //    }
        //}

        public int OSDPositionX
        {
            get { return _appConfiguration.OSDPositionX; }
            set
            {

                _appConfiguration.OSDPositionX = value;
                _rTSSService.SetOverlayPosition(value, _appConfiguration.OSDPositionY);
                RaisePropertyChanged();
            }
        }

        public int OSDPositionY
        {
            get { return _appConfiguration.OSDPositionY; }
            set
            {

                _appConfiguration.OSDPositionY = value;
                _rTSSService.SetOverlayPosition(_appConfiguration.OSDPositionX, value);
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
                SetGlobalHookEventOverlayHotkey();
                RaisePropertyChanged();
            }
        }
        public string OverlayConfigHotkeyString
        {
            get { return _appConfiguration.OverlayConfigHotKey; }
            set
            {
                if (!CXHotkey.IsValidHotkey(value))
                    return;

                _appConfiguration.OverlayConfigHotKey = value;
                SetGlobalHookEventOverlayConfigHotkey();
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
                SetGlobalHookEventResetHistoryHotkey();
                RaisePropertyChanged();
            }
        }

        public EMetric SelectedSecondMetric
        {
            get
            {
                return _appConfiguration
                  .RunHistorySecondMetric
                  .ConvertToEnum<EMetric>();
            }
            set
            {
                _appConfiguration.RunHistorySecondMetric =
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
                  .RunHistoryThirdMetric
                  .ConvertToEnum<EMetric>();
            }
            set
            {
                _appConfiguration.RunHistoryThirdMetric =
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
                if (value == 1)
                    UseAggregation = false;
                RaisePropertyChanged(nameof(AggregationButtonEnabled));
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
                _rTSSService.SetShowRunHistory(value);
                OnUseRunHistoryChanged();
                RaisePropertyChanged(nameof(AggregationButtonEnabled));
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


        public bool AggregationButtonEnabled
        {
            get
            {
                return (UseRunHistory && SelectedNumberOfRuns > 1);
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
                OverlayItemsOptionsEnabled = _selectedOverlayEntryIndex > -1 ? true : false;
                RaisePropertyChanged();
                RaisePropertyChanged(nameof(SelectedOverlayItemName));
                RaisePropertyChanged(nameof(SelectedOverlayItemGroupName));
                RaisePropertyChanged(nameof(SelectedOverlayItemSensorType));
            }
        }

        public IOverlayEntry SelectedOverlayEntry
        {
            get { return _selectedOverlayEntry; }
            set
            {
                if (value != null && value.Identifier == "RunHistory")
                    OverlayItemsOptionsEnabled = false;

                _selectedOverlayEntry = value;
                RaisePropertyChanged();
                DetermineMultipleGroupEntries(_selectedOverlayEntry);
                DetermineMultipleSensorTypeEntries(_selectedOverlayEntry);
            }
        }

        public IOverlayEntryFormatChange Checkboxes
        {
            get { return _checkboxes; }
            set
            {
                _checkboxes = value;
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

        public bool SaveButtonIsEnable
        {
            get { return _saveButtonIsEnable; }
            set
            {
                _saveButtonIsEnable = value;
                RaisePropertyChanged();
            }
        }
        public bool AutoDisableOverlay
        {
            get
            {
                return _appConfiguration.AutoDisableOverlay;
            }
            set
            {
                _appConfiguration.AutoDisableOverlay = value;
                RaisePropertyChanged();
            }
        }

        public string SelectedOverlayItemName
            => SelectedOverlayEntryIndex > -1 ?
            OverlayEntries[SelectedOverlayEntryIndex].Description : null;

        public string SelectedOverlayItemGroupName
           => SelectedOverlayEntryIndex > -1 ?
           OverlayEntries[SelectedOverlayEntryIndex].GroupName : null;

        public string SelectedOverlayItemSensorType
          => SelectedOverlayEntryIndex > -1 ?
          _sensorService.GetSensorTypeString(OverlayEntries[SelectedOverlayEntryIndex].Identifier) : null;

        public ICommand ConfigSwitchCommand { get; }

        public ICommand SaveConfigCommand { get; }

        public ICommand SaveToConfig0Command { get; }

        public ICommand SaveToConfig1Command { get; }

        public ICommand SaveToConfig2Command { get; }

        public ICommand ResetDefaultsCommand { get; }

        public ICommand ResetColorAndLimitDefaultsCommand { get; }

        public ICommand SetFormatForGroupNameCommand { get; }

        public ICommand SetFormatForSensorTypeCommand { get; }

        public ICommand SetToMinOsdCommand { get; }

        public bool IsRTSSInstalled
            => _rTSSService.IsRTSSInstalled();

        public IAppConfiguration AppConfiguration => _appConfiguration;

        public Array SecondMetricItems => Enum.GetValues(typeof(EMetric))
                                              .Cast<EMetric>()
                                              .Where(metric => metric != EMetric.Average && metric != EMetric.None)
                                              .ToArray();
        public Array ThirdMetricItems => Enum.GetValues(typeof(EMetric))
                                             .Cast<EMetric>()
                                             .Where(metric => metric != EMetric.Average)
                                             .ToArray();

        public Array NumberOfRunsItemsSource => Enumerable.Range(1, 20).ToArray();

        public Array OutlierPercentageItemsSource => Enumerable.Range(1, 9).ToArray();

        public Array OutlierHandlingItems => Enum.GetValues(typeof(EOutlierHandling))
                                                 .Cast<EOutlierHandling>()
                                                 .ToArray();

        public Array RelatedMetricItemsSource => new[] { "Average", "Second", "Third" };

        public Array RefreshPeriodItemsSource => new[] { 500, 1000, 1500, 2000 };

        public ObservableCollection<IOverlayEntry> OverlayEntries { get; private set; }
            = new ObservableCollection<IOverlayEntry>();

        public OverlayGroupControl OverlaySubModelGroupControl { get; }

        public OverlayGroupSeparating OverlaySubModelGroupSeparating { get; }

        public OverlayViewModel(IOverlayService overlayService, IOverlayEntryProvider overlayEntryProvider,
            IAppConfiguration appConfiguration, IEventAggregator eventAggregator, ISensorService sensorService, IRTSSService rTSSService)
        {
            _overlayService = overlayService;
            _overlayEntryProvider = overlayEntryProvider;
            _appConfiguration = appConfiguration;
            _eventAggregator = eventAggregator;
            _sensorService = sensorService;
            _rTSSService = rTSSService;

            // define submodels
            OverlaySubModelGroupControl = new OverlayGroupControl(this);
            OverlaySubModelGroupSeparating = new OverlayGroupSeparating(this);

            _rTSSService.SetShowRunHistory(UseRunHistory);

            ConfigSwitchCommand = new DelegateCommand<object>(_configSubject.OnNext);
            _configSubject
                .Select(obj =>
                {
                    return Convert.ToInt32(obj);
                })
                .DistinctUntilChanged()
                .SelectMany(index =>
                {
                    return Observable.FromAsync(() => Task.Run(() => _overlayEntryProvider.SwitchConfigurationTo(index)))
                        .SelectMany(_ => _overlayService.OnDictionaryUpdated.Take(1));
                })
                .StartWith(Enumerable.Empty<IOverlayEntry>())
                .SelectMany(_ => overlayEntryProvider.GetOverlayEntries(false))
                .ObserveOnDispatcher()
                .Subscribe(entries =>
                {
                    entries.ForEach(entry => entry.UpdateGroupName = OverlaySubModelGroupSeparating.UpdateGroupName);
                    OverlaySubModelGroupSeparating.SetOverlayEntries(entries);

                    OverlayEntries.Clear();
                    OverlayEntries.AddRange(entries);
                    OnUseRunHistoryChanged();

                    SetSaveButtonIsEnableAction();
                    SaveButtonIsEnable = _overlayEntryProvider.HasHardwareChanged;
                });

            SaveConfigCommand = new DelegateCommand<object>(
               async (object targetConfig) =>
               {
                   int index = Convert.ToInt32(targetConfig);
                   if (index == 3)
                   {
                       SaveButtonIsEnable = false;
                       await _overlayEntryProvider.SaveOverlayEntriesToJson(_appConfiguration.OverlayEntryConfigurationFile);
                   }
                   else
                   {
                       await _overlayEntryProvider.SaveOverlayEntriesToJson(index);
                   }
               });

            ResetDefaultsCommand = new DelegateCommand(
                 async () => await OnResetDefaults());

            SetFormatForGroupNameCommand = new DelegateCommand(
               () => _overlayEntryProvider.SetFormatForGroupName(SelectedOverlayItemGroupName, SelectedOverlayEntry, Checkboxes));

            SetFormatForSensorTypeCommand = new DelegateCommand(
               () => _overlayEntryProvider.SetFormatForSensorType(_sensorService.GetSensorTypeString(SelectedOverlayEntry.Identifier), SelectedOverlayEntry, Checkboxes));

            ResetColorAndLimitDefaultsCommand = new DelegateCommand(
                () => _overlayEntryProvider.ResetColorAndLimits(SelectedOverlayEntry));

            SetToMinOsdCommand = new DelegateCommand(
                () => OnSetMinOsd());

            UpdateHpyerlinkText = "To use the overlay, install the latest" + Environment.NewLine +
                "RivaTuner Statistics Server (RTSS)";

            SetGlobalHookEventOverlayHotkey();
            SetGlobalHookEventOverlayConfigHotkey();
            SetGlobalHookEventResetHistoryHotkey();

        }

        private void SetSaveButtonIsEnableAction()
        {
            OverlayEntries.ForEach(entry => entry.PropertyChangedAction = SetSaveButtonIsEnable);
            OverlaySubModelGroupSeparating.OverlayGroupNameSeparatorEntries.ForEach(entry => entry.PropertyChangedAction = SetSaveButtonIsEnable);
        }

        private void SetSaveButtonIsEnable()
            => SaveButtonIsEnable = true;

        private async Task OnResetDefaults()
        {
            var overlayEntries = await _overlayEntryProvider.GetDefaultOverlayEntries();
            OverlaySubModelGroupSeparating.SetOverlayEntries(overlayEntries);
            OverlayEntries.Clear();
            OverlayEntries.AddRange(overlayEntries);
            SetSaveButtonIsEnableAction();
            OnUseRunHistoryChanged();
            OverlayItemsOptionsEnabled = false;
        }

        private void OnSetMinOsd()
        {
            foreach (var entry in OverlayEntries)
            {
                switch (entry.Identifier)
                {
                    case "CaptureTimer":
                    case "Framerate":
                    case "Frametime":
                        entry.ShowOnOverlay = true;
                        break;
                    default:
                        entry.ShowOnOverlay = false;
                        break;
                }
            }
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

        private void SetGlobalHookEventOverlayHotkey()
        {
            if (!CXHotkey.IsValidHotkey(OverlayHotkeyString))
                return;

            HotkeyDictionaryBuilder.SetHotkey(AppConfiguration, HotkeyAction.Overlay, () =>
            {
                IsOverlayActive = !IsOverlayActive;

                //if (_appConfiguration.ToggleGlobalRTSSOSD && !IsOverlayActive)
                //    _rTSSService.OnOSDOff();
                //if (_appConfiguration.ToggleGlobalRTSSOSD && IsOverlayActive)
                //    _rTSSService.OnOSDOn();
            });
        }

        private void SetGlobalHookEventOverlayConfigHotkey()
        {
            if (!CXHotkey.IsValidHotkey(OverlayConfigHotkeyString))
                return;

            HotkeyDictionaryBuilder.SetHotkey(AppConfiguration, HotkeyAction.OverlayConfig, () =>
            {
                var nextConfig = GetNextConfig();
                Task.Run(() => _configSubject.OnNext(nextConfig));
            });
        }

        private void SetGlobalHookEventResetHistoryHotkey()
        {
            if (!CXHotkey.IsValidHotkey(ResetHistoryHotkeyString))
                return;

            HotkeyDictionaryBuilder.SetHotkey(AppConfiguration, HotkeyAction.ResetHistory, () => _overlayService.ResetHistory());
        }

        private string GetNextConfig()
        {
            if (IsConfig0Checked)
            {
                IsConfig1Checked = true;
                return "1";
            }

            else if (IsConfig1Checked)
            {
                IsConfig2Checked = true;
                return "2";
            }
            else
            {
                IsConfig0Checked = true;
                return "0";
            }
        }

        public void UpdateGroupNameEnable()
        {
            RaisePropertyChanged(nameof(SelectedOverlayItemGroupName));
            DetermineMultipleGroupEntries(_selectedOverlayEntry);
        }

        public void DetermineMultipleGroupEntries(IOverlayEntry selectedEntry)
        {
            if (selectedEntry == null)
                return;

            SetGroupButtonEnabled =
                OverlayEntries.Count(entry => entry.GroupName == selectedEntry.GroupName) > 1;
        }

        public void DetermineMultipleSensorTypeEntries(IOverlayEntry selectedEntry)
        {
            if (selectedEntry == null)
                return;

            string selectedSensorType = _sensorService.GetSensorTypeString(selectedEntry.Identifier);
            SetSensorTypeButtonEnabled = selectedSensorType != string.Empty &&
                OverlayEntries.Count((entry => _sensorService.GetSensorTypeString(entry.Identifier) == selectedSensorType)) > 1;
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

                            // move downwards
                            if (sourceIndex < targetIndex)
                            {
                                _overlayEntryProvider.MoveEntry(sourceIndex, targetIndex - 1);
                            }
                            // moving upwards
                            else
                            {
                                _overlayEntryProvider.MoveEntry(sourceIndex, targetIndex);
                            }

                            OverlayEntries.Clear();
                            OverlayEntries.AddRange(await _overlayEntryProvider.GetOverlayEntries());
                        }
                        else if (dropInfo.Data is IEnumerable<IOverlayEntry> overlayEntries)
                        {
                            // get source index
                            int count = overlayEntries.Count();
                            int sourceIndex = overlayEntries.Min(entry => OverlayEntries.IndexOf(entry));
                            int targetIndex = dropInfo.InsertIndex;

                            // move downwards
                            if (sourceIndex < targetIndex)
                            {
                                for (int i = 0; i < count; i++)
                                {
                                    _overlayEntryProvider.MoveEntry(sourceIndex, targetIndex - 1);
                                }
                            }
                            // moving upwards
                            else
                            {
                                for (int i = 0; i < count; i++)
                                {
                                    _overlayEntryProvider.MoveEntry(sourceIndex + i, targetIndex + i);
                                }
                            }

                            OverlayEntries.Clear();
                            OverlayEntries.AddRange(await _overlayEntryProvider.GetOverlayEntries());
                        }

                        SetSaveButtonIsEnable();
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
