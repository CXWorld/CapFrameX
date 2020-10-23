using CapFrameX.Contracts.Configuration;
using CapFrameX.Contracts.Data;
using CapFrameX.Contracts.Overlay;
using CapFrameX.Contracts.PresentMonInterface;
using CapFrameX.Contracts.Sensor;
using CapFrameX.Data;
using CapFrameX.EventAggregation.Messages;
using CapFrameX.Hotkey;
using CapFrameX.PresentMonInterface;
using CapFrameX.Statistics.NetStandard;
using Gma.System.MouseKeyHook;
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
using System.Collections.Specialized;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Text;
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

        private IDisposable _disposableHeartBeat;
        private string _selectedProcessToCapture;
        private string _selectedProcessToIgnore;
        private string _captureStateInfo = string.Empty;
        private string _captureTimeString = "0";
        private string _captureStartDelayString = "0";
        private double _captureTime;
        private IKeyboardMouseEvents _globalCaptureHookEvent;
        private string _loggerOutput = string.Empty;
        private PlotModel _frametimeModel;
        private string _lastCapturedProcess;

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

        public double CaptureTime
        {
            get { return _appConfiguration.CaptureTime; }
            set
            {
                _captureTime = value;
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
                UpdateGlobalCaptureHookEvent();
                RaisePropertyChanged();
            }
        }

        public bool UseSensorLogging
        {
            get { return _appConfiguration.UseSensorLogging; }
            set
            {
                _appConfiguration.UseSensorLogging = value;
                _captureManager.ToggleSensorLogging(value);
                RaisePropertyChanged();
            }
        }
        public int LoggingPeriod
        {
            get
            {
                return _appConfiguration
                  .SensorLoggingRefreshPeriod;
            }
            set
            {
                _appConfiguration
                   .SensorLoggingRefreshPeriod = value;
                _sensorService.SetLoggingInterval(TimeSpan.FromMilliseconds(value));
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
                _soundManager.SetSoundMode(value);
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
                                CaptureManager captureManager)
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
            AddToIgonreListCommand = new DelegateCommand(OnAddToIgonreList);
            AddToProcessListCommand = new DelegateCommand(OnAddToProcessList);
            ResetPresentMonCommand = new DelegateCommand(OnResetCaptureProcess);

            ProcessesToCapture.CollectionChanged += new NotifyCollectionChangedEventHandler
            ((sender, eventArg) => UpdateProcessToCapture());

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
                    else
                    {
                        AreButtonsActive = status.Status == ECaptureStatus.Stopped;
                        RaisePropertyChanged(nameof(AreButtonsActive));

                        if (status.Status == ECaptureStatus.Stopped)
                            UpdateCaptureStateInfo();
                    }

                    if (status.Status == ECaptureStatus.StartedRemote)
                    {
                        CaptureStateInfo = "Remote capturing in progress..." + Environment.NewLine;
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
            _processList.ProcessesUpdate.StartWith(default(int)).Subscribe(_ =>
            {
                ProcessesToIgnore.Clear();
                ProcessesToIgnore.AddRange(_processList.GetIgnoredProcessNames());
            });
        }

        private void SubscribeToGlobalCaptureHookEvent()
        {
            SetGlobalHookEventCaptureHotkey();
        }

        private void UpdateGlobalCaptureHookEvent()
        {
            if (_globalCaptureHookEvent != null)
            {
                _globalCaptureHookEvent.Dispose();
                SetGlobalHookEventCaptureHotkey();
            }
        }

        private void SetGlobalHookEventCaptureHotkey()
        {
            if (!CXHotkey.IsValidHotkey(CaptureHotkeyString))
                return;

            var onCombinationDictionary = new Dictionary<CXHotkeyCombination, Action>
            {
                {CXHotkeyCombination.FromString(CaptureHotkeyString), () =>
                {
                    if(!_captureManager.LockCaptureService)
                        SetCaptureMode();
                }}
            };

            _globalCaptureHookEvent = Hook.GlobalEvents();
            _globalCaptureHookEvent.OnCXCombination(onCombinationDictionary);
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
            else if (!_captureManager.IsCapturing)
            {
                _disposableHeartBeat?.Dispose();
                string processToCapture = SelectedProcessToCapture ?? ProcessesToCapture.FirstOrDefault();

                Task.Run(async () =>
                {
                    try
                    {
                        await _captureManager.StartCapture(new CaptureOptions()
                        {
                            CaptureTime = CaptureTime,
                            CaptureFileMode = AppConfiguration.CaptureFileMode,
                            ProcessName = processToCapture,
                            Remote = false
                        });
                    }
                    catch (Exception e)
                    {
                        LoggerOutput += $"{DateTime.Now.ToLongTimeString()}: Error: {e.Message}" + Environment.NewLine;
                    }

                    if (CaptureTimeString == "0" && CaptureStartDelayString == "0")
                        CaptureStateInfo = "Capturing in progress..." + Environment.NewLine + $"Press {CaptureHotkeyString} to stop capture.";

                    if (CaptureTimeString != "0" && CaptureStartDelayString == "0")
                        CaptureStateInfo = $"Capturing in progress (Set Time: {CaptureTimeString} seconds)..." + Environment.NewLine
                           + $"Press {CaptureHotkeyString} to stop capture.";

                    if (CaptureTimeString != "0" && CaptureStartDelayString != "0")
                        CaptureStateInfo = $"Capturing starts with delay of {CaptureStartDelayString} seconds. " +
                            $"Capture will stop after {CaptureTimeString} seconds." + Environment.NewLine + $"Press {CaptureHotkeyString} to stop capture.";
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
                            _disposableHeartBeat?.Dispose();
                            _disposableHeartBeat = GetListUpdatHeartBeat();
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
                .ObserveOnDispatcher()
                .Subscribe(x => UpdateProcessToCaptureList());
        }

        private void UpdateProcessToCaptureList()
        {
            var selectedProcessToCapture = SelectedProcessToCapture;
            var backupProcessList = new List<string>(ProcessesToCapture);

            ProcessesToCapture.Clear();

            var filter = _processList.GetIgnoredProcessNames().ToHashSet();
            var processList = _captureManager.GetAllFilteredProcesses(filter).Distinct();

            ProcessesToCapture.AddFromEnumerable(processList);

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
                UpdateGlobalCaptureHookEvent();
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

            _updateCurrentProcess?.Publish(new ViewMessages.CurrentProcessToCapture(currentProcess));
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
                else if (ProcessesToCapture.Count == 1)
                {
                    CaptureStateInfo = "Process auto-detected." + Environment.NewLine + $"Press {CaptureHotkeyString} to start capture.";
                    _overlayService.SetCaptureServiceStatus("Ready to capture...");
                }
                else if (ProcessesToCapture.Count > 1)
                {
                    //Multiple processes detected, select the one to capture or move unwanted processes to ignore list.
                    CaptureStateInfo = "Multiple processes detected." + Environment.NewLine + "Select one or move unwanted processes to ignore list.";
                    _overlayService.SetCaptureServiceStatus("Multiple processes detected");
                }
                return;
            }

            CaptureStateInfo = $"{SelectedProcessToCapture} selected." + Environment.NewLine + $"Press {CaptureHotkeyString} to start capture.";
            _overlayService.SetCaptureServiceStatus("Ready to capture...");
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
