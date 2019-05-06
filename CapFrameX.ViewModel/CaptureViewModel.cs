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
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace CapFrameX.ViewModel
{
    public class CaptureViewModel : BindableBase, INavigationAware
    {
        private readonly IAppConfiguration _appConfiguration;
        private readonly ICaptureService _captureService;

        private IDisposable _disposableSequence;
        private string _selectedProcessToCapture;
        private string _selectedProcessToIgnore;
        private bool _isAddToIgnoreListButtonActive = true;
        private bool _isCapturing;
        private bool _isCaptureModeActive = true;
        private string _captureStateInfo = string.Empty;
        private string _captureTimeString = "0";
        private string _captureStartDelayString = "0";
        private string _captureHotkeyString = "F12";
        private IKeyboardMouseEvents _globalHookEvent;

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
            get { return _captureHotkeyString; }
            set
            {
                _captureHotkeyString = value;
                UpdateCaptureStateInfo();
                UpdateGlobalHookEvent();
                RaisePropertyChanged();
            }
        }

        public IAppConfiguration AppConfiguration => _appConfiguration;

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

            CaptureStateInfo = $"Capturing inactive... select process and press {CaptureHotkeyString} to start.";

            ProcessesToIgnore.AddRange(CaptureServiceConfiguration.GetProcessIgnoreList());
            _disposableSequence = GetListUpdatHeartBeat();
            SubscribeToGlobalHookEvent();
        }

        public bool IsNavigationTarget(NavigationContext navigationContext)
        {
            return true;
        }

        public void OnNavigatedFrom(NavigationContext navigationContext)
        {
            _disposableSequence?.Dispose();
            _isCaptureModeActive = false;
        }

        public void OnNavigatedTo(NavigationContext navigationContext)
        {
            _disposableSequence?.Dispose();
            _disposableSequence = GetListUpdatHeartBeat();
            _isCaptureModeActive = true;
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
                        SetCaptureMode();
                }}
            };

            _globalHookEvent = Hook.GlobalEvents();
            _globalHookEvent.OnCombination(onCombinationDictionary);
        }

        private void SetCaptureMode()
        {
            if (!_isCapturing)
            {
                _isCapturing = !_isCapturing;
                IsAddToIgnoreListButtonActive = false;

                if (string.IsNullOrWhiteSpace(SelectedProcessToCapture))
                {
                    _isCapturing = !_isCapturing;
                    IsAddToIgnoreListButtonActive = true;
                    return;
                }

                _disposableSequence?.Dispose();

                CaptureStateInfo = $"Capturing started... press {CaptureHotkeyString} to stop.";



                System.Media.SystemSounds.Beep.Play();
                

                var context = TaskScheduler.FromCurrentSynchronizationContext();

                if (Convert.ToInt32(CaptureTimeString) > 0)
                {
                    Task.Run(async () =>
                    {
                        await PutTaskDelay().ContinueWith(_ => StopCaptureService(),
                            CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, context);
                    });
                }
            }
            else
            {
                StopCaptureService();
            }
        }

        private string GetOutputFilename()
        {
            var filename = CaptureServiceConfiguration.GetCaptureFilename(SelectedProcessToCapture);
            string observedDirectory = RecordDirectoryObserver.GetInitialObservedDirectory(_appConfiguration.ObservedDirectory);

            return Path.Combine(observedDirectory, filename);
        }

        private ICaptureServiceConfiguration GetRedirectedServiceConfig()
        {
            return new PresentMonServiceConfiguration
            {
                RedirectOutputStream = true,
                ProcessName = SelectedProcessToCapture + ".exe"
            };
        }

        private async Task PutTaskDelay()
        {
            await Task.Delay(TimeSpan.FromSeconds(Convert.ToInt32(CaptureTimeString)));
        }

        private void StopCaptureService()
        {
            _isCapturing = !_isCapturing;
            System.Media.SystemSounds.Beep.Play();
            _captureService.StopCaptureService();
            UpdateCaptureStateInfo();
            IsAddToIgnoreListButtonActive = true;
            _disposableSequence = GetListUpdatHeartBeat();
        }

        private void OnAddToIgonreList()
        {
            if (SelectedProcessToCapture == null)
                return;

            CaptureServiceConfiguration.AddProcessToIgnoreList(SelectedProcessToCapture);
            ProcessesToIgnore.Clear();
            ProcessesToIgnore.AddRange(CaptureServiceConfiguration.GetProcessIgnoreList());

            SelectedProcessToCapture = null;
        }

        private void OnRemoveFromIgnoreList()
        {
            if (SelectedProcessToIgnore == null)
                return;

            CaptureServiceConfiguration.RemoveProcessFromIgnoreList(SelectedProcessToIgnore);
            ProcessesToIgnore.Clear();
            ProcessesToIgnore.AddRange(CaptureServiceConfiguration.GetProcessIgnoreList());
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
                                        x => TimeSpan.FromSeconds(2))
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
        }

        private void OnSelectedProcessToCaptureChanged()
        {
            UpdateCaptureStateInfo();
        }

        private void UpdateCaptureStateInfo()
        {
            if (string.IsNullOrWhiteSpace(SelectedProcessToCapture))
            {
                CaptureStateInfo = $"Capturing inactive... select process and press {CaptureHotkeyString} to start.";
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
