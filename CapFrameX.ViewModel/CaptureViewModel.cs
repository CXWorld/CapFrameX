using CapFrameX.Contracts.Configuration;
using CapFrameX.Contracts.PresentMonInterface;
using CapFrameX.OcatInterface;
using CapFrameX.PresentMonInterface;
using Gma.System.MouseKeyHook;
using Prism.Commands;
using Prism.Mvvm;
using Prism.Regions;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
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
        private const int PRESICE_OFFSET = 300;

        private readonly IAppConfiguration _appConfiguration;
        private readonly ICaptureService _captureService;
        private readonly MediaPlayer _soundPlayer = new MediaPlayer();
        private readonly string[] _soundModes = new[] { "none", "simple sounds", "voice response" };
        private readonly EventLoopScheduler _captureStreamScheduler = new EventLoopScheduler();

        private IDisposable _disposableHeartBeat;
        private IDisposable _disposableCaptureStream;
        private List<string> _captureData;
        private string _selectedProcessToCapture;
        private string _selectedProcessToIgnore;
        private bool _isAddToIgnoreListButtonActive = true;
        private bool _isCapturing;
        private bool _isCaptureModeActive = true;
        private string _captureStateInfo = string.Empty;
        private string _captureTimeString = "0";
        private string _captureStartDelayString = "0";
        private IKeyboardMouseEvents _globalHookEvent;
        private string _selectedSoundMode;
        private string _loggerOutput = string.Empty;

        private Stopwatch _hotkeyHandleSetCaptureModeDelay;
        private Stopwatch _soundFileLoadAndPlayDelay;
        private Stopwatch _dataStreamManagementDelay;

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
                OnSelectedProcessToIgnoreChanged();
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

        public IAppConfiguration AppConfiguration => _appConfiguration;

        public string[] SoundModes => _soundModes;

        public ObservableCollection<string> ProcessesToCapture { get; }
            = new ObservableCollection<string>();

        public ObservableCollection<string> ProcessesToIgnore { get; }
            = new ObservableCollection<string>();

        public ICommand AddToIgonreListCommand { get; }

        public ICommand RemoveFromIgnoreListCommand { get; }

        public ICommand ResetCaptureProcessCommand { get; }

        public CaptureViewModel(IAppConfiguration appConfiguration, ICaptureService captureService)
        {
            _appConfiguration = appConfiguration;
            _captureService = captureService;

            AddToIgonreListCommand = new DelegateCommand(OnAddToIgonreList);
            RemoveFromIgnoreListCommand = new DelegateCommand(OnRemoveFromIgnoreList);
            ResetCaptureProcessCommand = new DelegateCommand(OnResetCaptureProcess);

            CaptureStateInfo = $"Service ready... press {CaptureHotkeyString} to start capture of the running process.";
            SelectedSoundMode = _appConfiguration.HotkeySoundMode;

            ProcessesToIgnore.AddRange(CaptureServiceConfiguration.GetProcessIgnoreList());
            _disposableHeartBeat = GetListUpdatHeartBeat();
            SubscribeToGlobalHookEvent();
            StartCaptureService();
        }

        public bool IsNavigationTarget(NavigationContext navigationContext)
        {
            return true;
        }

        public void OnNavigatedFrom(NavigationContext navigationContext)
        {
            _disposableHeartBeat?.Dispose();
            _isCaptureModeActive = false;
            StopCaptureService();
        }

        public void OnNavigatedTo(NavigationContext navigationContext)
        {
            _disposableHeartBeat?.Dispose();
            _disposableHeartBeat = GetListUpdatHeartBeat();
            _isCaptureModeActive = true;
            StartCaptureService();
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
            var onCombinationDictionary = new Dictionary<Combination, Action>
            {
                {Combination.FromString(CaptureHotkeyString), () =>
                {
                    if (_isCaptureModeActive)
                    {
                         _hotkeyHandleSetCaptureModeDelay = new Stopwatch();
                        _hotkeyHandleSetCaptureModeDelay.Start();
                        SetCaptureMode();                       
                    }
                }}
            };

            _globalHookEvent = Hook.GlobalEvents();
            _globalHookEvent.OnCombination(onCombinationDictionary);
        }

        private void SetCaptureMode()
        {
            _hotkeyHandleSetCaptureModeDelay.Stop();
            LoggerOutput += DateTime.UtcNow.ToLongTimeString() + " delay between hotkey handle and call SetCaptureMode method in ms: " + _hotkeyHandleSetCaptureModeDelay.ElapsedMilliseconds + Environment.NewLine;

            _soundFileLoadAndPlayDelay = new Stopwatch();
            _soundFileLoadAndPlayDelay.Start();

            if (!ProcessesToCapture.Any())
            {
                _soundPlayer.Open(new Uri("Sounds/no_process.mp3", UriKind.Relative));
                _soundPlayer.Volume = 0.75;
                _soundPlayer.Play();
                return;
            }

            if (ProcessesToCapture.Count > 1 && string.IsNullOrWhiteSpace(SelectedProcessToCapture))
            {
                _soundPlayer.Open(new Uri("Sounds/more_than_one_process.mp3", UriKind.Relative));
                _soundPlayer.Volume = 0.75;
                _soundPlayer.Play();
                return;
            }

            if (!_isCapturing)
            {
                // none -> do nothing
                // simple sounds
                if (SelectedSoundMode == _soundModes[1])
                {
                    _soundPlayer.Open(new Uri("Sounds/simple_start_sound.mp3", UriKind.Relative));
                    _soundPlayer.Volume = 0.75;
                    _soundPlayer.Play();
                }
                // voice response
                else if (SelectedSoundMode == _soundModes[2])
                {
                    _soundPlayer.Open(new Uri("Sounds/capture_started.mp3", UriKind.Relative));
                    _soundPlayer.Volume = 0.75;
                    _soundPlayer.Play();
                }

                _soundFileLoadAndPlayDelay.Stop();
                LoggerOutput += DateTime.UtcNow.ToLongTimeString() + " delay for loading sound files in ms: " + _soundFileLoadAndPlayDelay.ElapsedMilliseconds + Environment.NewLine;

                _dataStreamManagementDelay = new Stopwatch();
                _dataStreamManagementDelay.Start();

                StartCaptureDataFromStream();

                _isCapturing = !_isCapturing;
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
                FinishCapturingAndUpdateUi();
            }
        }

        private void FinishCapturingAndUpdateUi()
        {
            // none -> do nothing
            // simple sounds
            if (SelectedSoundMode == _soundModes[1])
            {
                _soundPlayer.Open(new Uri("Sounds/simple_stop_sound.mp3", UriKind.Relative));
                _soundPlayer.Volume = 0.75;
                _soundPlayer.Play();
            }
            // voice response
            else if (SelectedSoundMode == _soundModes[2])
            {
                _soundPlayer.Open(new Uri("Sounds/capture_finished.mp3", UriKind.Relative));
                _soundPlayer.Volume = 0.75;
                _soundPlayer.Play();
            }

            _dataStreamManagementDelay = new Stopwatch();
            _dataStreamManagementDelay.Start();

            StopCaptureDataFromStream();

            _isCapturing = !_isCapturing;
            _disposableHeartBeat = GetListUpdatHeartBeat();
            IsAddToIgnoreListButtonActive = true;
            UpdateCaptureStateInfo();
        }

        private void StartCaptureDataFromStream()
        {
            _captureData = new List<string>();
            bool autoTermination = Convert.ToInt32(CaptureTimeString) > 0;
            double delayCapture = Convert.ToInt32(CaptureStartDelayString);
            double captureTime = Convert.ToInt32(CaptureTimeString) + delayCapture;
            bool intializedStartTime = false;
            bool streamStarted = false;
            double startTime = 0;

            _disposableCaptureStream = _captureService.RedirectedOutputDataStream
                .ObserveOn(_captureStreamScheduler).Subscribe(dataLine =>
                {
                    if (string.IsNullOrWhiteSpace(dataLine))
                        return;

                    if (!streamStarted)
                    {
                        _dataStreamManagementDelay.Stop();
                        LoggerOutput += DateTime.UtcNow.ToLongTimeString() + " delay for subscription to first element of capture data stream ms: " + _dataStreamManagementDelay.ElapsedMilliseconds + Environment.NewLine;
                        streamStarted = true;
                    }

                    if (!autoTermination)
                    {
                        _captureData.Add(dataLine);
                    }
                    else
                    {
                        if (!intializedStartTime)
                        {
                            var firstLineSplit = dataLine.Split(',');
                            startTime = Convert.ToDouble(firstLineSplit[11], CultureInfo.InvariantCulture);
                            intializedStartTime = true;
                        }

                        var currentLineSplit = dataLine.Split(',');
                        double currentTime = Convert.ToDouble(currentLineSplit[11], CultureInfo.InvariantCulture);

                        if (currentTime - startTime <= delayCapture)
                            return;

                        if (currentTime - startTime > captureTime)
                        {
                            _captureData.Add(dataLine);

                            Application.Current.Dispatcher.Invoke(new Action(() =>
                            {
                                FinishCapturingAndUpdateUi();
                            }));
                        }
                        else
                        {
                            _captureData.Add(dataLine);
                        }
                    }
                });
        }

        private void StopCaptureDataFromStream()
        {
            _disposableCaptureStream?.Dispose();

            _dataStreamManagementDelay.Stop();
            LoggerOutput += DateTime.UtcNow.ToLongTimeString() + " delay for unsubscription from capture data stream ms: " + _dataStreamManagementDelay.ElapsedMilliseconds + Environment.NewLine;

            // explicit hook, only one process
            if (!string.IsNullOrWhiteSpace(SelectedProcessToCapture))
            {
                Task.Run(() => WriteCaptureDataToFile(SelectedProcessToCapture));
            }
            // auto hook with filtered process list
            else
            {
                var filter = CaptureServiceConfiguration.GetProcessIgnoreList();
                var process = ProcessesToCapture.FirstOrDefault();

                Task.Run(() => WriteCaptureDataToFile(process));
            }
        }

        private void StartCaptureService()
        {
            var serviceConfig = GetRedirectedServiceConfig();
            var startInfo = CaptureServiceConfiguration
                .GetServiceStartInfo(serviceConfig.ConfigParameterToArguments());
            _captureService.StartCaptureService(startInfo);
        }

        private void StopCaptureService() => _captureService.StopCaptureService();

        private string GetOutputFilename(string processName)
        {
            var filename = CaptureServiceConfiguration.GetCaptureFilename(processName);
            string observedDirectory = RecordDirectoryObserver
                .GetInitialObservedDirectory(_appConfiguration.ObservedDirectory);

            return Path.Combine(observedDirectory, filename);
        }

        private void WriteCaptureDataToFile(string processName)
        {
            if (string.IsNullOrWhiteSpace(processName))
                return;

            var filePath = GetOutputFilename(processName);
            var csv = new StringBuilder();
            csv.AppendLine(CaptureServiceConfiguration.FILE_HEADER);

            //additional data/comment
            string firstLineWithInfos = _captureData.First();
            firstLineWithInfos += "," + HardwareInfo.GetProcessorName();
            firstLineWithInfos += "," + HardwareInfo.GetGraphicCardName();
            firstLineWithInfos += "," + HardwareInfo.GetMotherboardName();
            string[] currentLineSplit = firstLineWithInfos.Split(',');

            // normalize time
            currentLineSplit = firstLineWithInfos.Split(',');
            var timeStart = currentLineSplit[11];
            double normalizedTime = Convert.ToDouble(currentLineSplit[11], CultureInfo.InvariantCulture)
                            - Convert.ToDouble(timeStart, CultureInfo.InvariantCulture);
            currentLineSplit[11] = normalizedTime.ToString(CultureInfo.InvariantCulture);

            csv.AppendLine(string.Join(",", currentLineSplit));

            foreach (var dataLine in _captureData.Skip(1))
            {
                int index = dataLine.IndexOf(".exe");
                if (index > 0)
                {
                    var extractedProcessName = dataLine.Substring(0, index);
                    if (extractedProcessName == processName)
                    {
                        currentLineSplit = dataLine.Split(',');

                        // normalize time
                        normalizedTime = Convert.ToDouble(currentLineSplit[11], CultureInfo.InvariantCulture)
                            - Convert.ToDouble(timeStart, CultureInfo.InvariantCulture);

                        // cutting offset
                        int captureTime = Convert.ToInt32(CaptureTimeString);
                        if (captureTime > 0 && normalizedTime > captureTime)
                            break;

                        currentLineSplit[11] = normalizedTime.ToString(CultureInfo.InvariantCulture);

                        csv.AppendLine(string.Join(",", currentLineSplit));
                    }
                }
            }

            using (var sw = new StreamWriter(filePath))
            {
                sw.Write(csv.ToString());
            }
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

        private void OnRemoveFromIgnoreList()
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
                if (ProcessesToCapture.Count <= 1)
                    CaptureStateInfo = $"Service ready... press {CaptureHotkeyString} to start capture.";
                else if (ProcessesToCapture.Count > 1)
                    CaptureStateInfo = $"Service ready... multiple processes detected, select one and press {CaptureHotkeyString} to start capture.";
                return;
            }

            CaptureStateInfo = $"{SelectedProcessToCapture} selected, press {CaptureHotkeyString} to start capture.";
        }

        private void OnSelectedProcessToIgnoreChanged()
        {
            // throw new NotImplementedException();
        }
    }
}
