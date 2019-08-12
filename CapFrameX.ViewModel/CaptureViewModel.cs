using CapFrameX.Contracts.Configuration;
using CapFrameX.Contracts.Data;
using CapFrameX.Contracts.PresentMonInterface;
using CapFrameX.Data;
using CapFrameX.EventAggregation.Messages;
using CapFrameX.Hotkey;
using CapFrameX.OcatInterface;
using CapFrameX.PresentMonInterface;
using Gma.System.MouseKeyHook;
using Prism.Commands;
using Prism.Events;
using Prism.Mvvm;
using Prism.Regions;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace CapFrameX.ViewModel
{
    public class CaptureViewModel : BindableBase, INavigationAware
    {
        private const int PRESICE_OFFSET = 500;
        private const int ARCHIVE_LENGTH = 1000;

        private readonly IAppConfiguration _appConfiguration;
        private readonly ICaptureService _captureService;
        private readonly IEventAggregator _eventAggregator;
        private readonly IRecordDataProvider _recordDataProvider;
        private readonly MediaPlayer _soundPlayer = new MediaPlayer();
        private readonly string[] _soundModes = new[] { "none", "simple sounds", "voice response" };
        private readonly List<string> _captureDataArchive = new List<string>(ARCHIVE_LENGTH);

        private IDisposable _disposableHeartBeat;
        private IDisposable _disposableCaptureStream;
        private IDisposable _disposableArchiveStream;
        private List<string> _captureData;
        private string _selectedProcessToCapture;
        private string _selectedProcessToIgnore;
        private bool _isAddToIgnoreListButtonActive = true;
        private bool _isCapturing;
        private string _captureStateInfo = string.Empty;
        private string _captureTimeString = "0";
        private string _captureStartDelayString = "0";
        private IKeyboardMouseEvents _globalHookEvent;
        private string _selectedSoundMode;
        private string _loggerOutput = string.Empty;
        private bool _fillArchive = false;
        private long _timestampStartCapture;
        private long _timestampStopCapture;
        private long _timestampFirstStreamElement;
        private CancellationTokenSource _cancellationTokenSource;
        private int _sliderSoundLevel;
        private bool _showVolumeController;

        private bool IsCapturing
        {
            get { return _isCapturing; }
            set
            {
                _isCapturing = value;
                _captureService.IsCaptureModeActiveStream.OnNext(value);
            }
        }

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

        public bool IsAddToIgnoreListButtonActive
        {
            get { return _isAddToIgnoreListButtonActive; }
            set
            {
                _isAddToIgnoreListButtonActive = value;
                RaisePropertyChanged();
            }
        }

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

                if (int.TryParse(_captureTimeString, out _))
                    _appConfiguration.CaptureTime = Convert.ToInt32(value);
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
                if (!CaptureHotkey.IsValidHotkey(value))
                    return;

                _appConfiguration.CaptureHotKey = value;
                UpdateCaptureStateInfo();
                UpdateGlobalHookEvent();
                RaisePropertyChanged();
            }
        }

        public string SelectedSoundMode
        {
            get { return _selectedSoundMode; }
            set
            {
                _selectedSoundMode = value;
                _appConfiguration.HotkeySoundMode = value;

                if (value == "simple sounds")
                {
                    SliderSoundLevel = (int)Math.Round(SimpleSoundLevel * 100, 0);
                    ShowVolumeController = true;
                }
                else if (value == "voice response")
                {
                    SliderSoundLevel = (int)Math.Round(VoiceSoundLevel * 100, 0);
                    ShowVolumeController = true;
                }
                else
                {
                    ShowVolumeController = false;
                }

                RaisePropertyChanged();
            }
        }

        public bool ShowVolumeController
        {
            get { return _showVolumeController; }
            set
            {
                _showVolumeController = value;
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

        public int SliderSoundLevel
        {
            get { return _sliderSoundLevel; }
            set
            {
                _sliderSoundLevel = value;
                RaisePropertyChanged();
            }
        }

        public double VoiceSoundLevel
        {
            get { return _appConfiguration.VoiceSoundLevel; }
            set
            {
                _appConfiguration.VoiceSoundLevel = value;
            }
        }

        public double SimpleSoundLevel
        {
            get { return _appConfiguration.SimpleSoundLevel; }
            set
            {
                _appConfiguration.SimpleSoundLevel = value;
            }
        }

        public IAppConfiguration AppConfiguration => _appConfiguration;

        public string[] SoundModes => _soundModes;

        public ObservableCollection<string> ProcessesToCapture { get; }
            = new ObservableCollection<string>();

        public ObservableCollection<string> ProcessesToIgnore { get; }
            = new ObservableCollection<string>();

        public ICommand AddToIgonreListCommand { get; }

        public ICommand AddToProcessListCommand { get; }

        public ICommand ResetCaptureProcessCommand { get; }

        public CaptureViewModel(IAppConfiguration appConfiguration,
                                ICaptureService captureService,
                                IEventAggregator eventAggregator,
                                IRecordDataProvider recordDataProvider)
        {
            _appConfiguration = appConfiguration;
            _captureService = captureService;
            _eventAggregator = eventAggregator;
            _recordDataProvider = recordDataProvider;

            AddToIgonreListCommand = new DelegateCommand(OnAddToIgonreList);
            AddToProcessListCommand = new DelegateCommand(OnAddToProcessList);
            ResetCaptureProcessCommand = new DelegateCommand(OnResetCaptureProcess);

            CaptureStateInfo = $"Service ready... press {CaptureHotkeyString} to start capture of the running process.";
            SelectedSoundMode = _appConfiguration.HotkeySoundMode;
            CaptureTimeString = _appConfiguration.CaptureTime.ToString();

            ProcessesToIgnore.AddRange(CaptureServiceConfiguration.GetProcessIgnoreList());
            _disposableHeartBeat = GetListUpdatHeartBeat();
            SubscribeToUpdateProcessIgnoreList();
            SubscribeToGlobalHookEvent();
            StartCaptureService();

            _captureService.IsCaptureModeActiveStream.OnNext(false);
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
            if (SelectedSoundMode == "simple sounds")
            {
                SimpleSoundLevel = SliderSoundLevel / 100d;

                _soundPlayer.Open(new Uri("Sounds/simple_start_sound.mp3", UriKind.Relative));
                _soundPlayer.Volume = SimpleSoundLevel;
                _soundPlayer.Play();
            }
            else if (SelectedSoundMode == "voice response")
            {
                VoiceSoundLevel = SliderSoundLevel / 100d;

                _soundPlayer.Open(new Uri("Sounds/capture_started.mp3", UriKind.Relative));
                _soundPlayer.Volume = VoiceSoundLevel;
                _soundPlayer.Play();
            }
        }

        private void AddLoggerEntry(string entry)
        {
            LoggerOutput += $"{DateTime.Now.ToLongTimeString()}: {entry}" + Environment.NewLine;
        }

        private void SubscribeToUpdateProcessIgnoreList()
        {
            _eventAggregator.GetEvent<PubSubEvent<ViewMessages.UpdateProcessIgnoreList>>()
                            .Subscribe(msg =>
                            {
                                ProcessesToIgnore.Clear();
                                ProcessesToIgnore.AddRange(CaptureServiceConfiguration.GetProcessIgnoreList());
                            });
        }

        private void SubscribeToGlobalHookEvent()
        {
            SetGlobalHookEventCaptureHotkey();
        }

        private void UpdateGlobalHookEvent()
        {
            if (_globalHookEvent != null)
            {
                _globalHookEvent.Dispose();
                SetGlobalHookEventCaptureHotkey();
            }
        }

        private void SetGlobalHookEventCaptureHotkey()
        {
            if (!CaptureHotkey.IsValidHotkey(CaptureHotkeyString))
                return;

            var onCombinationDictionary = new Dictionary<Combination, Action>
            {
                {Combination.FromString(CaptureHotkeyString), () =>
                {
                    SetCaptureMode();
                }}
            };

            _globalHookEvent = Hook.GlobalEvents();
            _globalHookEvent.OnCombination(onCombinationDictionary);
        }

        private void SetCaptureMode()
        {
            if (!ProcessesToCapture.Any())
            {
                _soundPlayer.Open(new Uri("Sounds/no_process.mp3", UriKind.Relative));
                _soundPlayer.Volume = VoiceSoundLevel;
                _soundPlayer.Play();
                return;
            }

            if (ProcessesToCapture.Count > 1 && string.IsNullOrWhiteSpace(SelectedProcessToCapture))
            {
                _soundPlayer.Open(new Uri("Sounds/more_than_one_process.mp3", UriKind.Relative));
                _soundPlayer.Volume = VoiceSoundLevel;
                _soundPlayer.Play();
                return;
            }

            if (!IsCapturing)
            {
                // none -> do nothing
                // simple sounds
                if (SelectedSoundMode == _soundModes[1])
                {
                    _soundPlayer.Open(new Uri("Sounds/simple_start_sound.mp3", UriKind.Relative));
                    _soundPlayer.Volume = SimpleSoundLevel;
                    _soundPlayer.Play();
                }
                // voice response
                else if (SelectedSoundMode == _soundModes[2])
                {
                    _soundPlayer.Open(new Uri("Sounds/capture_started.mp3", UriKind.Relative));
                    _soundPlayer.Volume = VoiceSoundLevel;
                    _soundPlayer.Play();
                }

                StartCaptureDataFromStream();

                IsCapturing = !IsCapturing;
                _disposableHeartBeat?.Dispose();
                IsAddToIgnoreListButtonActive = false;

                if (CaptureTimeString == "0" && CaptureStartDelayString == "0")
                    CaptureStateInfo = $"Capturing in progress... press {CaptureHotkeyString} to stop capture.";

                if (CaptureTimeString != "0" && CaptureStartDelayString == "0")
                    CaptureStateInfo = $"Capturing in progress. Capture will stop after {CaptureTimeString} seconds." + Environment.NewLine
                       + $"Press {CaptureHotkeyString} to stop capture.";

                if (CaptureTimeString != "0" && CaptureStartDelayString != "0")
                    CaptureStateInfo = $"Capturing starts with delay of {CaptureStartDelayString} seconds. Capture will stop after {CaptureTimeString} seconds." + Environment.NewLine
                         + $"Press {CaptureHotkeyString} to stop capture.";
            }
            else
            {
                _cancellationTokenSource?.Cancel();
                FinishCapturingAndUpdateUi();
            }
        }

        private void FinishCapturingAndUpdateUi()
        {
            _timestampStopCapture = new DateTimeOffset(DateTime.UtcNow).ToUnixTimeMilliseconds();
            _disposableCaptureStream?.Dispose();

            AddLoggerEntry("Capturing stopped.");

            // none -> do nothing
            // simple sounds
            if (SelectedSoundMode == _soundModes[1])
            {
                _soundPlayer.Open(new Uri("Sounds/simple_stop_sound.mp3", UriKind.Relative));
                _soundPlayer.Volume = SimpleSoundLevel;
                _soundPlayer.Play();
            }
            // voice response
            else if (SelectedSoundMode == _soundModes[2])
            {
                _soundPlayer.Open(new Uri("Sounds/capture_finished.mp3", UriKind.Relative));
                _soundPlayer.Volume = VoiceSoundLevel;
                _soundPlayer.Play();
            }

            WriteCaptureDataToFile();

            IsCapturing = !IsCapturing;
            _disposableHeartBeat = GetListUpdatHeartBeat();
            IsAddToIgnoreListButtonActive = true;
            UpdateCaptureStateInfo();
        }


        private async Task SetTaskDelay()
        {
            // put some offset here
            await Task.Delay(TimeSpan.FromMilliseconds(PRESICE_OFFSET +
                1000 * Convert.ToInt32(CaptureTimeString)));
        }

        private void StartCaptureDataFromStream()
        {
            AddLoggerEntry("Capturing started.");

            _captureData = new List<string>();
            bool autoTermination = Convert.ToInt32(CaptureTimeString) > 0;
            double delayCapture = Convert.ToInt32(CaptureStartDelayString);
            double captureTime = Convert.ToInt32(CaptureTimeString) + delayCapture;
            bool intializedStartTime = false;

            var context = TaskScheduler.FromCurrentSynchronizationContext();
            _timestampStartCapture = new DateTimeOffset(DateTime.UtcNow).ToUnixTimeMilliseconds();

            _disposableCaptureStream = _captureService.RedirectedOutputDataStream
                .ObserveOn(new EventLoopScheduler()).Subscribe(dataLine =>
                {
                    if (string.IsNullOrWhiteSpace(dataLine))
                        return;

                    _captureData.Add(dataLine);

                    if (!intializedStartTime)
                    {
                        _timestampFirstStreamElement = new DateTimeOffset(DateTime.UtcNow).ToUnixTimeMilliseconds();
                        intializedStartTime = true;

                        // stop archive
                        _fillArchive = false;
                        _disposableArchiveStream?.Dispose();

                        AddLoggerEntry("Stopped filling archive.");
                    }
                });

            if (autoTermination)
            {
                AddLoggerEntry("Starting countdown...");

                _cancellationTokenSource = new CancellationTokenSource();
                Task.Run(async () =>
                {
                    await SetTaskDelay().ContinueWith(_ =>
                   {
                       Application.Current.Dispatcher.Invoke(new Action(() =>
                       {
                           FinishCapturingAndUpdateUi();
                       }));
                   }, _cancellationTokenSource.Token, TaskContinuationOptions.ExecuteSynchronously, context);
                });
            }
        }

        private void WriteCaptureDataToFile()
        {
            // explicit hook, only one process
            if (!string.IsNullOrWhiteSpace(SelectedProcessToCapture))
            {
                Task.Run(() => WriteExtractedCaptureDataToFile(SelectedProcessToCapture));
            }
            // auto hook with filtered process list
            else
            {
                var filter = CaptureServiceConfiguration.GetProcessIgnoreList();
                var process = ProcessesToCapture.FirstOrDefault();

                Task.Run(() => WriteExtractedCaptureDataToFile(process));
            }
        }

        private void StartCaptureService()
        {
            var serviceConfig = GetRedirectedServiceConfig();
            var startInfo = CaptureServiceConfiguration
                .GetServiceStartInfo(serviceConfig.ConfigParameterToArguments());
            _captureService.StartCaptureService(startInfo);

            StartFillArchive();
        }

        private void StartFillArchive()
        {
            _disposableArchiveStream?.Dispose();
            _fillArchive = true;
            ResetArchive();

            _disposableArchiveStream = _captureService
                .RedirectedOutputDataStream.Where(x => _fillArchive == true).ObserveOn(new EventLoopScheduler())
                .Subscribe(dataLine =>
                {
                    AddDataLineToArchive(dataLine);
                });
        }

        private void StopFillArchive()
        {
            _disposableArchiveStream?.Dispose();
            _fillArchive = false;
            ResetArchive();
        }

        private void StopCaptureService()
        {
            StopFillArchive();
            _captureService.StopCaptureService();
        }

        private string GetOutputFilename(string processName)
        {
            var filename = CaptureServiceConfiguration.GetCaptureFilename(processName);
            string observedDirectory = RecordDirectoryObserver
                .GetInitialObservedDirectory(_appConfiguration.ObservedDirectory);

            return Path.Combine(observedDirectory, filename);
        }

        private void WriteExtractedCaptureDataToFile(string processName)
        {
            if (string.IsNullOrWhiteSpace(processName))
                return;

            var captureData = GetAdjustedCaptureData();
            StartFillArchive();

            if (captureData == null)
            {
                AddLoggerEntry("Error while extracting capture data. No file will be written.");
                return;
            }

            var filePath = GetOutputFilename(processName);
            int captureTime = Convert.ToInt32(CaptureTimeString);
            _recordDataProvider.SavePresentData(captureData, filePath, processName, captureTime);

            AddLoggerEntry("Capture file is successfully written into directory.");
        }

        private List<string> GetAdjustedCaptureData()
        {
            var processName = RecordDataProvider.GetProcessNameFromDataLine(_captureData.First());
            var startTimeWithOffset = RecordDataProvider.GetStartTimeFromDataLine(_captureData.First());
            var captureTime = Convert.ToDouble(CaptureTimeString, CultureInfo.InvariantCulture);

            if (captureTime == 0)
            {
                // ms -> sec
                captureTime = (_timestampStopCapture - _timestampStartCapture) / 1000d;
            }

            AddLoggerEntry($"Recording time (free run or time set) in sec: " +
                            Math.Round(captureTime, 2).ToString(CultureInfo.InvariantCulture));

            var filteredArchive = _captureDataArchive.Where(line => RecordDataProvider.GetProcessNameFromDataLine(line) == processName).ToList();

            AddLoggerEntry($"Using archive with {filteredArchive.Count} frames.");

            // Distinct archive and live stream
            var lastArchiveTime = RecordDataProvider.GetStartTimeFromDataLine(filteredArchive.Last());
            int distinctIndex = 0;
            for (int i = 0; i < _captureData.Count; i++)
            {
                if (RecordDataProvider.GetStartTimeFromDataLine(_captureData[i]) <= lastArchiveTime)
                    distinctIndex++;
                else
                    break;
            }

            if (distinctIndex == 0)
                return null;

            var unionCaptureData = filteredArchive.Concat(_captureData.Skip(distinctIndex)).ToList();

            var captureInterval = new List<string>();
            double leftTimeBound = startTimeWithOffset - (_timestampFirstStreamElement - _timestampStartCapture) / 1000d;
            double rightTimeBound = startTimeWithOffset + captureTime - (_timestampFirstStreamElement - _timestampStartCapture) / 1000d;

            var compensatedDelay = Math.Round((_timestampFirstStreamElement - _timestampStartCapture) / 1000d, 2);

            AddLoggerEntry($"Compensated {compensatedDelay.ToString(CultureInfo.InvariantCulture)} " +
                $"seconds delay with data from archive.");

            for (int i = 0; i < unionCaptureData.Count; i++)
            {
                var currentStartTime = RecordDataProvider.GetStartTimeFromDataLine(unionCaptureData[i]);

                if (currentStartTime >= leftTimeBound && currentStartTime <= rightTimeBound)
                    captureInterval.Add(unionCaptureData[i]);
            }

            return captureInterval;
        }

        private ICaptureServiceConfiguration GetRedirectedServiceConfig()
        {
            return new PresentMonServiceConfiguration
            {
                RedirectOutputStream = true,
                ExcludeProcesses = CaptureServiceConfiguration.GetProcessIgnoreList().ToList()
            };
        }

        private void OnAddToIgonreList()
        {
            if (SelectedProcessToCapture == null)
                return;

            StopCaptureService();
            CaptureServiceConfiguration.AddProcessToIgnoreList(SelectedProcessToCapture);
            ProcessesToIgnore.Clear();
            ProcessesToIgnore.AddRange(CaptureServiceConfiguration.GetProcessIgnoreList());

            SelectedProcessToCapture = null;
            StartCaptureService();
        }


        private void OnAddToProcessList()
        {
            if (SelectedProcessToIgnore == null)
                return;

            StopCaptureService();
            CaptureServiceConfiguration.RemoveProcessFromIgnoreList(SelectedProcessToIgnore);
            ProcessesToIgnore.Clear();
            ProcessesToIgnore.AddRange(CaptureServiceConfiguration.GetProcessIgnoreList());
            StartCaptureService();
        }

        private void OnResetCaptureProcess()
        {
            SelectedProcessToCapture = null;
        }

        private IDisposable GetListUpdatHeartBeat()
        {
            var context = SynchronizationContext.Current;
            return Observable.Generate(0, // dummy initialState
                                        x => true, // dummy condition
                                        x => x, // dummy iterate
                                        x => x, // dummy resultSelector
                                        x => TimeSpan.FromSeconds(1))
                                        .ObserveOn(context)
                                        .SubscribeOn(context)
                                        .Subscribe(x => UpdateProcessToCaptureList());
        }

        private void UpdateProcessToCaptureList()
        {
            var selectedProcessToCapture = SelectedProcessToCapture;
            ProcessesToCapture.Clear();
            var filter = CaptureServiceConfiguration.GetProcessIgnoreList();
            var processList = _captureService.GetAllFilteredProcesses(filter).Distinct();
            ProcessesToCapture.AddRange(processList);

            if (!processList.Contains(selectedProcessToCapture))
                SelectedProcessToCapture = null;
            else
                SelectedProcessToCapture = selectedProcessToCapture;

            Application.Current.Dispatcher.Invoke(new Action(() =>
            {
                UpdateCaptureStateInfo();
            }));
        }

        private void OnSelectedProcessToCaptureChanged()
        {
            UpdateCaptureStateInfo();
        }

        private void UpdateCaptureStateInfo()
        {
            if (string.IsNullOrWhiteSpace(SelectedProcessToCapture))
            {
                if (!ProcessesToCapture.Any())
                    CaptureStateInfo = $"Process list clear... start any game / application and press  {CaptureHotkeyString} to start capture.";
                else if (ProcessesToCapture.Count == 1)
                    CaptureStateInfo = $"Process auto-detected. Press {CaptureHotkeyString} to start capture.";
                else if (ProcessesToCapture.Count > 1)
                    //Multiple processes detected, select the one to capture or move unwanted processes to ignore list.
                    CaptureStateInfo = $"Multiple processes detected, select one and press {CaptureHotkeyString} to start capture or move unwanted processes to ignore list.";
                return;
            }

            CaptureStateInfo = $"{SelectedProcessToCapture} selected, press {CaptureHotkeyString} to start capture.";
        }

        private void AddDataLineToArchive(string dataLine)
        {
            if (_captureDataArchive.Count < ARCHIVE_LENGTH)
            {
                _captureDataArchive.Add(dataLine);
            }
            else
            {
                _captureDataArchive.RemoveAt(0);
                _captureDataArchive.Add(dataLine);
            }
        }

        private void ResetArchive() => _captureDataArchive.Clear();
    }
}
