using CapFrameX.Contracts.Configuration;
using CapFrameX.Contracts.Data;
using CapFrameX.Contracts.Overlay;
using CapFrameX.Contracts.PresentMonInterface;
using CapFrameX.Contracts.UpdateCheck;
using CapFrameX.Overlay;
using CapFrameX.Updater;
using Prism.Events;
using Prism.Mvvm;
using System;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Linq;
using System.Reflection;

namespace CapFrameX.ViewModel
{
	public class StateViewModel : BindableBase
	{	
		private readonly IRecordDirectoryObserver _recordObserver;
		private readonly IEventAggregator _eventAggregator;
		private readonly IAppConfiguration _appConfiguration;
		private readonly ICaptureService _captureService;
		private readonly IOverlayService _overlayService;
		private readonly IUpdateCheck _updateCheck;
		private readonly IAppVersionProvider _appVersionProvider;
		private bool _isCaptureModeActive;
		private bool _isOverlayActive;
		private bool _isDirectoryObserving;
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

		public bool IsDirectoryObserving
		{
			get { return _isDirectoryObserving; }
			set
			{
				_isDirectoryObserving =
					value && _recordObserver.HasValidSource;
				RaisePropertyChanged();
			}
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

		public bool IsUpdateAvailable => _updateCheck.IsUpdateAvailable();

		public StateViewModel(IRecordDirectoryObserver recordObserver,
							  IEventAggregator eventAggregator,
							  IAppConfiguration appConfiguration,
							  ICaptureService captureService,
							  IOverlayService overlayService,
							  IUpdateCheck updateCheck,
							  IAppVersionProvider appVersionProvider,
							  IWebVersionProvider webVersionProvider)
		{
			_recordObserver = recordObserver;
			_eventAggregator = eventAggregator;
			_appConfiguration = appConfiguration;
			_captureService = captureService;
			_overlayService = overlayService;
			_updateCheck = updateCheck;
			_appVersionProvider = appVersionProvider;
			IsDirectoryObserving = true;
			IsCaptureModeActive = false;
			IsOverlayActive = _appConfiguration.IsOverlayActive && !string.IsNullOrEmpty(RTSSUtils.GetRTSSFullPath());

			UpdateHyperlinkText = $"New version available on GitHub: v{webVersionProvider.GetWebVersion()}";

			_recordObserver.HasValidSourceStream
				.Subscribe(state => IsDirectoryObserving = state);

			_captureService.IsCaptureModeActiveStream
				.Subscribe(state => IsCaptureModeActive = state);

			_overlayService.IsOverlayActiveStream
				.Subscribe(state => IsOverlayActive = state);
		}

		private Assembly GetAssemblyByName(string name)
		{
			return AppDomain.CurrentDomain.GetAssemblies().
				   SingleOrDefault(assembly => assembly.GetName().Name == name);
		}
	}
}
