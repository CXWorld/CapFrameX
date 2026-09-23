using CapFrameX.Contracts.Configuration;
using CapFrameX.Contracts.Localization;
using CapFrameX.Contracts.Data;
using CapFrameX.Contracts.Logging;
using CapFrameX.Contracts.Overlay;
using CapFrameX.Contracts.RTSS;
using CapFrameX.Contracts.Sensor;
using CapFrameX.Data;
using CapFrameX.EventAggregation.Messages;
using CapFrameX.Extensions;
using CapFrameX.Extensions.NetStandard;
using CapFrameX.Hotkey;
using CapFrameX.Monitoring.Contracts;
using CapFrameX.Overlay;
using CapFrameX.PresentMonInterface;
using CapFrameX.Statistics.NetStandard.Contracts;
using Microsoft.Extensions.Logging;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Legends;
using Prism.Commands;
using Prism.Events;
using Prism.Mvvm;
using Prism.Navigation.Regions;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace CapFrameX.ViewModel
{
    public partial class CaptureViewModel : BindableBase, INavigationAware
    {
        private readonly IAppConfiguration _appConfiguration;
        private readonly IEventAggregator _eventAggregator;
        private readonly IRecordManager _recordManager;
        private readonly IOverlayService _overlayService;
        private readonly IOverlayEntryProvider _overlayEntryProvider;
        private readonly ISensorService _sensorService;
        private readonly IOnlineMetricService _onlineMetricService;
        private readonly IStatisticProvider _statisticProvider;
        private readonly ILogger<CaptureViewModel> _logger;
        private readonly ProcessList _processList;
        private readonly SoundManager _soundManager;
        private readonly CaptureManager _captureManager;
        private readonly ISensorConfig _sensorConfig;
        private readonly IRTSSService _rTSSService;
        private readonly ILogEntryManager _logEntryManager;

        private IDisposable _disposableHeartBeat;
        private string _selectedProcessToCapture;
        private string _selectedProcessToIgnore;
        private string _captureStateInfo = string.Empty;
        private string _captureTimeString = "0";
        private string _captureDelayString = "0";
        private string _captureStartDelayString = "0";
        private PlotModel _frametimeModel;
        private string _lastCapturedProcess;
        private string _currentGameNameToCapture = string.Empty;
        private string _currentProcessToCapture = string.Empty;
        private int _lastPublishedProcessId;
        private bool _isLoggerOutputEmpty = true;
        private bool _isUpdatingProcessList;
        private bool _areButtonsActive = true;
        private Dictionary<string, string> _gameFileDescriptionCache = new Dictionary<string, string>();

        private PubSubEvent<ViewMessages.CurrentProcessToCapture> _updateCurrentProcess;

        public string SelectedProcessToCapture
        {
            get { return _selectedProcessToCapture; }
            set
            {
                _selectedProcessToCapture = value;
                RaisePropertyChanged();
                if (!_isUpdatingProcessList)
                    OnSelectedProcessToCaptureChanged();
            }
        }

        public string SelectedProcessToIgnore
        {
            get { return _selectedProcessToIgnore; }
            set
            {
                _selectedProcessToIgnore = value;
                RaisePropertyChanged();
            }
        }

        public bool AreButtonsActive
        {
            get => _areButtonsActive;
            set
            {
                if (SetProperty(ref _areButtonsActive, value))
                    RaisePropertyChanged(nameof(CanRememberGameCaptureTime));
            }
        }

        public bool IsLoggerOutputEmpty
        {
            get { return _isLoggerOutputEmpty; }
            set
            {
                _isLoggerOutputEmpty = value;
                RaisePropertyChanged();
            }
        }

        public string CaptureStateInfo
        {
            get { return TranslateCaptureState(_captureStateInfo); }
            set
            {
                _captureStateInfo = value;
                RaisePropertyChanged();
            }
        }

        public string CaptureDelayString
        {
            get { return _captureDelayString; }
            set
            {
                _captureDelayString = value;

                if (double.TryParse(_captureDelayString, out _))
                    _appConfiguration.CaptureDelay = Convert.ToDouble(value, CultureInfo.InvariantCulture);
                RaisePropertyChanged();
            }
        }

        public string CaptureStartDelayString
        {
            get { return _captureStartDelayString; }
            set
            {
                _captureStartDelayString = value;
                RaisePropertyChanged();
            }
        }

        public string CaptureHotkeyString
        {
            get { return _appConfiguration.CaptureHotKey; }
            set
            {
                if (!CXHotkey.IsValidSetting(value))
                    return;

                _appConfiguration.CaptureHotKey = value;
                UpdateCaptureStateInfo();
                SetGlobalHookEventCaptureHotkey();
                RaisePropertyChanged();
            }
        }

        public PlotModel FrametimeModel
        {
            get { return _frametimeModel; }
            set
            {
                _frametimeModel = value;
                RaisePropertyChanged();
            }
        }

        public string SelectedSoundMode
        {
            get => Enum.GetName(typeof(SoundMode), _soundManager.SoundMode);
            set
            {
                _soundManager.SoundMode = (SoundMode)Enum.Parse(typeof(SoundMode), value);
                RaisePropertyChanged();
                RaisePropertyChanged(nameof(SliderSoundLevel));
                RaisePropertyChanged(nameof(ShowVolumeController));
            }
        }

        public bool ShowVolumeController => _soundManager.SoundMode != SoundMode.None;

        public double SliderSoundLevel
        {
            get => Math.Round(_soundManager.Volume * 100, 0, MidpointRounding.AwayFromZero);
            set
            {
                _soundManager.Volume = value / 100;
                RaisePropertyChanged();
            }
        }

        public bool ShowBasicInfo
        {
            get => _logEntryManager.ShowBasicInfo;
            set
            {
                _logEntryManager.ShowBasicInfo = value;
                RaisePropertyChanged();
            }
        }

        public bool ShowAdvancedInfo
        {
            get => _logEntryManager.ShowAdvancedInfo;
            set
            {
                _logEntryManager.ShowAdvancedInfo = value;
                RaisePropertyChanged();
            }
        }

        public bool ShowErrors
        {
            get => _logEntryManager.ShowErrors;
            set
            {
                _logEntryManager.ShowErrors = value;
                RaisePropertyChanged();
            }
        }

        // Run history and aggregation options
        public string ResetHistoryHotkeyString
        {
            get { return _appConfiguration.ResetHistoryHotkey; }
            set
            {
                if (!CXHotkey.IsValidSetting(value))
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

        public bool AggregationButtonEnabled => UseRunHistory && SelectedNumberOfRuns > 1;

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

        // End of run history and aggregation options

        public string[] SoundModes => _soundManager.AvailableSoundModes;

        public IAppConfiguration AppConfiguration => _appConfiguration;

        // Process discovery is observed on the UI dispatcher. Synchronous collection
        // notifications keep selection changes inside the guarded refresh below.
        public ObservableCollection<string> ProcessesToCapture { get; }
            = new ObservableCollection<string>();

        public ObservableConcurrentCollection<(string, int)> ProcessesInfo { get; }
            = new ObservableConcurrentCollection<(string, int)>();

        public ObservableCollection<string> ProcessesToIgnore { get; }
            = new ObservableCollection<string>();

        public ObservableCollection<ILogEntry> LoggerOutput
            => _logEntryManager.LogEntryOutput;

        public ICommand AddToIgonreListCommand { get; }

        public ICommand AddToProcessListCommand { get; }

        public ICommand ResetPresentMonCommand { get; }

        public ICommand UpdateLogCommand { get; }

        public ICommand ClearLogCommand { get; }

        public ICommand ShowProcessDetailsCommand { get; }

        public Array LoggingPeriodItemsSource => new[] { 250, 500 };

        public CaptureViewModel(IAppConfiguration appConfiguration,
            IEventAggregator eventAggregator,
            IRecordManager recordManager,
            IOverlayService overlayService,
            IOverlayEntryProvider overlayEntryProvider,
            ISensorService sensorService,
            IOnlineMetricService onlineMetricService,
            IStatisticProvider statisticProvider,
            ILogger<CaptureViewModel> logger,
            ProcessList processList,
            SoundManager soundManager,
            CaptureManager captureManager,
            ISensorConfig sensorConfig,
            IRTSSService rTSSService,
            ILogEntryManager logEntryManager)
        {
            _appConfiguration = appConfiguration;
            _eventAggregator = eventAggregator;
            _recordManager = recordManager;
            _overlayService = overlayService;
            _overlayEntryProvider = overlayEntryProvider;
            _sensorService = sensorService;
            _onlineMetricService = onlineMetricService;
            _statisticProvider = statisticProvider;
            _logger = logger;
            _processList = processList;
            _soundManager = soundManager;
            _captureManager = captureManager;
            _sensorConfig = sensorConfig;
            _rTSSService = rTSSService;
            _logEntryManager = logEntryManager;

            AddToIgonreListCommand = new DelegateCommand(OnAddToIgonreList);
            AddToProcessListCommand = new DelegateCommand(OnAddToProcessList);
            ResetPresentMonCommand = new DelegateCommand(OnResetCaptureProcess);
            ShowProcessDetailsCommand = new DelegateCommand(ShowProcessDetails);
            UpdateLogCommand = new DelegateCommand(() => _logEntryManager?.UpdateFilter());
            ClearLogCommand = new DelegateCommand(() => _logEntryManager?.ClearLog());
            UseGlobalCaptureTimeCommand = new DelegateCommand(UseGlobalCaptureDuration, () => AreButtonsActive)
                .ObservesProperty(() => AreButtonsActive);
            RememberGameCaptureTimeCommand = new DelegateCommand(RememberGameCaptureDuration, () => CanRememberGameCaptureTime)
                .ObservesProperty(() => CanRememberGameCaptureTime);

            LoggerOutput.CollectionChanged += (e, x) =>
            {
                IsLoggerOutputEmpty = (x.NewItems?.Count ?? 0) == 0;
            };

            _rTSSService.SetShowRunHistory(UseRunHistory);

            _captureManager
                .CaptureStatusChange
                .ObserveOnDispatcher()
                .Subscribe(status =>
            {
                if (status.Status != null)
                {
                    AreButtonsActive = status.Status == ECaptureStatus.Stopped;

                    if (status.Status == ECaptureStatus.Processing)
                    {
                        CaptureStateInfo = "Creating capture file..." + Environment.NewLine;
                        _overlayService.SetCaptureServiceStatus("Processing data");
                    }
                    else if (status.Status == ECaptureStatus.StartedDelay)
                    {
                        CaptureStateInfo = $"Capture starting with delay of {CaptureDelayString} seconds..." + Environment.NewLine;
                        _overlayService.SetCaptureServiceStatus("Capture starting in");
                    }
                    else
                    {
                        if (status.Status == ECaptureStatus.Stopped)
                            UpdateCaptureStateInfo();
                    }

                    if (status.Status == ECaptureStatus.StartedTimer)
                    {
                        CaptureStateInfo = $"Capturing in progress (Set Time: {CaptureTimeString} seconds)..." + Environment.NewLine
                          + GetCaptureHotkeyHint($"Press {CaptureHotkeyString} to stop capture.");
                    }
                    else if (status.Status == ECaptureStatus.StartedRemote)
                    {
                        CaptureStateInfo = "Remote capturing in progress..." + Environment.NewLine;
                    }
                    else if (status.Status == ECaptureStatus.Started)
                    {
                        CaptureStateInfo = "Capturing in progress..." + Environment.NewLine + GetCaptureHotkeyHint($"Press {CaptureHotkeyString} to stop capture.");
                    }
                }
            });

            _logger.LogDebug("{viewName} Ready", this.GetType().Name);
            CaptureStateInfo = "Service ready..." + Environment.NewLine +
                GetCaptureHotkeyHint($"Press {CaptureHotkeyString} to start capture of the running process.");
            SelectedSoundMode = _appConfiguration.HotkeySoundMode;
            RestoreCaptureTime();
            CaptureDelayString = _appConfiguration.CaptureDelay.ToString(CultureInfo.InvariantCulture);
            _disposableHeartBeat?.Dispose();
            _disposableHeartBeat = GetListUpdateHeartBeat();
            _updateCurrentProcess = _eventAggregator.GetEvent<PubSubEvent<ViewMessages.CurrentProcessToCapture>>(); ;

            SubscribeToUpdateProcessIgnoreList();
            SubscribeToGlobalCaptureHookEvent();
            SetGlobalHookEventResetHistoryHotkey();

            bool captureServiceStarted = _captureManager.RestartCaptureService();

            if (captureServiceStarted)
                _overlayService.SetCaptureServiceStatus("Capture service ready...");


            _eventAggregator.GetEvent<PubSubEvent<ViewMessages.OverlayConfigChanged>>()
                .Subscribe(msg =>
                {
                    OnUseRunHistoryChanged();
                });


            InitializeFrametimeModel();
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

        public void OnSoundLevelChanged()
        {
            _soundManager.PlaySound(Sound.CaptureStarted);
        }

        private void SubscribeToUpdateProcessIgnoreList()
        {
            _processList.ProcessesUpdate
                .StartWith(default(int))
                .ObserveOnDispatcher()
                .Subscribe(_ =>
                {
                    ProcessesToIgnore.Clear();
                    ProcessesToIgnore.AddRange(_processList.GetIgnoredProcessNames());
                    if (!_isCaptureTimeEdited && AreButtonsActive)
                        RestoreCaptureTime();
                });
        }

        private void SubscribeToGlobalCaptureHookEvent()
        {
            SetGlobalHookEventCaptureHotkey();
        }

        private void SetGlobalHookEventCaptureHotkey()
        {
            // No local re-trigger lock: key repeat is filtered centrally for every hotkey now
            // (KeyRepeatFilter). The 500 ms lock this replaces also swallowed deliberate double
            // presses; a start immediately followed by a stop is legitimate and is already
            // guarded by LockCaptureService and the capture state checks in SetCaptureMode.
            HotkeyDictionaryBuilder.SetHotkey(AppConfiguration, HotkeyAction.Capture,
                () =>
                {
                    _logger.LogDebug("Hotkey ({captureHotkeyString}) callback triggered. Lock capture service state is {lockCaptureServiceState}.", CaptureHotkeyString, _captureManager.LockCaptureService);
                    _logger.LogDebug("IsCapturing state: {isCapturingState}", _captureManager.IsCapturing);
                    if (!_captureManager.LockCaptureService)
                    {
                        SetCaptureMode();
                    }
                });
        }

        /// <summary>
        /// Runs a capture start/stop on a thread of its own. Task.Run queues onto the thread pool,
        /// and when the pool is saturated by blocking work (sensor polling, WMI, PresentMon I/O) it
        /// adds threads at roughly one per second, so the hotkey action sat in that queue for
        /// seconds while nothing in the log said why. LongRunning starts a dedicated thread now.
        /// </summary>
        private static void StartCaptureWork(Func<Task> work)
        {
            Task.Factory.StartNew(work, System.Threading.CancellationToken.None,
                TaskCreationOptions.LongRunning, TaskScheduler.Default);
        }

        private void SetCaptureMode()
        {
            if (!ProcessesToCapture.Any())
            {
                _soundManager.PlaySound(Sound.NoProcess);
                _logEntryManager.AddLogEntry("Capture triggered, but no process was detected", ELogMessageType.BasicInfo, true);
                return;
            }
            else if (ProcessesToCapture.Count > 1 && string.IsNullOrWhiteSpace(SelectedProcessToCapture))
            {
                _soundManager.PlaySound(Sound.MoreThanOneProcess);
                _logEntryManager.AddLogEntry("Capture triggered, but multiple proccesses were detected", ELogMessageType.BasicInfo, true);
                return;
            }
            else if (!_captureManager.IsCapturing && !_captureManager.DelayCountdownRunning)
            {
                string processToCapture = SelectedProcessToCapture ?? ProcessesToCapture.FirstOrDefault();
                var processInfo = ProcessesInfo.FirstOrDefault(info => info.Item1 == processToCapture);

                if (!CommitCaptureTime())
                {
                    _logEntryManager.AddLogEntry("Enter a valid capture duration before starting a capture (0 = no limit).", ELogMessageType.Error, true);
                    return;
                }

                // Freeze the selected process and its settings before starting the worker.
                var captureOptions = new CaptureOptions()
                {
                    CaptureTime = GetEffectiveCaptureTime(processToCapture),
                    CaptureDelay = _appConfiguration.CaptureDelay,
                    CaptureFileMode = AppConfiguration.CaptureFileMode,
                    ProcessInfo = processInfo,
                    Remote = false
                };

                StartCaptureWork(async () =>
                {
                    try
                    {
                        await _captureManager.StartCapture(captureOptions);
                    }
                    catch (Exception e)
                    {
                        _logEntryManager.AddLogEntry($"Error: {e.Message}", ELogMessageType.Error, false);
                    }
                });

                _lastCapturedProcess = processToCapture;
            }
            else
            {
                StartCaptureWork(async () =>
                {
                    try
                    {
                        await _captureManager.StopCapture();
                    }
                    catch (Exception e)
                    {
                        _logEntryManager.AddLogEntry($"Error: {e.Message}", ELogMessageType.Error, false);
                    }
                    finally
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            UpdateCaptureStateInfo();
                        });
                    }
                });
            }
        }

        // The ignore list reaches PresentMon as --exclude arguments, so every change to it has to
        // go through a restart of the capture service - which reads the list back in.
        private void OnAddToIgonreList()
        {
            if (SelectedProcessToCapture == null)
                return;

            var process = _processList.Processes
                .FirstOrDefault(p => p.Name == SelectedProcessToCapture);
            if (process is null)
            {
                _processList.AddEntry(SelectedProcessToCapture, null, true);
            }
            else if (process is CXProcess)
            {
                process.Blacklist();
            }
            _processList?.Save();

            SelectedProcessToCapture = null;
            _captureManager.RestartCaptureService();
        }

        private void OnAddToProcessList()
        {
            if (SelectedProcessToIgnore == null)
                return;

            var process = _processList.Processes
                .FirstOrDefault(p => p.Name == SelectedProcessToIgnore);

            if (process is CXProcess)
            {
                process.Whitelist();
                _processList?.Save();
            }

            _captureManager.RestartCaptureService();
        }

        private void OnResetCaptureProcess()
        {
            SelectedProcessToCapture = null;
            _captureManager.RestartCaptureService();
        }

        private IDisposable GetListUpdateHeartBeat()
        {
            return Observable
                .Timer(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(1))
                .ObserveOnDispatcher()
                .Where(x => AreButtonsActive && !_captureManager.IsCapturing && !_captureManager.DelayCountdownRunning)
                .Subscribe(x => UpdateProcessToCaptureList());
        }

        private void UpdateProcessToCaptureList()
        {
            var selectedProcessToCapture = SelectedProcessToCapture;

            var filter = _processList.GetIgnoredProcessNames().ToHashSet();
            var processesInfo = _captureManager.GetAllFilteredProcesses(filter).ToArray();
            var processList = processesInfo.Select(info => info.Item1).ToArray();

            // A ListView clears its selection while its items are replaced. Do not treat
            // that intermediate state as a real process/scope change or commit an edit.
            _isUpdatingProcessList = true;
            try
            {
                foreach (var process in ProcessesToCapture.Where(process => !processList.Contains(process)).ToArray())
                    ProcessesToCapture.Remove(process);
                foreach (var process in processList.Where(process => !ProcessesToCapture.Contains(process)))
                    ProcessesToCapture.Add(process);
                ProcessesInfo.Clear();
                ProcessesInfo.AddFromEnumerable(processesInfo);
                SelectedProcessToCapture = processList.Contains(selectedProcessToCapture)
                    ? selectedProcessToCapture : null;
            }
            finally
            {
                _isUpdatingProcessList = false;
            }

            if (ProcessesToCapture.Any() && !string.IsNullOrWhiteSpace(_lastCapturedProcess))
            {
                if (!ProcessesToCapture.Contains(_lastCapturedProcess) ||
                    (selectedProcessToCapture != null &&
                    selectedProcessToCapture != _lastCapturedProcess))
                {
                    _overlayService.ResetHistory();
                }
            }

            // The capture hotkey is deliberately NOT re-registered here. Its action is a closure
            // over this view model and always reads the current process list, so a detected
            // process never invalidated the registration — while re-registering once per process
            // change used to tear down and re-install the global hook, which drops keystrokes.

            OnSelectedProcessToCaptureChanged();
        }

        private void OnSelectedProcessToCaptureChanged()
        {
            UpdateProcessToCapture();
            UpdateCaptureStateInfo();
        }

        private void UpdateProcessToCapture()
        {
            string currentProcess = null;
            // explicit hook
            if (!string.IsNullOrWhiteSpace(SelectedProcessToCapture))
                currentProcess = SelectedProcessToCapture;
            // auto hook with filtered process list
            else if (ProcessesToCapture.Count == 1)
            {
                currentProcess = ProcessesToCapture.FirstOrDefault();
            }

            if (_currentProcessToCapture != currentProcess)
            {
                // An automatic process switch also finishes a valid edit for the old target.
                CommitCaptureTime();
                GetGameNameFromProcessList(currentProcess);
                _currentProcessToCapture = currentProcess;
                RestoreCaptureTime();
            }

            var processId = ProcessesInfo.FirstOrDefault(info => info.Item1 == currentProcess).Item2;
            if (processId != _lastPublishedProcessId)
            {
                // Every overlay renderer keys its frame feed on this PID; a flicker to 0 silences
                // the hook-free graph and renames its <APP> group, so each change is logged once
                // while extended OSD logging is on.
                if (ExtendedOsdLoggingController.IsVerboseLoggingEnabledInProcess())
                {
                    _logger.LogInformation(
                        "Overlay target process: '{process}' PID {previous} -> {current} (detected {count}, selected '{selected}')",
                        currentProcess ?? "<none>", _lastPublishedProcessId, processId,
                        ProcessesToCapture.Count, SelectedProcessToCapture ?? "<auto>");
                }
                _lastPublishedProcessId = processId;
            }
            _rTSSService.ProcessIdStream.OnNext(processId);
            // The PID stays 0 while several processes wait for a selection; the hook-free overlay
            // only shows itself while the list has entries, so it needs the count as well.
            _rTSSService.ProcessCountStream.OnNext(ProcessesToCapture.Count);

            _updateCurrentProcess?.Publish(new ViewMessages.CurrentProcessToCapture(currentProcess, processId));
        }

        private void ShowProcessDetails()
        {
            if (!string.IsNullOrEmpty(SelectedProcessToCapture))
            {
                var info = ProcessesInfo.FirstOrDefault(x => x.Item1 == SelectedProcessToCapture);
                if (info.Item1 != null)
                {
                    System.Windows.MessageBox.Show(
                        $"Name: {info.Item1}\nProcess-ID (PID): {info.Item2}",
                        "Process details",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Information);
                }
            }
        }

        private void GetGameNameFromProcessList(string processName)
        {
            if (processName == _currentProcessToCapture)
                return;

            string gameName = string.Empty;
            if (!string.IsNullOrWhiteSpace(processName))
                gameName = GetGameNameFromFileDescription(processName);

            if (!string.IsNullOrWhiteSpace(gameName))
                _currentGameNameToCapture = gameName;
            else
                _currentGameNameToCapture = processName;
        }

        private string GetGameNameFromFileDescription(string processName)
        {
            if (string.IsNullOrWhiteSpace(processName))
            {
                return "Unknown";
            }

            var processNameStripped = processName.StripExeExtension();

            if (_gameFileDescriptionCache.ContainsKey(processName))
            {
                return _gameFileDescriptionCache[processName];
            }
            else
            {
                Process[] processes = Process.GetProcessesByName(processNameStripped);

                if (processes.Any())
                {
                    // prefer getting game name from process list
                    var gameName = _processList.FindProcessByName(processName)?.DisplayName;

                    if (gameName != null)
                    {
                        _gameFileDescriptionCache.Add(processName, gameName);
                        return gameName;
                    }

                    try
                    {
                        string mainWindoTitle = processes.First()?.MainWindowTitle?.TrimEnd();
                        string fileDescription = processes.First()?.MainModule?.FileVersionInfo?.FileDescription?.TrimEnd();

                        // prefer file description
                        if (!fileDescription.IsNullOrEmpty())
                        {
                            if (processNameStripped != fileDescription)
                            {
                                _gameFileDescriptionCache.Add(processName, fileDescription);
                                return fileDescription;
                            }
                        }
                        else if (!mainWindoTitle.IsNullOrEmpty())
                        {
                            if (processNameStripped != mainWindoTitle)
                            {
                                _gameFileDescriptionCache.Add(processName, mainWindoTitle);
                                return mainWindoTitle;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error getting game name from process info");
                    }
                }
            }

            return _processList.FindProcessByName(processName)?.DisplayName ?? processNameStripped;
        }

        private static string TranslateCaptureState(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;
            text = System.Text.RegularExpressions.Regex.Replace(text, "auto-detected\\.", CxLang.T("CaptureViewModel_AutoDetected"));
            text = System.Text.RegularExpressions.Regex.Replace(
                text,
                "Press \"(.+?)\" to start capture\\.",
                CxLang.T("CaptureViewModel_Press0ToStartCapture").Replace("{0}", "$1"));
            text = text.Replace("Process list clear.", CxLang.T("CaptureViewModel_ProcessListClear"));
            text = text.Replace("Start any game / application and press", CxLang.T("CaptureViewModel_StartAnyGameApplicationAnd"));
            text = text.Replace("to start capture.", CxLang.T("CaptureViewModel_ToStartCapture"));
            text = text.Replace("selected.", CxLang.T("CaptureViewModel_Selected"));
            text = text.Replace("Multiple processes detected.", CxLang.T("CaptureViewModel_MultipleProcessesDetected"));
            text = text.Replace("Select one or move unwanted processes to ignore list.", CxLang.T("CaptureViewModel_SelectOneOrMoveUnwanted"));
            text = text.Replace("Capture hotkey disabled.", CxLang.T("CaptureViewModel_CaptureHotkeyDisabled"));
            text = text.Replace("to stop capture.", CxLang.T("CaptureViewModel_ToStopCapture"));
            text = text.Replace("Capturing in progress...", CxLang.T("CaptureViewModel_CapturingInProgress"));
            return text;
        }

        private string GetCaptureHotkeyHint(string enabledHint)
        {
            return string.IsNullOrEmpty(CaptureHotkeyString) ? "Capture hotkey disabled." : enabledHint;
        }

        private void UpdateCaptureStateInfo()
        {
            if (string.IsNullOrWhiteSpace(SelectedProcessToCapture))
            {
                if (!ProcessesToCapture.Any())
                {
                    CaptureStateInfo = "Process list clear." + Environment.NewLine + GetCaptureHotkeyHint($"Start any game / application and press \"{CaptureHotkeyString}\" to start capture.");
                    _overlayService.SetCaptureServiceStatus("Scanning for process...");
                }
                else if (ProcessesToCapture.Count == 1 && !_captureManager.DelayCountdownRunning)
                {
                    CaptureStateInfo = $"\"{_currentGameNameToCapture}\" auto-detected." + Environment.NewLine + GetCaptureHotkeyHint($"Press \"{CaptureHotkeyString}\" to start capture.");
                    _overlayService.SetCaptureServiceStatus($"\"{_currentGameNameToCapture}\" ready to capture...");
                }
                else if (ProcessesToCapture.Count > 1)
                {
                    //Multiple processes detected, select the one to capture or move unwanted processes to ignore list.
                    CaptureStateInfo = "Multiple processes detected." + Environment.NewLine + "Select one or move unwanted processes to ignore list.";
                    _overlayService.SetCaptureServiceStatus("Multiple processes detected");
                }
                return;
            }

            if (!_captureManager.DelayCountdownRunning)
            {
                CaptureStateInfo = $"\"{_currentGameNameToCapture}\" selected." + Environment.NewLine + GetCaptureHotkeyHint($"Press \"{CaptureHotkeyString}\" to start capture.");
                _overlayService.SetCaptureServiceStatus($"\"{_currentGameNameToCapture}\" ready to capture...");
            }
        }

        private void InitializeFrametimeModel()
        {
            FrametimeModel = new PlotModel
            {
                PlotMargins = new OxyThickness(40, 0, 0, 40),
                PlotAreaBorderColor = OxyColor.FromArgb(64, 204, 204, 204)
            };

            FrametimeModel.Legends.Add(new Legend()
            {
                LegendPosition = LegendPosition.TopCenter,
                LegendOrientation = LegendOrientation.Horizontal
            });

            //Axes
            //X
            FrametimeModel.Axes.Add(new LinearAxis()
            {
                Key = "xAxis",
                Position = AxisPosition.Bottom,
                Title = CxLang.T("CaptureViewModel_Samples"),
                MajorGridlineStyle = LineStyle.Solid,
                MajorGridlineThickness = 1,
                MajorGridlineColor = OxyColor.FromArgb(64, 204, 204, 204),
                MinorTickSize = 0,
                MajorTickSize = 0
            });

            //Y
            FrametimeModel.Axes.Add(new LinearAxis()
            {
                Key = "yAxis",
                Position = AxisPosition.Left,
                Title = CxLang.T("CaptureViewModel_FrametimeMs"),
                MajorGridlineStyle = LineStyle.Solid,
                MajorGridlineThickness = 1,
                MajorGridlineColor = OxyColor.FromArgb(64, 204, 204, 204),
                MinorTickSize = 0,
                MajorTickSize = 0
            });
        }


        //Run history and aggregation options
        private void OnUseRunHistoryChanged()
        {
            var historyEntry = _overlayEntryProvider.GetOverlayEntry("RunHistory");

            // temp. fix for disabled run history checkbox from previous versions
            if (historyEntry != null)
            {
                historyEntry.ShowOnOverlayIsEnabled = true;
            }

            if (!UseRunHistory)
            {
                UseAggregation = false;
            }
        }

        private void SetGlobalHookEventResetHistoryHotkey()
        {
            HotkeyDictionaryBuilder.SetHotkey(AppConfiguration, HotkeyAction.ResetHistory, () => _overlayService.ResetHistory());
        }
    }
}
