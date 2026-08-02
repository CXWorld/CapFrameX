using CapFrameX.Capture.Contracts;
using CapFrameX.Contracts.Configuration;
using CapFrameX.Contracts.Data;
using CapFrameX.Contracts.Overlay;
using CapFrameX.Contracts.RTSS;
using CapFrameX.Contracts.Sensor;
using CapFrameX.Contracts.Update;
using CapFrameX.Data;
using CapFrameX.EventAggregation.Messages;
using Microsoft.Extensions.Logging;
using Prism.Commands;
using Prism.Events;
using Prism.Mvvm;
using System;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Threading;

namespace CapFrameX.ViewModel
{
	public partial class StateViewModel : BindableBase
	{
		private const int SystemInfoStatusTimeoutMilliseconds = 5000;

		private readonly IEventAggregator _eventAggregator;
		private readonly IAppConfiguration _appConfiguration;
		private readonly ICaptureService _captureService;
		private readonly IOverlayService _overlayService;
		private readonly IAppVersionProvider _appVersionProvider;
		private readonly ISystemInfo _systemInfo;
		private static ILogger<StateViewModel> _logger;

		private bool _isCaptureModeActive;
		private bool _isOverlayActive;
		private HookOverlayStatus _hookOverlayStatus;
		private bool _isHookOverlayStatusVisible;
		private Task _systemInfoStatusUpdateTask;

		private bool IsBeta => _appVersionProvider.GetReleaseChannel() == EUpdateChannel.Beta;

		public bool IsOverlayActive
		{
			get { return _isOverlayActive; }
			set
			{
				_isOverlayActive = value;
				RaisePropertyChanged();
			}
		}

		public bool IsHookOverlayStatusVisible
		{
			get { return _isHookOverlayStatusVisible; }
			private set
			{
				_isHookOverlayStatusVisible = value;
				RaisePropertyChanged();
			}
		}

		public string HookOverlayStatusText
		{
			get
			{
				if (_hookOverlayStatus == null) return "Waiting";
				switch (_hookOverlayStatus.State)
				{
					case EHookOverlayStatus.Disabled: return "Off";
					case EHookOverlayStatus.Waiting: return "Waiting";
					case EHookOverlayStatus.Injecting: return "Injecting";
					case EHookOverlayStatus.Injected: return "Injected";
					case EHookOverlayStatus.Initializing: return "Initializing";
					case EHookOverlayStatus.Active: return "Active";
					case EHookOverlayStatus.Fallback: return "Fallback";
					case EHookOverlayStatus.Hidden: return "Hidden";
					case EHookOverlayStatus.Idle: return "Idle";
					case EHookOverlayStatus.Error: return "Error";
					case EHookOverlayStatus.Blocked: return "Blocked";
					default: return "Waiting";
				}
			}
		}

		public string HookOverlayStatusColor
		{
			get
			{
				if (_hookOverlayStatus == null) return "Orange";
				switch (_hookOverlayStatus.State)
				{
					case EHookOverlayStatus.Active: return "LimeGreen";
					case EHookOverlayStatus.Fallback: return "DarkOrange";
					// Needs the user to act (restart the game), so it must not read as a
					// transient "Waiting" orange, but it is not a malfunction either.
					case EHookOverlayStatus.Blocked: return "Goldenrod";
					case EHookOverlayStatus.Error: return "OrangeRed";
					case EHookOverlayStatus.Disabled:
					case EHookOverlayStatus.Hidden: return "Gray";
					default: return "Orange";
				}
			}
		}

		public string HookOverlayStatusToolTip => _hookOverlayStatus?.Detail ??
			"Waiting for hook status.";

		public bool IsCaptureModeActive
		{
			get { return _isCaptureModeActive; }
			set
			{
				_isCaptureModeActive = value;
				RaisePropertyChanged();
			}
		}
		public bool IsLoggingActive
		{
			get { return _appConfiguration.UseSensorLogging; }
			set
			{
				_appConfiguration.UseSensorLogging = value;
				RaisePropertyChanged();
			}
		}

		/// <summary>
		/// Backs the update indicator on the right of the status bar. The shared instance also
		/// backs the update tab in the options popup and the embedded dialog.
		/// </summary>
		public UpdateViewModel UpdateViewModel { get; }

		public string VersionString
		{
			get
			{
				var version = _appVersionProvider.GetAppVersion();
				var versionString = $"{version.Major}.{version.Minor}.{Math.Max(version.Build, 0)}.{Math.Max(version.Revision, 0)}";

				return IsBeta ? $"{versionString} Beta" : versionString;
			}
		}

		public bool IsLoggedIn { get; private set; }

		public string InfoToolTipText => GetInfoText();

		public ICommand UpdateStatusInfoCommand { get; }

		public StateViewModel(IEventAggregator eventAggregator,
			IAppConfiguration appConfiguration,
			ICaptureService captureService,
			IOverlayService overlayService,
			IHookOverlayStatusService hookOverlayStatusService,
			UpdateViewModel updateViewModel,
			IAppVersionProvider appVersionProvider,
			LoginManager loginManager,
			IRTSSService rTSSService,
			ISystemInfo systemInfo,
			ISensorService sensorService,
			ILogger<StateViewModel> logger)
		{
			_eventAggregator = eventAggregator;
			_appConfiguration = appConfiguration;
			_captureService = captureService;
			_overlayService = overlayService;
			UpdateViewModel = updateViewModel;
			_appVersionProvider = appVersionProvider;
			_systemInfo = systemInfo;
			_logger = logger;

			UpdateStatusInfoCommand = new DelegateCommand(RefreshSystemInfo);

			IsCaptureModeActive = false;
			IsOverlayActive = _appConfiguration.IsOverlayActive &&
				(rTSSService.IsRTSSInstalled() || _appConfiguration.EnableHookFreeOverlay ||
				 _appConfiguration.EnableHookOverlay);
			IsHookOverlayStatusVisible = _appConfiguration.EnableHookOverlay;
			ApplyHookOverlayStatus(hookOverlayStatusService.Current);
			Dispatcher uiDispatcher = Dispatcher.CurrentDispatcher;

			hookOverlayStatusService.StatusStream.Subscribe(status =>
			{
				Action apply = () => ApplyHookOverlayStatus(status);
				if (uiDispatcher.CheckAccess()) apply();
				else uiDispatcher.BeginInvoke(apply);
			});

			_appConfiguration.OnValueChanged
				.Where(x => x.key == nameof(IAppConfiguration.EnableHookOverlay))
				.Subscribe(x =>
				{
					Action apply = () => IsHookOverlayStatusVisible = (bool)x.value;
					if (uiDispatcher.CheckAccess()) apply();
					else uiDispatcher.BeginInvoke(apply);
				});

			_captureService.IsCaptureModeActiveStream
				.Subscribe(state => IsCaptureModeActive = state);

			sensorService.IsLoggingActiveStream
				.Subscribe(state => IsLoggingActive = state);

			_overlayService.IsOverlayActiveStream
				.Subscribe(state => IsOverlayActive = state);

			IsLoggedIn = loginManager.State.Token != null;

			_eventAggregator.GetEvent<PubSubEvent<AppMessages.LoginState>>().Subscribe(state =>
			{
				IsLoggedIn = state.IsLoggedIn;
				RaisePropertyChanged(nameof(IsLoggedIn));
			});

			_eventAggregator.GetEvent<PubSubEvent<ViewMessages.UpdateSystemInfo>>().Subscribe(msg =>
			{
				UpdateSystemInfoStatus();
			});

			Dispatcher.CurrentDispatcher.BeginInvoke(
				new Action(RefreshSystemInfo),
				DispatcherPriority.ApplicationIdle);
		}

		private void ApplyHookOverlayStatus(HookOverlayStatus status)
		{
			_hookOverlayStatus = status;
			RaisePropertyChanged(nameof(HookOverlayStatusText));
			RaisePropertyChanged(nameof(HookOverlayStatusColor));
			RaisePropertyChanged(nameof(HookOverlayStatusToolTip));
		}

		public async void RefreshSystemInfo()
		{
			await RefreshSystemInfoAsync();
		}

		private async Task RefreshSystemInfoAsync()
		{
			if (_systemInfoStatusUpdateTask != null && !_systemInfoStatusUpdateTask.IsCompleted)
				return;

			var refreshTask = Task.Run(() => _systemInfo.SetSystemInfosStatus());
			_systemInfoStatusUpdateTask = refreshTask;

			try
			{
				var timeoutTask = Task.Delay(SystemInfoStatusTimeoutMilliseconds);
				var completedTask = await Task.WhenAny(refreshTask, timeoutTask);

				if (completedTask != refreshTask)
				{
					_logger.LogWarning("System info status update timed out after {timeoutMilliseconds} ms.", SystemInfoStatusTimeoutMilliseconds);
					_ = refreshTask.ContinueWith(task =>
					{
						_logger.LogError(task.Exception.Flatten(), "Error while updating system info status after timeout.");
					}, TaskContinuationOptions.OnlyOnFaulted);
					return;
				}

				await refreshTask;
				UpdateSystemInfoStatus();
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error while updating system info status.");
			}
			finally
			{
				if (ReferenceEquals(_systemInfoStatusUpdateTask, refreshTask) && refreshTask.IsCompleted)
				{
					_systemInfoStatusUpdateTask = null;
				}
			}
		}

		private string GetInfoText()
		{
			string info;
			try
			{
				var version = _appVersionProvider.GetAppVersion();
				info = $"Revision: {version.Revision}";
			}
			catch
			{
				info = "No info available";
			}

			return info;
		}
	}
}
