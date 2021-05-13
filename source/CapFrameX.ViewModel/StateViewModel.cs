using CapFrameX.Contracts.Configuration;
using CapFrameX.Contracts.Data;
using CapFrameX.Contracts.Overlay;
using CapFrameX.Contracts.PresentMonInterface;
using CapFrameX.Contracts.RTSS;
using CapFrameX.Contracts.UpdateCheck;
using CapFrameX.Data;
using CapFrameX.EventAggregation.Messages;
using CapFrameX.SetupAPI.NetStandard;
using Microsoft.Extensions.Logging;
using Prism.Events;
using Prism.Mvvm;
using System;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace CapFrameX.ViewModel
{
    public class StateViewModel : BindableBase
    {
        private readonly IEventAggregator _eventAggregator;
        private readonly IAppConfiguration _appConfiguration;
        private readonly ICaptureService _captureService;
        private readonly IOverlayService _overlayService;
        private readonly IUpdateCheck _updateCheck;
        private readonly IAppVersionProvider _appVersionProvider;
        private readonly ISetupAPI _setupAPI;
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

        public bool IsPciAbove4GDecodingEnabled => _setupAPI.PciAbove4GDecodingStatus == EPciDeviceInfoStatus.Enabled;

        public bool IsPciLargeMemoryEnabled => _setupAPI.PciLargeMemoryStatus == EPciDeviceInfoStatus.Enabled;

        public bool PciDeviceInfoIsValid => 
            _setupAPI.PciAbove4GDecodingStatus != EPciDeviceInfoStatus.Invalid 
            && _setupAPI.PciLargeMemoryStatus != EPciDeviceInfoStatus.Invalid;

        public StateViewModel(IEventAggregator eventAggregator,
                              IAppConfiguration appConfiguration,
                              ICaptureService captureService,
                              IOverlayService overlayService,
                              IUpdateCheck updateCheck,
                              IAppVersionProvider appVersionProvider,
                              LoginManager loginManager,
                              IRTSSService rTSSService,
                              ISetupAPI setupAPI,
                              ILogger<StateViewModel> logger)
        {
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();

            _eventAggregator = eventAggregator;
            _appConfiguration = appConfiguration;
            _captureService = captureService;
            _overlayService = overlayService;
            _updateCheck = updateCheck;
            _appVersionProvider = appVersionProvider;
            _setupAPI = setupAPI;
            _logger = logger;

            IsCaptureModeActive = false;
            IsOverlayActive = _appConfiguration.IsOverlayActive && rTSSService.IsRTSSInstalled();

            _captureService.IsCaptureModeActiveStream
                .Subscribe(state => IsCaptureModeActive = state);

            _captureService.IsLoggingActiveStream
                .Subscribe(state => IsLoggingActive = state);

            _overlayService.IsOverlayActiveStream
                .Subscribe(state => IsOverlayActive = state);

            IsLoggedIn = loginManager.State.Token != null;

            _eventAggregator.GetEvent<PubSubEvent<AppMessages.LoginState>>().Subscribe(state =>
            {
                IsLoggedIn = state.IsLoggedIn;
                RaisePropertyChanged(nameof(IsLoggedIn));
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

            stopwatch.Stop();
            _logger.LogInformation(this.GetType().Name + " {initializationTime}s initialization time", Math.Round(stopwatch.ElapsedMilliseconds * 1E-03, 1));
        }

        private Assembly GetAssemblyByName(string name)
        {
            return AppDomain.CurrentDomain.GetAssemblies().
                   SingleOrDefault(assembly => assembly.GetName().Name == name);
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
