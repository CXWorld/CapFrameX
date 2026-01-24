using CapFrameX.Contracts.Configuration;
using CapFrameX.Contracts.Overlay;
using CapFrameX.Contracts.RTSS;
using CapFrameX.Contracts.Sensor;
using CapFrameX.Data;
using CapFrameX.Extensions;
using CapFrameX.Hardware.Controller;
using CapFrameX.Hotkey;
using CapFrameX.MVVM.Dialogs;
using CapFrameX.Overlay;
using CapFrameX.PresentMonInterface;
using CapFrameX.Statistics.NetStandard.Contracts;
using CapFrameX.ViewModel.SubModels;
using DynamicData;
using GongSolutions.Wpf.DragDrop;
using Prism.Commands;
using Prism.Events;
using Prism.Mvvm;
using Prism.Regions;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;

namespace CapFrameX.ViewModel
{
    public class OverlayViewModel : BindableBase, INavigationAware, IDropTarget
    {
        private readonly IOverlayService _overlayService;
        private readonly IOverlayEntryProvider _overlayEntryProvider;
        private readonly IAppConfiguration _appConfiguration;
        private readonly IPathService _pathService;
        private readonly IEventAggregator _eventAggregator;
        private readonly ISensorService _sensorService;
        private readonly IRTSSService _rTSSService;
        private readonly IThreadAffinityController _threadAffinityController;
        private readonly IOnlineMetricService _onlineMetricService;
        private readonly IOverlayTemplateService _overlayTemplateService;
        private int _selectedOverlayEntryIndex = -1;
        private IOverlayEntry _selectedOverlayEntry;
        private IOverlayEntryFormatChange _checkboxes = new OverlayEntryFormatChange();
        private string _updateHpyerlinkText;
        private bool _setSensorTypeButtonEnabled;
        private bool _setGroupButtonEnabled;
        private bool _overlayItemsOptionsEnabled = false;
        private bool _saveButtonIsEnable;
        private Subject<object> _configSubject = new Subject<object>();
        private ResetOverlayConfigDialog _resetOverlayConfigContent;
        private bool _resetOverlayConfigContentIsOpen;
        private string _filterText = string.Empty;
        private EOverlayEntryType? _selectedEntryTypeFilter;
        private ICollectionView _overlayEntriesView;

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

        public bool OSDCustomPosition
        {
            get { return _appConfiguration.OSDCustomPosition; }
            set
            {
                _appConfiguration.OSDCustomPosition = value;
                _rTSSService.SetOSDCustomPosition(value);
                RaisePropertyChanged();
            }
        }
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

        public string ThreadAffinityHotkeyString
        {
            get { return _appConfiguration.ThreadAffinityHotkey; }
            set
            {
                if (!CXHotkey.IsValidHotkey(value))
                    return;

                _appConfiguration.ThreadAffinityHotkey = value;
                SetGlobalHookEventThreadAffinityHotkey();
                RaisePropertyChanged();
            }
        }

        public string ResetMetricsHotkeyString
        {
            get { return _appConfiguration.ResetMetricsHotkey; }
            set
            {
                if (!CXHotkey.IsValidHotkey(value))
                    return;

                _appConfiguration.ResetMetricsHotkey = value;
                SetGlobalHookEventResetMetricsHotkey();
                RaisePropertyChanged();
            }
        }

        public int OSDRefreshPeriod
        {
            get => _appConfiguration.OSDRefreshPeriod;
            set
            {
                _appConfiguration.OSDRefreshPeriod = value;
                _sensorService.SetOSDInterval(TimeSpan.FromMilliseconds(value));
                RaisePropertyChanged();
            }
        }

        public int MetricInterval
        {
            get => _appConfiguration.MetricInterval;
            set
            {
                _appConfiguration.MetricInterval = value;
                _onlineMetricService.SetMetricInterval();
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
                RaisePropertyChanged(nameof(SelectedOverlayItemName));
                RaisePropertyChanged(nameof(SelectedOverlayItemGroupName));
                RaisePropertyChanged(nameof(SelectedOverlayItemSensorType));
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

        public EOverlayTemplate SelectedOverlayTemplate
        {
            get
            {
                return (EOverlayTemplate)_appConfiguration.SelectedOverlayTemplate;
            }
            set
            {
                _appConfiguration.SelectedOverlayTemplate = (int)value;
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

        public ResetOverlayConfigDialog ResetOverlayConfigContent
        {
            get { return _resetOverlayConfigContent; }
            set
            {
                _resetOverlayConfigContent = value;
                RaisePropertyChanged();
            }
        }

        public bool ResetOverlayConfigContentIsOpen
        {
            get { return _resetOverlayConfigContentIsOpen; }
            set
            {
                _resetOverlayConfigContentIsOpen = value;
                RaisePropertyChanged();
            }
        }

        public bool ShowSystemTimeSeconds
        {
            get { return _appConfiguration.ShowSystemTimeSeconds; }
            set
            {
                _appConfiguration.ShowSystemTimeSeconds = value;
                RaisePropertyChanged();
            }
        }

        public bool UseThreadAffinity
        {
            get { return _appConfiguration.UseThreadAffinity; }
            set
            {
                _appConfiguration.UseThreadAffinity = value;
                RaisePropertyChanged();
            }
        }

        public string FilterText
        {
            get => _filterText;
            set
            {
                _filterText = value;
                RaisePropertyChanged();
                RaisePropertyChanged(nameof(HasActiveFilter));
                RefreshOverlayEntriesView();
            }
        }

        public EOverlayEntryType? SelectedEntryTypeFilter
        {
            get => _selectedEntryTypeFilter;
            set
            {
                _selectedEntryTypeFilter = value;
                RaisePropertyChanged();
                RaisePropertyChanged(nameof(HasActiveFilter));
                RefreshOverlayEntriesView();
            }
        }

        public ICollectionView OverlayEntriesView
        {
            get => _overlayEntriesView;
            private set
            {
                _overlayEntriesView = value;
                RaisePropertyChanged();
            }
        }

        public Array EntryTypeFilterItems => new object[] { null }
            .Concat(Enum.GetValues(typeof(EOverlayEntryType)).Cast<object>())
            .ToArray();

        public bool HasActiveFilter
            => !string.IsNullOrWhiteSpace(_filterText) || _selectedEntryTypeFilter.HasValue;

        public string SelectedOverlayItemName
            => SelectedOverlayEntry?.Description;

        public string SelectedOverlayItemGroupName
           => SelectedOverlayEntry?.GroupName;

        public string SelectedOverlayItemSensorType
          => SelectedOverlayEntry == null ? null
          : _sensorService.GetSensorTypeString(SelectedOverlayEntry.Identifier);

        public ICommand ConfigSwitchCommand { get; }

        public ICommand SaveConfigCommand { get; }

        public ICommand SaveToConfig0Command { get; }

        public ICommand SaveToConfig1Command { get; }

        public ICommand SaveToConfig2Command { get; }

        public ICommand OpenResetDialogCommand { get; }

        public ICommand ResetConfigCommand { get; }

        public ICommand ResetColorAndLimitDefaultsCommand { get; }

        public ICommand SetFormatForGroupNameCommand { get; }

        public ICommand SetFormatForSensorTypeCommand { get; }

        public ICommand SetFormatForAllGroupsCommand { get; }

        public ICommand SetFormatForAllValuesCommand { get; }

        public ICommand SetToMinOsdCommand { get; }

        public ICommand ResyncPerfmonCommand { get; }

        public ICommand OpenConfigFolderCommand { get; }

        public ICommand SortByEntryTypeCommand { get; }

        public ICommand ClearFilterCommand { get; }

        public ICommand LaunchOverlayPreviewAppCommand { get; }

        public ICommand ApplyOverlayTemplateCommand { get; }

        public ICommand RevertOverlayTemplateCommand { get; }

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

        public Array MetricIntervalItemsSource => new[] { 5, 10, 20, 30, 60, 120, 240, 300 };

        public Array OverlayTemplateItems => Enum.GetValues(typeof(EOverlayTemplate))
            .Cast<EOverlayTemplate>()
            .ToArray();

        public ObservableCollection<IOverlayEntry> OverlayEntries { get; private set; }
            = new ObservableCollection<IOverlayEntry>();

        public OverlayGroupControl OverlaySubModelGroupControl { get; }

        public OverlayGroupSeparating OverlaySubModelGroupSeparating { get; }

        public OverlayViewModel(IOverlayService overlayService, IOverlayEntryProvider overlayEntryProvider,
            IAppConfiguration appConfiguration, IPathService pathService, IEventAggregator eventAggregator, ISensorService sensorService, IRTSSService rTSSService,
            IThreadAffinityController threadAffinityController, IOnlineMetricService onlineMetricService, IOverlayTemplateService overlayTemplateService)
        {
            _overlayService = overlayService;
            _overlayEntryProvider = overlayEntryProvider;
            _appConfiguration = appConfiguration;
            _pathService = pathService;
            _eventAggregator = eventAggregator;
            _sensorService = sensorService;
            _rTSSService = rTSSService;
            _overlayTemplateService = overlayTemplateService;
            _eventAggregator = eventAggregator;
            _threadAffinityController = threadAffinityController;
            _onlineMetricService = onlineMetricService;

            // define submodels
            OverlaySubModelGroupControl = new OverlayGroupControl(this);
            OverlaySubModelGroupSeparating = new OverlayGroupSeparating(this);

            ResetOverlayConfigContent = new ResetOverlayConfigDialog();

            ConfigSwitchCommand = new DelegateCommand<object>(_configSubject.OnNext);
            _configSubject
                .Select(obj =>
                {
                    return Convert.ToInt32(obj);
                })
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
                    SetupOverlayEntriesView();
                    _overlayEntryProvider.UpdateOverlayEntryFormats();

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

            OpenResetDialogCommand = new DelegateCommand(() => ResetOverlayConfigContentIsOpen = true);
            ResetConfigCommand = new DelegateCommand(async () => await OnResetDefaults());

            SetFormatForGroupNameCommand = new DelegateCommand(
               () => _overlayEntryProvider.SetFormatForGroupName(SelectedOverlayItemGroupName, SelectedOverlayEntry, Checkboxes));

            SetFormatForSensorTypeCommand = new DelegateCommand(
               () => _overlayEntryProvider.SetFormatForSensorType(_sensorService.GetSensorTypeString(SelectedOverlayEntry.Identifier), SelectedOverlayEntry, Checkboxes));

            ResetColorAndLimitDefaultsCommand = new DelegateCommand(
                () => _overlayEntryProvider.ResetColorAndLimits(SelectedOverlayEntry));

            SetFormatForAllGroupsCommand = new DelegateCommand(
               () => _overlayEntryProvider.SetFormatForAllGroups(SelectedOverlayEntry, Checkboxes));

            SetFormatForAllValuesCommand = new DelegateCommand(
               () => _overlayEntryProvider.SetFormatForAllValues(SelectedOverlayEntry, Checkboxes));

            SetToMinOsdCommand = new DelegateCommand(
                () => OnSetMinOsd());

            ResyncPerfmonCommand = new DelegateCommand(
                () => OnResyncPerfmon());

            OpenConfigFolderCommand = new DelegateCommand(OnOpenConfigFolder);
            SortByEntryTypeCommand = new DelegateCommand(OnSortByEntryType);
            ClearFilterCommand = new DelegateCommand(OnClearFilter);
            LaunchOverlayPreviewAppCommand = new DelegateCommand(OnLaunchOverlayPreviewApp);
            ApplyOverlayTemplateCommand = new DelegateCommand(async () => await OnApplyOverlayTemplate());
            RevertOverlayTemplateCommand = new DelegateCommand(async () => await OnRevertOverlayTemplate());

            UpdateHpyerlinkText = "To use the overlay, install the latest" + Environment.NewLine +
                "RivaTuner Statistics Server (RTSS)";

            SetGlobalHookEventOverlayHotkey();
            SetGlobalHookEventOverlayConfigHotkey();
            SetGlobalHookEventThreadAffinityHotkey();
            SetGlobalHookEventResetMetricsHotkey();

            InitializeOSDCustomPosition();
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
            bool wasOverlayActive = _appConfiguration.IsOverlayActive;
            if (wasOverlayActive)
            {
                IsOverlayActive = false;
                // give overlay management a bit time to disable overlay
                await Task.Delay(200);
            }

            try
            {
                var overlayEntries = await _overlayEntryProvider.GetDefaultOverlayEntries();

                OverlaySubModelGroupSeparating.SetOverlayEntries(overlayEntries);
                OverlayEntries.ForEach(entry => entry.Dispose());
                OverlayEntries.Clear();
                OverlayEntries.AddRange(overlayEntries);
                SetupOverlayEntriesView();
                SetSaveButtonIsEnableAction();
                OverlayItemsOptionsEnabled = false;
                _overlayEntryProvider.UpdateOverlayEntryFormats();

                await _overlayEntryProvider.SaveOverlayEntriesToJson(_appConfiguration.OverlayEntryConfigurationFile);
                SaveButtonIsEnable = false;
                ResetOverlayConfigContentIsOpen = false;
            }
            finally
            {
                if (wasOverlayActive)
                    IsOverlayActive = true;
            }
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

        private void OnResyncPerfmon()
        {
            ProcessStartInfo processStartInfo = new ProcessStartInfo("cmd.exe", "/c " + @"Files\bf0822e8-2e55-4b99-82ee-939d8ac2384e.bat")
            {
                CreateNoWindow = false,
                WindowStyle = ProcessWindowStyle.Normal,
                Verb = "runas"
            };

            Process p = new Process
            {
                StartInfo = processStartInfo
            };

            p.Start();
            p.WaitForExit();
        }

        private void SetGlobalHookEventOverlayHotkey()
        {
            if (!CXHotkey.IsValidHotkey(OverlayHotkeyString))
                return;

            HotkeyDictionaryBuilder.SetHotkey(AppConfiguration, HotkeyAction.Overlay, () =>
            {
                IsOverlayActive = !IsOverlayActive;
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

        private void SetGlobalHookEventThreadAffinityHotkey()
        {
            if (!CXHotkey.IsValidHotkey(ThreadAffinityHotkeyString))
                return;

            HotkeyDictionaryBuilder.SetHotkey(AppConfiguration, HotkeyAction.ThreadAffinity, () =>
            {
                Task.Run(() => _threadAffinityController.ToggleAffinity());
            });
        }

        private void SetGlobalHookEventResetMetricsHotkey()
        {
            if (!CXHotkey.IsValidHotkey(ResetMetricsHotkeyString))
                return;

            HotkeyDictionaryBuilder.SetHotkey(AppConfiguration, HotkeyAction.ResetMetrics, () =>
            {
                Task.Run(() => _onlineMetricService.ResetRealtimeMetrics());
            });
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

        private void InitializeOSDCustomPosition()
        {
            _rTSSService.SetOSDCustomPosition(_appConfiguration.OSDCustomPosition);
            _rTSSService.SetOverlayPosition(_appConfiguration.OSDPositionX, _appConfiguration.OSDPositionY);
        }

        private void OnOpenConfigFolder()
        {
            try
            {
                Process.Start(_pathService.ConfigFolder);
            }
            catch { }
        }

        private void OnLaunchOverlayPreviewApp()
        {
            try
            {
                var appDirectory = AppDomain.CurrentDomain.BaseDirectory;
                var vkcubePath = Path.Combine(appDirectory, "3d-test-app", "vkcube.exe");
                if (!File.Exists(vkcubePath))
                    return;

                // --present_mode 2 = VK_PRESENT_MODE_FIFO_KHR (V-Sync, ~60fps)
                // --width/--height = window dimensions
                var startInfo = new ProcessStartInfo
                {
                    FileName = vkcubePath,
                    Arguments = "--present_mode 2 --width 520 --height 820",
                    UseShellExecute = false
                };
                Process.Start(startInfo);
            }
            catch { }
        }

        private async Task OnApplyOverlayTemplate()
        {
            bool wasOverlayActive = _appConfiguration.IsOverlayActive;
            if (wasOverlayActive)
            {
                IsOverlayActive = false;
                // give overlay management a bit time to disable overlay
                await Task.Delay(100);
            }

            // Store current state before applying template
            _overlayTemplateService.StoreCurrentState(OverlayEntries);
            var clonedEntries = OverlayEntries.Select(entry => entry.Clone()).ToList();

            // Apply the selected template
            _overlayTemplateService.ApplyTemplate(SelectedOverlayTemplate, clonedEntries);

            // Sort entries by category order, then by SortKey within each category
            var sortedEntries = clonedEntries
                .OrderBy(entry => GetTemplateSortOrder(entry))
                .ThenBy(entry => entry.SortKey, AlphanumericComparer.Instance)
                .ToList();

            // Dispose old entries and replace with sorted and templated entries
            OverlayEntries.ForEach(entry => entry.Dispose());
            OverlayEntries.Clear();
            OverlayEntries.AddRange(sortedEntries);

            // Update OverlayEntryProvider accordingly
            _overlayEntryProvider.UpdateOverlayEntries(sortedEntries);

            // Setup view, refresh Separators list, and notify overlay
            SetupOverlayEntriesView();
            OverlaySubModelGroupSeparating.SetOverlayEntries(sortedEntries);

            SetSaveButtonIsEnable();

            if (wasOverlayActive)
                IsOverlayActive = true;
        }

        private async Task OnRevertOverlayTemplate()
        {
            bool wasOverlayActive = _appConfiguration.IsOverlayActive;
            if (wasOverlayActive)
            {
                IsOverlayActive = false;
                // give overlay management a bit time to disable overlay
                await Task.Delay(100);
            }

            var storedOverlayEntries = _overlayTemplateService.GetStoredOverlayEntries();

            OverlayEntries.ForEach(entry => entry.Dispose());
            OverlayEntries.Clear();
            OverlayEntries.AddRange(storedOverlayEntries);

            // Update OverlayEntryProvider accordingly
            _overlayEntryProvider.UpdateOverlayEntries(OverlayEntries);

            // Setup view, refresh Separators list, and notify overlay
            SetupOverlayEntriesView();
            OverlaySubModelGroupSeparating.SetOverlayEntries(OverlayEntries);

            SetSaveButtonIsEnable();

            if (wasOverlayActive)
                IsOverlayActive = true;
        }

        private int GetTemplateSortOrder(IOverlayEntry entry)
        {
            // Framerate and Frametime always at the end
            if (entry.Identifier == "Framerate" || entry.Identifier == "Frametime")
                return 8;

            switch (entry.OverlayEntryType)
            {
                case EOverlayEntryType.CX:
                    // CustomCPU and CustomRAM are positioned as section headers in templates
                    if (entry.Identifier == "CustomCPU")
                        return 3;
                    if (entry.Identifier == "CustomRAM")
                        return 5;
                    return 1;

                case EOverlayEntryType.GPU:
                    return 2;

                case EOverlayEntryType.CPU:
                    return 4;
                case EOverlayEntryType.RAM:
                    return 6;
                case EOverlayEntryType.OnlineMetric:
                    return 7;
                default:
                    return 9;
            }
        }

        private void OnSortByEntryType()
        {
            // Sort entries by OverlayEntryType
            // 1. CX
            // 2. GPU
            // 3. CPU
            // 4. RAM
            // 5. OnlineMetric
            // 6. Undefined
            var sortedEntries = OverlayEntries.OrderBy(entry =>
            {
                switch (entry.OverlayEntryType)
                {
                    case EOverlayEntryType.CX:
                        return 1;
                    case EOverlayEntryType.GPU:
                        return 2;
                    case EOverlayEntryType.CPU:
                        return 3;
                    case EOverlayEntryType.RAM:
                        return 4;
                    case EOverlayEntryType.OnlineMetric:
                        return 5;
                    case EOverlayEntryType.Undefined:
                        return 6;
                    default:
                        return 7;
                }
            }).ThenBy(entry => entry.SortKey, new SortKeyComparer()).ToList();

            // Update OverlayEntryProvider accordingly
            _overlayEntryProvider.SortOverlayEntriesByType();

            OverlayEntries.Clear();
            OverlayEntries.AddRange(sortedEntries);
            SetupOverlayEntriesView();
            SetSaveButtonIsEnable();
        }

        private void SetupOverlayEntriesView()
        {
            OverlayEntriesView = CollectionViewSource.GetDefaultView(OverlayEntries);
            OverlayEntriesView.Filter = FilterOverlayEntry;
        }

        private void RefreshOverlayEntriesView()
        {
            OverlayEntriesView?.Refresh();
        }

        private void OnClearFilter()
        {
            FilterText = string.Empty;
            SelectedEntryTypeFilter = null;
        }

        private bool FilterOverlayEntry(object item)
        {
            if (!(item is IOverlayEntry entry))
                return false;

            // Filter by entry type
            if (_selectedEntryTypeFilter.HasValue && entry.OverlayEntryType != _selectedEntryTypeFilter.Value)
                return false;

            // Filter by search text across all columns
            if (!string.IsNullOrWhiteSpace(_filterText))
            {
                var searchText = _filterText.Trim();
                bool matchesDescription = entry.Description?.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0;
                bool matchesGroupName = entry.GroupName?.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0;
                bool matchesIdentifier = entry.Identifier?.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0;

                if (!matchesDescription && !matchesGroupName && !matchesIdentifier)
                    return false;
            }

            return true;
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
            if (HasActiveFilter)
                return;

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
                            SetupOverlayEntriesView();
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
                            SetupOverlayEntriesView();
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
