using CapFrameX.Contracts.Configuration;
using CapFrameX.Contracts.Data;
using CapFrameX.Contracts.Overlay;
using CapFrameX.Contracts.PresentMonInterface;
using CapFrameX.Contracts.RTSS;
using CapFrameX.Contracts.Sensor;
using CapFrameX.Data;
using CapFrameX.EventAggregation.Messages;
using CapFrameX.Hotkey;
using CapFrameX.PresentMonInterface;
using CapFrameX.Statistics.NetStandard;
using Microsoft.Extensions.Logging;
using OxyPlot;
using OxyPlot.Axes;
using Prism.Commands;
using Prism.Events;
using Prism.Mvvm;
using Prism.Regions;
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
        private readonly ISensorService _sensorService;
        private readonly IOnlineMetricService _onlineMetricService;
        private readonly IStatisticProvider _statisticProvider;
        private readonly ILogger<CaptureViewModel> _logger;
        private readonly ProcessList _processList;
        private readonly SoundManager _soundManager;
        private readonly CaptureManager _captureManager;
        private readonly ISensorConfig _sensorConfig;
        private readonly IRTSSService _rTSSService;

        private IDisposable _disposableHeartBeat;
        private string _selectedProcessToCapture;
        private string _selectedProcessToIgnore;
        private string _captureStateInfo = string.Empty;
        private string _captureTimeString = "0";
        private string _captureDelayString = "0";
        private string _captureStartDelayString = "0";
        private string _loggerOutput = string.Empty;
        private PlotModel _frametimeModel;
        private string _lastCapturedProcess;
        private bool _hotkeyLocked = false;
        private string _currentGameNameToCapture = string.Empty;
        private string _currentProcessToCapture = string.Empty;

        private PubSubEvent<ViewMessages.CurrentProcessToCapture> _updateCurrentProcess;

        public string SelectedProcessToCapture
        {
            get { return _selectedProcessToCapture; }
            set
            {
                _selectedProcessToCapture = value;
                RaisePropertyChanged();
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

        public bool AreButtonsActive { get; set; } = true;

        public string CaptureStateInfo
        {
            get { return _captureStateInfo; }
            set
            {
                _captureStateInfo = value;
                RaisePropertyChanged();
            }
        }

        public string CaptureTimeString
        {
            get { return _captureTimeString; }
            set
            {
                _captureTimeString = value;

                if (double.TryParse(_captureTimeString, out _))
                    _appConfiguration.CaptureTime = Convert.ToDouble(value, CultureInfo.InvariantCulture);
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
                if (!CXHotkey.IsValidHotkey(value))
                    return;

                _appConfiguration.CaptureHotKey = value;
                UpdateCaptureStateInfo();
                SetGlobalHookEventCaptureHotkey();
                RaisePropertyChanged();
            }
        }

        public string LoggerOutput
        {
            get { return _loggerOutput; }
            set
            {
                _loggerOutput = value;
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
            get => Math.Round(_soundManager.Volume * 100, 0);
            set
            {
                _soundManager.Volume = value / 100;
                RaisePropertyChanged();
            }
        }

        public string[] SoundModes => _soundManager.AvailableSoundModes;

        public IAppConfiguration AppConfiguration => _appConfiguration;

        public ObservableConcurrentCollection<string> ProcessesToCapture { get; }
            = new ObservableConcurrentCollection<string>();

        public ObservableConcurrentCollection<(string, int)> ProcessesInfo { get; }
            = new ObservableConcurrentCollection<(string, int)>();

        public ObservableCollection<string> ProcessesToIgnore { get; }
            = new ObservableCollection<string>();

        public ICommand AddToIgonreListCommand { get; }

        public ICommand AddToProcessListCommand { get; }

        public ICommand ResetPresentMonCommand { get; }

        public Array LoggingPeriodItemsSource => new[] { 250, 500 };

        public CaptureViewModel(IAppConfiguration appConfiguration,
                                IEventAggregator eventAggregator,
                                IRecordManager recordManager,
                                IOverlayService overlayService,
                                ISensorService sensorService,
                                IOnlineMetricService onlineMetricService,
                                IStatisticProvider statisticProvider,
                                ILogger<CaptureViewModel> logger,
                                ProcessList processList,
                                SoundManager soundManager,
                                CaptureManager captureManager,
                                ISensorConfig sensorConfig,
                                IRTSSService rTSSService)
        {
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();

            _appConfiguration = appConfiguration;
            _eventAggregator = eventAggregator;
            _recordManager = recordManager;
            _overlayService = overlayService;
            _sensorService = sensorService;
            _onlineMetricService = onlineMetricService;
            _statisticProvider = statisticProvider;
            _logger = logger;
            _processList = processList;
            _soundManager = soundManager;
            _captureManager = captureManager;
            _sensorConfig = sensorConfig;
            _rTSSService = rTSSService;

            AddToIgonreListCommand = new DelegateCommand(OnAddToIgonreList);
            AddToProcessListCommand = new DelegateCommand(OnAddToProcessList);
            ResetPresentMonCommand = new DelegateCommand(OnResetCaptureProcess);

            //ProcessesToCapture.CollectionChanged += new NotifyCollectionChangedEventHandler
            //    ((sender, eventArg) => UpdateProcessToCapture());

            _captureManager
                .CaptureStatusChange
                .SubscribeOnDispatcher()
                .Subscribe(status =>
            {
                if (status.Status != null)
                {
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
                        AreButtonsActive = status.Status == ECaptureStatus.Stopped;
                        RaisePropertyChanged(nameof(AreButtonsActive));

                        if (status.Status == ECaptureStatus.Stopped)
                            UpdateCaptureStateInfo();
                    }

                    if (status.Status == ECaptureStatus.StartedTimer)
                    {
                        CaptureStateInfo = $"Capturing in progress (Set Time: {CaptureTimeString} seconds)..." + Environment.NewLine
                          + $"Press {CaptureHotkeyString} to stop capture.";
                    }
                    else if (status.Status == ECaptureStatus.StartedRemote)
                    {
                        CaptureStateInfo = "Remote capturing in progress..." + Environment.NewLine;
                    }
                    else if (status.Status == ECaptureStatus.Started)
                    {
                        CaptureStateInfo = "Capturing in progress..." + Environment.NewLine + $"Press {CaptureHotkeyString} to stop capture.";
                    }
                }

                if (status.Message != null)
                {
                    LoggerOutput += $"{DateTime.Now.ToLongTimeString()}: {status.Message}" + Environment.NewLine;
                }
            });

            _logger.LogDebug("{viewName} Ready", this.GetType().Name);
            CaptureStateInfo = "Service ready..." + Environment.NewLine +
                $"Press {CaptureHotkeyString} to start capture of the running process.";
            SelectedSoundMode = _appConfiguration.HotkeySoundMode;
            CaptureTimeString = _appConfiguration.CaptureTime.ToString(CultureInfo.InvariantCulture);
            CaptureDelayString = _appConfiguration.CaptureDelay.ToString(CultureInfo.InvariantCulture);
            _disposableHeartBeat?.Dispose();
            _disposableHeartBeat = GetListUpdatHeartBeat();
            _updateCurrentProcess = _eventAggregator.GetEvent<PubSubEvent<ViewMessages.CurrentProcessToCapture>>(); ;

            SubscribeToUpdateProcessIgnoreList();
            SubscribeToGlobalCaptureHookEvent();

            bool captureServiceStarted = StartCaptureService();

            if (captureServiceStarted)
                _overlayService.SetCaptureServiceStatus("Capture service ready...");

            InitializeFrametimeModel();

            stopwatch.Stop();
            _logger.LogInformation(this.GetType().Name + " {initializationTime}s initialization time", Math.Round(stopwatch.ElapsedMilliseconds * 1E-03, 1));
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
                });
        }

        private void SubscribeToGlobalCaptureHookEvent()
        {
            SetGlobalHookEventCaptureHotkey();
        }

        private void SetGlobalHookEventCaptureHotkey()
        {
            if (!CXHotkey.IsValidHotkey(CaptureHotkeyString))
                return;

            HotkeyDictionaryBuilder.SetHotkey(AppConfiguration, HotkeyAction.Capture, () =>
            {
                if (!_hotkeyLocked)
                {
                    _hotkeyLocked = true;
                    Task.Run(async () => await Task.Delay(250)).ContinueWith(t => _hotkeyLocked = false);
                    _logger.LogInformation("Hotkey ({captureHotkeyString}) callback triggered. Lock capture service state is {lockCaptureServiceState}.", CaptureHotkeyString, _captureManager.LockCaptureService);
                    _logger.LogInformation("IsCapturing state: {isCapturingState}", _captureManager.IsCapturing);
                    if (!_captureManager.LockCaptureService)
                        SetCaptureMode();
                }
            });
        }

        private void SetCaptureMode()
        {
            if (!ProcessesToCapture.Any())
            {
                _soundManager.PlaySound(Sound.NoProcess);
                return;
            }
            else if (ProcessesToCapture.Count > 1 && string.IsNullOrWhiteSpace(SelectedProcessToCapture))
            {
                _soundManager.PlaySound(Sound.MoreThanOneProcess);
                return;
            }
            else if (!_captureManager.IsCapturing && !_captureManager.DelayCountdownRunning)
            {
                string processToCapture = SelectedProcessToCapture ?? ProcessesToCapture.FirstOrDefault();
                var processInfo = ProcessesInfo.FirstOrDefault(info => info.Item1 == processToCapture);

                Task.Run(async () =>
                {
                    try
                    {
                        await _captureManager.StartCapture(new CaptureOptions()
                        {
                            CaptureTime = _appConfiguration.CaptureTime,
                            CaptureDelay = _appConfiguration.CaptureDelay,
                            CaptureFileMode = AppConfiguration.CaptureFileMode,
                            ProcessInfo = processInfo,
                            Remote = false
                        });
                    }
                    catch (Exception e)
                    {
                        LoggerOutput += $"{DateTime.Now.ToLongTimeString()}: Error: {e.Message}" + Environment.NewLine;
                    }
                });

                _lastCapturedProcess = processToCapture;
            }
            else
            {
                Task.Run(async () =>
                {
                    try
                    {
                        await _captureManager.StopCapture();
                    }
                    catch (Exception e)
                    {
                        LoggerOutput += $"{DateTime.Now.ToLongTimeString()}: Error: {e.Message}" + Environment.NewLine;
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

        private bool StartCaptureService()
        {
            bool success;
            var serviceConfig = GetRedirectedServiceConfig();
            var startInfo = CaptureServiceConfiguration
                .GetServiceStartInfo(serviceConfig.ConfigParameterToArguments());
            success = _captureManager.StartCaptureService(startInfo);

            _captureManager.StartFillArchive();

            return success;
        }

        private void StopCaptureService()
        {
            _captureManager.StopFillArchive();
        }

        private ICaptureServiceConfiguration GetRedirectedServiceConfig()
        {
            return new PresentMonServiceConfiguration
            {
                RedirectOutputStream = true,
                ExcludeProcesses = _processList.GetIgnoredProcessNames().ToList()
            };
        }

        private void OnAddToIgonreList()
        {
            if (SelectedProcessToCapture == null)
                return;

            StopCaptureService();

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
            _processList.Save();

            SelectedProcessToCapture = null;
            StartCaptureService();
        }

        private void OnAddToProcessList()
        {
            if (SelectedProcessToIgnore == null)
                return;

            StopCaptureService();
            var process = _processList.Processes
                .FirstOrDefault(p => p.Name == SelectedProcessToIgnore);
            if (process is CXProcess)
            {
                process.Whitelist();
                _processList.Save();
            }
            StartCaptureService();
        }

        private void OnResetCaptureProcess()
        {
            SelectedProcessToCapture = null;
            StopCaptureService();
            StartCaptureService();
        }

        private IDisposable GetListUpdatHeartBeat()
        {
            return Observable
                .Timer(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(1))
                .Where(x => !_captureManager.IsCapturing)
                .ObserveOnDispatcher()
                .Subscribe(x => UpdateProcessToCaptureList());
        }

        private void UpdateProcessToCaptureList()
        {
            var selectedProcessToCapture = SelectedProcessToCapture;
            var backupProcessList = new List<string>(ProcessesToCapture);

            ProcessesToCapture.Clear();
            ProcessesInfo.Clear();

            var filter = _processList.GetIgnoredProcessNames().ToHashSet();
            var processesInfo = _captureManager.GetAllFilteredProcesses(filter);
            var processList = processesInfo.Select(info => info.Item1);

            ProcessesToCapture.AddFromEnumerable(processList);
            ProcessesInfo.AddFromEnumerable(processesInfo);

            if (ProcessesToCapture.Any() && !string.IsNullOrWhiteSpace(_lastCapturedProcess))
            {
                if (!ProcessesToCapture.Contains(_lastCapturedProcess) ||
                    (selectedProcessToCapture != null &&
                    selectedProcessToCapture != _lastCapturedProcess))
                    _overlayService.ResetHistory();
            }

            // fire update global hook if new process is detected
            if (backupProcessList.Count != ProcessesToCapture.Count)
            {
                SetGlobalHookEventCaptureHotkey();
            }

            if (!processList.Contains(selectedProcessToCapture))
                SelectedProcessToCapture = null;
            else
                SelectedProcessToCapture = selectedProcessToCapture;

            UpdateCaptureStateInfo();
        }

        private void OnSelectedProcessToCaptureChanged()
        {
            UpdateCaptureStateInfo();
            UpdateProcessToCapture();
        }

        private void UpdateProcessToCapture()
        {
            string currentProcess;
            // explicit hook, only one process
            if (!string.IsNullOrWhiteSpace(SelectedProcessToCapture))
                currentProcess = SelectedProcessToCapture;
            // auto hook with filtered process list
            else
            {
                currentProcess = ProcessesToCapture.FirstOrDefault();
            }

            GetGameNameFromProcessList(currentProcess);
            _currentProcessToCapture = currentProcess;

            var processId = ProcessesInfo.FirstOrDefault(info => info.Item1 == currentProcess).Item2;
            _rTSSService.ProcessIdStream.OnNext(processId);

            _updateCurrentProcess?.Publish(new ViewMessages.CurrentProcessToCapture(currentProcess, processId));
        }

        private void GetGameNameFromProcessList(string process)
        {
            if (process == _currentProcessToCapture)
                return;

            string gameName = string.Empty;
            if (!string.IsNullOrWhiteSpace(process))
                gameName = _processList.FindProcessByName(process)?.DisplayName;

            if (!string.IsNullOrWhiteSpace(gameName))
                _currentGameNameToCapture = gameName;
            else
                _currentGameNameToCapture = process;
        }

        private void UpdateCaptureStateInfo()
        {
            if (string.IsNullOrWhiteSpace(SelectedProcessToCapture))
            {
                if (!ProcessesToCapture.Any())
                {
                    CaptureStateInfo = "Process list clear." + Environment.NewLine + $"Start any game / application and press  {CaptureHotkeyString} to start capture.";
                    _overlayService.SetCaptureServiceStatus("Scanning for process...");
                }
                else if (ProcessesToCapture.Count == 1 && !_captureManager.DelayCountdownRunning)
                {
                    CaptureStateInfo = "Process auto-detected." + Environment.NewLine + $"Press {CaptureHotkeyString} to start capture.";
                    _overlayService.SetCaptureServiceStatus($"{_currentGameNameToCapture} ready to capture...");
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
                CaptureStateInfo = $"{_currentGameNameToCapture} selected." + Environment.NewLine + $"Press {CaptureHotkeyString} to start capture.";
                _overlayService.SetCaptureServiceStatus($"{SelectedProcessToCapture} ready to capture...");
            }
        }

        private void InitializeFrametimeModel()
        {
            FrametimeModel = new PlotModel
            {
                PlotMargins = new OxyThickness(40, 0, 0, 40),
                PlotAreaBorderColor = OxyColor.FromArgb(64, 204, 204, 204),
                LegendPosition = LegendPosition.TopCenter,
                LegendOrientation = LegendOrientation.Horizontal
            };

            //Axes
            //X
            FrametimeModel.Axes.Add(new LinearAxis()
            {
                Key = "xAxis",
                Position = AxisPosition.Bottom,
                Title = "Samples",
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
                Title = "Frametime [ms]",
                MajorGridlineStyle = LineStyle.Solid,
                MajorGridlineThickness = 1,
                MajorGridlineColor = OxyColor.FromArgb(64, 204, 204, 204),
                MinorTickSize = 0,
                MajorTickSize = 0
            });
        }
    }
}
