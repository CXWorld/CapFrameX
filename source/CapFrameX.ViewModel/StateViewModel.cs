using CapFrameX.Contracts.Configuration;
using CapFrameX.Contracts.Overlay;
using CapFrameX.Contracts.PresentMonInterface;
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

		private bool _isCaptureModeActive;
		private bool _isOverlayActive;
		private bool _isDirectoryObserving;
		private string _updateHpyerlinkText;

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

		public string UpdateHpyerlinkText
		{
			get { return _updateHpyerlinkText; }
			set
			{
				_updateHpyerlinkText = value;
				RaisePropertyChanged();
			}
		}

		public string VersionString
		{
			get
			{
				Assembly assembly = GetAssemblyByName("CapFrameX");
				var fileVersion = FileVersionInfo.GetVersionInfo(assembly.Location).FileVersion;

				var numbers = fileVersion.Split('.');
				return IsBeta ? $"{numbers[0]}.{numbers[1]}.{numbers[2]} Beta" : $"{numbers[0]}.{numbers[1]}.{numbers[2]}";
			}
		}

		public bool IsUpdateAvailable => WebCheck.IsCXUpdateAvailable(WebCheck.VersionSourceFileUrl);

		public StateViewModel(IRecordDirectoryObserver recordObserver,
							  IEventAggregator eventAggregator,
							  IAppConfiguration appConfiguration,
							  ICaptureService captureService,
							  IOverlayService overlayService)
		{
			_recordObserver = recordObserver;
			_eventAggregator = eventAggregator;
			_appConfiguration = appConfiguration;
			_captureService = captureService;
			_overlayService = overlayService;

			IsDirectoryObserving = true;
			IsCaptureModeActive = false;
			IsOverlayActive = _appConfiguration.IsOverlayActive && !string.IsNullOrEmpty(RTSSUtils.GetRTSSFullPath());

			UpdateHpyerlinkText = $"New version available on GitHub: v{WebCheck.GetWebVersion(WebCheck.VersionSourceFileUrl)}";

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
