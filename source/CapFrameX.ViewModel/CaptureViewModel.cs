using CapFrameX.Contracts.Configuration;
using CapFrameX.Contracts.Data;
using CapFrameX.Contracts.Overlay;
using CapFrameX.Contracts.PresentMonInterface;
using CapFrameX.Contracts.RTSS;
using CapFrameX.Contracts.Sensor;
using CapFrameX.Data;
using CapFrameX.Hotkey;
using CapFrameX.PresentMonInterface;
using CapFrameX.Statistics;
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
using System.Diagnostics;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace CapFrameX.ViewModel
{
	public partial class CaptureViewModel : BindableBase, INavigationAware
	{
		private const int PRESICE_OFFSET = 2500;
		private const int ARCHIVE_LENGTH = 500;

		private readonly object _archiveLock = new object();

		[DllImport("Kernel32.dll")]
		private static extern bool QueryPerformanceCounter(out long lpPerformanceCount);

		private readonly IAppConfiguration _appConfiguration;
		private readonly ICaptureService _captureService;
		private readonly IEventAggregator _eventAggregator;
		private readonly IRecordManager _recordManager;
		private readonly IOverlayService _overlayService;
		private readonly ISensorService _sensorService;
		private readonly IOnlineMetricService _onlineMetricService;
		private readonly IStatisticProvider _statisticProvider;
		private readonly IRTSSService _rTSSService;
		private readonly ILogger<CaptureViewModel> _logger;
		private readonly ProcessList _processList;
		private readonly SoundManager _soundManager;
		private readonly List<string> _captureDataArchive = new List<string>(ARCHIVE_LENGTH);

		private IDisposable _disposableHeartBeat;
		private IDisposable _disposableCaptureStream;
		private IDisposable _disposableArchiveStream;
		private List<string> _captureData;
		private string _selectedProcessToCapture;
		private string _selectedProcessToIgnore;
		private bool _areButtonsActive = true;
		private bool _isCapturing;
		private string _captureStateInfo = string.Empty;
		private string _captureTimeString = "0";
		private string _captureStartDelayString = "0";
		private IKeyboardMouseEvents _globalCaptureHookEvent;
		private string _loggerOutput = string.Empty;
		private bool _fillArchive = false;
		private CancellationTokenSource _cancellationTokenSource;
		private PlotModel _frametimeModel;
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

		public bool AreButtonsActive
		{
			get { return _areButtonsActive; }
			set
			{
				_areButtonsActive = value;
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

		public bool UseSensorLogging
		{
			get { return _appConfiguration.UseSensorLogging; }
			set
			{
				_appConfiguration.UseSensorLogging = value;
				_captureService.IsLoggingActiveStream.OnNext(value);
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
								ICaptureService captureService,
								IEventAggregator eventAggregator,
								IRecordManager recordManager,
								IOverlayService overlayService,
								ISensorService sensorService,
								IOnlineMetricService onlineMetricService,
								IStatisticProvider statisticProvider,
								IRTSSService rTSSService,
								ILogger<CaptureViewModel> logger,
								ProcessList processList,
								SoundManager soundManager)
		{
			_appConfiguration = appConfiguration;
			_captureService = captureService;
			_eventAggregator = eventAggregator;
			_recordManager = recordManager;
			_overlayService = overlayService;
			_sensorService = sensorService;
			_onlineMetricService = onlineMetricService;
			_statisticProvider = statisticProvider;
			_rTSSService = rTSSService;
			_logger = logger;
			_processList = processList;
			_soundManager = soundManager;
			AddToIgonreListCommand = new DelegateCommand(OnAddToIgonreList);
			AddToProcessListCommand = new DelegateCommand(OnAddToProcessList);
			ResetPresentMonCommand = new DelegateCommand(OnResetCaptureProcess);

			_logger.LogDebug("{viewName} Ready", this.GetType().Name);
			CaptureStateInfo = "Service ready..." + Environment.NewLine +
				$"Press {CaptureHotkeyString} to start capture of the running process.";
			SelectedSoundMode = _appConfiguration.HotkeySoundMode;
			CaptureTimeString = _appConfiguration.CaptureTime.ToString();
			_disposableHeartBeat = GetListUpdatHeartBeat();

			SubscribeToUpdateProcessIgnoreList();
			SubscribeToGlobalCaptureHookEvent();
			ConnectOnlineMetricDataStream();

			bool captureServiceStarted = StartCaptureService();

			if (captureServiceStarted)
				_overlayService.SetCaptureServiceStatus("Capture service ready...");


			_captureService.IsCaptureModeActiveStream.OnNext(false);
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

		private void AddLoggerEntry(string entry)
		{
			LoggerOutput += $"{DateTime.Now.ToLongTimeString()}: {entry}" + Environment.NewLine;
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
					if(!_dataOffsetRunning)
						SetCaptureMode();
				}}
			};

			_globalCaptureHookEvent = Hook.GlobalEvents();
			_globalCaptureHookEvent.OnCXCombination(onCombinationDictionary);
		}

		private void ConnectOnlineMetricDataStream()
		{
			string currentProcess = null;
			_captureService.RedirectedOutputDataStream
				.Skip(5)
				.ObserveOn(new EventLoopScheduler()).Subscribe(dataLine =>
				{
					if (string.IsNullOrWhiteSpace(dataLine))
						return;

					// explicit hook, only one process
					if (!string.IsNullOrWhiteSpace(SelectedProcessToCapture))
						currentProcess = SelectedProcessToCapture;
					// auto hook with filtered process list
					else
					{
						currentProcess = ProcessesToCapture.FirstOrDefault();
					}

					var lineSplit = dataLine.Split(',');
					if (lineSplit.Count() > 1)
					{

						_onlineMetricService.ProcessDataLineStream.OnNext(Tuple.Create(currentProcess, dataLine));

						if (currentProcess == lineSplit[0].Replace(".exe", ""))
						{
							if (uint.TryParse(lineSplit[1], out uint processID))
							{
								_rTSSService.ProcessIdStream.OnNext(processID);
							}
						}
					}
					else
						_logger.LogInformation("Unusable {dataLine} string.", dataLine);
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
			else if (!IsCapturing)
			{
				if (SelectedProcessToCapture != null)
					_lastCapturedProcess = SelectedProcessToCapture;
				else
					_lastCapturedProcess = ProcessesToCapture.FirstOrDefault();

				_ = QueryPerformanceCounter(out long startCounter);
				AddLoggerEntry($"Performance counter on start capturing: {startCounter}");
				_qpcTimeStart = startCounter;

				_timestampStartCapture = new DateTimeOffset(DateTime.UtcNow).ToUnixTimeMilliseconds();

				_soundManager.PlaySound(Sound.CaptureStarted);

				StartCaptureDataFromStream();

				IsCapturing = !IsCapturing;
				_disposableHeartBeat?.Dispose();
				AreButtonsActive = false;

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

				_soundManager.PlaySound(Sound.CaptureStopped);

				_sensorService.StopSensorLogging();
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
			_sensorService.StartSensorLogging();

			_captureData = new List<string>();
			bool autoTermination = Convert.ToInt32(CaptureTimeString) > 0;
			double delayCapture = Convert.ToInt32(CaptureStartDelayString);
			double captureTime = Convert.ToInt32(CaptureTimeString) + delayCapture;
			bool intializedStartTime = false;
			double captureDataArchiveLastTime = 0;

			_disposableCaptureStream = _captureService.RedirectedOutputDataStream
				.Skip(5)
				.ObserveOn(new EventLoopScheduler()).Subscribe(dataLine =>
				{
					if (string.IsNullOrWhiteSpace(dataLine))
						return;

					_captureData.Add(dataLine);

					if (!intializedStartTime && _captureData.Any())
					{
						double captureDataFirstTime = GetStartTimeFromDataLine(_captureData.First());
						lock (_archiveLock)
						{
							if (_captureDataArchive.Any())
							{
								captureDataArchiveLastTime = GetStartTimeFromDataLine(_captureDataArchive.Last());
							}
						}

						if (captureDataFirstTime < captureDataArchiveLastTime)
						{
							intializedStartTime = true;

							// stop archive
							_fillArchive = false;
							_disposableArchiveStream?.Dispose();

							AddLoggerEntry("Stopped filling archive.");
						}
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
							// stop sensor data logging
							_sensorService.StopSensorLogging();

							// turn locking on 
							_dataOffsetRunning = true;
							CaptureStateInfo = "Creating capture file...";

							// update overlay
							_overlayService.SetCaptureServiceStatus("Processing data");
							_soundManager.PlaySound(Sound.CaptureStopped);
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
			WriteCaptureDataToFile();

			IsCapturing = !IsCapturing;
			_disposableHeartBeat = GetListUpdatHeartBeat();
			AreButtonsActive = true;

			UpdateCaptureStateInfo();
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
				.RedirectedOutputDataStream.Where(x => _fillArchive == true)
				.ObserveOn(new EventLoopScheduler())
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
			var processList = _captureService.GetAllFilteredProcesses(filter).Distinct();

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

		private void AddDataLineToArchive(string dataLine)
		{
			if (string.IsNullOrWhiteSpace(dataLine))
			{
				return;
			}

			lock (_archiveLock)
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
		}

		private void ResetArchive() => _captureDataArchive.Clear();

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
