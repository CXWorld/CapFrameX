using CapFrameX.Capture.Contracts;
using CapFrameX.Contracts.Configuration;
using CapFrameX.Contracts.Data;
using CapFrameX.Contracts.Overlay;
using CapFrameX.Contracts.RTSS;
using CapFrameX.Contracts.Sensor;
using CapFrameX.Contracts.UpdateCheck;
using CapFrameX.Data;
using CapFrameX.EventAggregation.Messages;
using Microsoft.Extensions.Logging;
using Prism.Commands;
using Prism.Events;
using Prism.Mvvm;
using System;
using System.Linq;
using System.Reactive.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Threading;

namespace CapFrameX.ViewModel
{
	public partial class StateViewModel : BindableBase
	{
		private readonly IEventAggregator _eventAggregator;
		private readonly IAppConfiguration _appConfiguration;
		private readonly ICaptureService _captureService;
		private readonly IOverlayService _overlayService;
		private readonly IUpdateCheck _updateCheck;
		private readonly IAppVersionProvider _appVersionProvider;
		private readonly ISystemInfo _systemInfo;
		private static ILogger<StateViewModel> _logger;

		private bool _isCaptureModeActive;
		private bool _isOverlayActive;
		private string _updateHyperlinkText;

		private bool IsBeta => GetBetaState();

		private bool GetBetaState()
		{
			Assembly assembly = GetAssemblyByName("CapFrameX");
			var metaData = assembly.GetCustomAttributes(typeof(AssemblyMetadataAttribute));

			if (metaData.FirstOrDefault(attribute => (attribute as AssemblyMetadataAttribute).Key == "IsBeta")
				is AssemblyMetadataAttribute isBetaAttribute)
				return Convert.ToBoolean(isBetaAttribute.Value);

			return true;
		}

		public bool IsOverlayActive
		{
			get { return _isOverlayActive; }
			set
			{
				_isOverlayActive = value;
				RaisePropertyChanged();
			}
		}

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

		public string UpdateHyperlinkText
		{
			get { return _updateHyperlinkText; }
			set
			{
				_updateHyperlinkText = value;
				RaisePropertyChanged();
			}
		}

		public string VersionString
		{
			get
			{
				var version = _appVersionProvider.GetAppVersion();
				var versionString = $"{version.Major}.{version.Minor}.{version.Build}";

				return IsBeta ? $"{versionString} Beta" : versionString;
			}
		}

		public bool IsUpdateAvailable { get; private set; }

		public bool IsLoggedIn { get; private set; }

		public string InfoToolTipText => GetInfoText();

		public ICommand UpdateStatusInfoCommand { get; }

		public StateViewModel(IEventAggregator eventAggregator,
							  IAppConfiguration appConfiguration,
							  ICaptureService captureService,
							  IOverlayService overlayService,
							  IUpdateCheck updateCheck,
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
			_updateCheck = updateCheck;
			_appVersionProvider = appVersionProvider;
			_systemInfo = systemInfo;
			_logger = logger;

			UpdateStatusInfoCommand = new DelegateCommand(RefreshSystemInfo);

			IsCaptureModeActive = false;
			IsOverlayActive = _appConfiguration.IsOverlayActive && rTSSService.IsRTSSInstalled();

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

			Task.Run(async () =>
			{
				var (updateAvailable, updateVersion) = await _updateCheck.IsUpdateAvailable();
				Dispatcher.CurrentDispatcher.Invoke(() =>
				{
					IsUpdateAvailable = updateAvailable;
					UpdateHyperlinkText = $"New version available on GitHub: v{updateVersion}";
					RaisePropertyChanged(nameof(IsUpdateAvailable));
				});
			});
		}

		private Assembly GetAssemblyByName(string name)
		{
			return AppDomain.CurrentDomain.GetAssemblies().
				   SingleOrDefault(assembly => assembly.GetName().Name == name);
		}

		public void RefreshSystemInfo()
		{
			_systemInfo.SetSystemInfosStatus();
			UpdateSystemInfoStatus();
		}

		private string GetInfoText()
		{
			string info;
			try
			{
				var version = GetAssemblyByName("CapFrameX").GetName().Version;
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
