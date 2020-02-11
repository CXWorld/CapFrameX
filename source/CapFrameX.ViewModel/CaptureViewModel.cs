using CapFrameX.Contracts.Configuration;
using CapFrameX.Contracts.Data;
using CapFrameX.Contracts.Overlay;
using CapFrameX.Contracts.PresentMonInterface;
using CapFrameX.EventAggregation.Messages;
using CapFrameX.Hotkey;
using CapFrameX.PresentMonInterface;
using CapFrameX.Statistics;
using Gma.System.MouseKeyHook;
using Microsoft.Extensions.Logging;
using OxyPlot;
using OxyPlot.Axes;
using Prism.Commands;
using Prism.Events;
using Prism.Mvvm;
using Prism.Regions;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace CapFrameX.ViewModel
{
	public partial class CaptureViewModel : BindableBase, INavigationAware
	{
		private const int PRESICE_OFFSET = 2500;
		private const int ARCHIVE_LENGTH = 500;

		[DllImport("Kernel32.dll")]
		private static extern bool QueryPerformanceCounter(out long lpPerformanceCount);

		private readonly IAppConfiguration _appConfiguration;
		private readonly ICaptureService _captureService;
		private readonly IEventAggregator _eventAggregator;
		private readonly IRecordDataProvider _recordDataProvider;
		private readonly IOverlayService _overlayService;
		private readonly IStatisticProvider _statisticProvider;
		private readonly ILogger<CaptureViewModel> _logger;
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
		private IKeyboardMouseEvents _globalCaptureHookEvent;
		private string _selectedSoundMode;
		private string _loggerOutput = string.Empty;
		private bool _fillArchive = false;
		private CancellationTokenSource _cancellationTokenSource;
		private int _sliderSoundLevel;
		private bool _showVolumeController;
		private PlotModel _frametimeModel;
		private ISubject<string> _frametimeStream;
		private long _qpcTimeStart;
		private long _timestampStartCapture;
		private long _timestampStopCapture;
		private bool _dataOffsetRunning;
		private string _lastCapturedProcess;

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
				if (!CXHotkey.IsValidHotkey(value))
					return;

				_appConfiguration.CaptureHotKey = value;
				UpdateCaptureStateInfo();
				UpdateGlobalCaptureHookEvent();
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

		public PlotModel FrametimeModel
		{
			get { return _frametimeModel; }
			set
			{
				_frametimeModel = value;
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
								IRecordDataProvider recordDataProvider,
								IOverlayService overlayService,
								IStatisticProvider statisticProvider,
								ILogger<CaptureViewModel> logger)
		{
			_appConfiguration = appConfiguration;
			_captureService = captureService;
			_eventAggregator = eventAggregator;
			_recordDataProvider = recordDataProvider;
			_overlayService = overlayService;
			_statisticProvider = statisticProvider;
			_logger = logger;

			AddToIgonreListCommand = new DelegateCommand(OnAddToIgonreList);
			AddToProcessListCommand = new DelegateCommand(OnAddToProcessList);
			ResetCaptureProcessCommand = new DelegateCommand(OnResetCaptureProcess);

			_logger.LogDebug("{viewName} Ready", this.GetType().Name);
			CaptureStateInfo = "Service ready..." + Environment.NewLine + 
				$"Press {CaptureHotkeyString} to start capture of the running process.";
			SelectedSoundMode = _appConfiguration.HotkeySoundMode;
			CaptureTimeString = _appConfiguration.CaptureTime.ToString();

			ProcessesToIgnore.AddRange(CaptureServiceConfiguration.GetProcessIgnoreList());
			_disposableHeartBeat = GetListUpdatHeartBeat();
			_frametimeStream = new Subject<string>();

			SubscribeToUpdateProcessIgnoreList();
			SubscribeToGlobalCaptureHookEvent();

			bool captureServiceStarted = StartCaptureService();

			if (captureServiceStarted)
				_overlayService.SetCaptureServiceStatus("Capture service ready...");


			_captureService.IsCaptureModeActiveStream.OnNext(false);

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
					if(!_dataOffsetRunning)
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
				if (SelectedProcessToCapture != null)
					_lastCapturedProcess = SelectedProcessToCapture;
				else
					_lastCapturedProcess = ProcessesToCapture.FirstOrDefault();

				_ = QueryPerformanceCounter(out long startCounter);
				AddLoggerEntry($"Performance counter on start capturing: {startCounter}");
				_qpcTimeStart = startCounter;

				_timestampStartCapture = new DateTimeOffset(DateTime.UtcNow).ToUnixTimeMilliseconds();

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
					CaptureStateInfo = "Capturing in progress..." + Environment.NewLine + $"Press {CaptureHotkeyString} to stop capture.";

				if (CaptureTimeString != "0" && CaptureStartDelayString == "0")
					CaptureStateInfo = $"Capturing in progress (Set Time: {CaptureTimeString} seconds)..." + Environment.NewLine
					   + $"Press {CaptureHotkeyString} to stop capture.";

				if (CaptureTimeString != "0" && CaptureStartDelayString != "0")
					CaptureStateInfo = $"Capturing starts with delay of {CaptureStartDelayString} seconds. " +
						$"Capture will stop after {CaptureTimeString} seconds." + Environment.NewLine + $"Press {CaptureHotkeyString} to stop capture.";
			}
			else
			{
				// manual termination (hotkey triggered)
				// turn locking on 
				_dataOffsetRunning = true;

				_timestampStopCapture = new DateTimeOffset(DateTime.UtcNow).ToUnixTimeMilliseconds();
				_cancellationTokenSource?.Cancel();

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

				var context = TaskScheduler.FromCurrentSynchronizationContext();

				CaptureStateInfo = "Creating capture file...";
				_overlayService.StopCaptureTimer();
				_overlayService.SetCaptureServiceStatus("Processing data");

				// offset timer
				Task.Run(async () =>
				{
					await SetTaskDelayOffset().ContinueWith(_ =>
					{
						Application.Current.Dispatcher.Invoke(new Action(() =>
						{
							FinishCapturingAndUpdateUi();
						}));
					}, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, context);
				});
			}
		}

		private void StartCaptureDataFromStream()
		{
			AddLoggerEntry("Capturing started.");
			_overlayService.SetCaptureServiceStatus("Recording frametimes");

			_captureData = new List<string>();
			bool autoTermination = Convert.ToInt32(CaptureTimeString) > 0;
			double delayCapture = Convert.ToInt32(CaptureStartDelayString);
			double captureTime = Convert.ToInt32(CaptureTimeString) + delayCapture;
			bool intializedStartTime = false;

			_disposableCaptureStream = _captureService.RedirectedOutputDataStream
				.ObserveOn(new EventLoopScheduler()).Subscribe(dataLine =>
				{
					if (string.IsNullOrWhiteSpace(dataLine))
						return;

					_captureData.Add(dataLine);
					_frametimeStream.OnNext(dataLine);

					if (!intializedStartTime)
					{
						intializedStartTime = true;

						// stop archive
						_fillArchive = false;
						_disposableArchiveStream?.Dispose();

						AddLoggerEntry("Stopped filling archive.");
					}
				});

			var context = TaskScheduler.FromCurrentSynchronizationContext();

			if (autoTermination)
			{
				AddLoggerEntry("Starting countdown...");

				// Start overlay countdown timer
				_overlayService.StartCountdown(Convert.ToInt32(CaptureTimeString));
				_cancellationTokenSource = new CancellationTokenSource();

				// data timer
				Task.Run(async () =>
				{
					await SetTaskDelayData().ContinueWith(_ =>
					{
						Application.Current.Dispatcher.Invoke(new Action(() =>
						{
							FinishCapturingAndUpdateUi();
						}));
					}, _cancellationTokenSource.Token, TaskContinuationOptions.ExecuteSynchronously, context);
				});

				// sound timer
				Task.Run(async () =>
				{
					await SetTaskDelaySound().ContinueWith(_ =>
					{
						Application.Current.Dispatcher.Invoke(new Action(() =>
						{
							// turn locking on 
							_dataOffsetRunning = true;
							CaptureStateInfo = "Creating capture file...";

							// update overlay
							_overlayService.SetCaptureServiceStatus("Processing data");

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
						}));
					}, _cancellationTokenSource.Token, TaskContinuationOptions.ExecuteSynchronously, context);
				});
			}
			else
				_overlayService.StartCaptureTimer();
		}

		private void FinishCapturingAndUpdateUi()
		{
			_disposableCaptureStream?.Dispose();

			AddLoggerEntry("Capturing stopped.");
			// asynchron
			WriteCaptureDataToFile();

			IsCapturing = !IsCapturing;
			_disposableHeartBeat = GetListUpdatHeartBeat();
			IsAddToIgnoreListButtonActive = true;
			UpdateCaptureStateInfo();
			_overlayService.SetCaptureServiceStatus("Capture service ready...");	
		}

		private async Task SetTaskDelayOffset()
		{
			await Task.Delay(TimeSpan.FromMilliseconds(PRESICE_OFFSET));
		}

		private async Task SetTaskDelayData()
		{
			await Task.Delay(TimeSpan.FromMilliseconds(PRESICE_OFFSET +
				1000 * Convert.ToInt32(CaptureTimeString)));
		}

		private async Task SetTaskDelaySound()
		{
			await Task.Delay(TimeSpan.FromMilliseconds(
				1000 * Convert.ToInt32(CaptureTimeString)));
		}

		private bool StartCaptureService()
		{
			bool success;
			var serviceConfig = GetRedirectedServiceConfig();
			var startInfo = CaptureServiceConfiguration
				.GetServiceStartInfo(serviceConfig.ConfigParameterToArguments());
			success = _captureService.StartCaptureService(startInfo);

			StartFillArchive();

			return success;
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
			return Observable
				.Timer(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(1))
				.ObserveOn(context)
				.SubscribeOn(context)
				.Subscribe(x => UpdateProcessToCaptureList());
		}

		private void UpdateProcessToCaptureList()
		{
			var selectedProcessToCapture = SelectedProcessToCapture;
			var backupProcessList = new List<string>(ProcessesToCapture);
			ProcessesToCapture.Clear();
			var filter = CaptureServiceConfiguration.GetProcessIgnoreList();
			var processList = _captureService.GetAllFilteredProcesses(filter).Distinct();
			ProcessesToCapture.AddRange(processList);

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
