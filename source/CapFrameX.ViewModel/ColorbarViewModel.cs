using CapFrameX.Contracts.Configuration;
using CapFrameX.Data;
using CapFrameX.Extensions.NetStandard;
using CapFrameX.PresentMonInterface;
using Microsoft.WindowsAPICodePack.Dialogs;
using Prism.Commands;
using Prism.Events;
using Prism.Mvvm;
using Prism.Regions;
using System;
using Task = System.Threading.Tasks.Task;
using Microsoft.Win32.TaskScheduler;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Input;
using CapFrameX.Contracts.MVVM;
using Microsoft.Extensions.Logging;
using CapFrameX.Contracts.Data;
using CapFrameX.EventAggregation.Messages;
using Microsoft.Win32;
using System.Net.Http;
using System.Configuration;
using CapFrameX.Extensions;
using Newtonsoft.Json;
using CapFrameX.Webservice.Data.DTO;
using CapFrameX.Remote;
using CapFrameX.Contracts.Overlay;
using System.Net.NetworkInformation;

namespace CapFrameX.ViewModel
{
    public class ColorbarViewModel : BindableBase
    {
        private readonly IRegionManager _regionManager;
        private readonly IEventAggregator _eventAggregator;
        private readonly IAppConfiguration _appConfiguration;
        private readonly ILogger<ColorbarViewModel> _logger;
        private readonly IShell _shell;
        private readonly ISystemInfo _systemInfo;
        private readonly LoginManager _loginManager;
        private PubSubEvent<AppMessages.OpenLoginWindow> _openLoginWindow;
        public PubSubEvent<ViewMessages.OptionPopupClosed> OptionPopupClosed;
        public PubSubEvent<ViewMessages.ThemeChanged> _themeChanged;

        private bool _captureIsChecked = true;
        private bool _overlayIsChecked;
        private bool _singleRecordIsChecked;
        private bool _recordComparisonIsChecked;
        private bool _reportIsChecked;
        private bool _sensorIsChecked;
        private bool _pmdIsChecked;
        private bool _synchronizationIsChecked;
        private bool _cloudIsChecked;
        private bool _aggregatioIsChecked;
        private bool _hasCustomInfo;
        private string _selectedView = "Options";
        private bool _optionsViewSelected = true;
        private bool _appViewSelected;
        private bool _remoteViewSelected;
        private bool _helpViewSelected;
        private bool _showNotification;
        private DateTime _notificationTimestamp = DateTime.MinValue;

        public string OsdHttpUrl => WebserverFactory.OsdHttpUrl;
        public string OsdWSUrl => WebserverFactory.OsdWSUrl;
        public string SensorsWSUrl => WebserverFactory.SensorsWSUrl;
        public string ActiveSensorsWSUrl => WebserverFactory.ActiveSensorsWSUrl;

        public string CurrentPageName { get; set; }

        public IFileRecordInfo RecordInfo { get; set; }

        public bool CaptureIsChecked
        {
            get { return _captureIsChecked; }
            set
            {
                _captureIsChecked = value;
                RaisePropertyChanged();

                if (value)
                    OnCaptureIsCheckedChanged();
            }
        }

        public bool OverlayIsChecked
        {
            get { return _overlayIsChecked; }
            set
            {
                _overlayIsChecked = value;
                RaisePropertyChanged();

                if (value)
                    OnOverlayIsCheckedChanged();
            }
        }

        public bool SingleRecordIsChecked
        {
            get { return _singleRecordIsChecked; }
            set
            {
                _singleRecordIsChecked = value;
                RaisePropertyChanged();

                if (value)
                    OnSingleRecordIsCheckedChanged();
            }
        }
        public bool AggregationIsChecked
        {
            get { return _aggregatioIsChecked; }
            set
            {
                _aggregatioIsChecked = value;
                RaisePropertyChanged();

                if (value)
                    OnAggregationIsCheckedChanged();
            }
        }

        public bool RecordComparisonIsChecked
        {
            get { return _recordComparisonIsChecked; }
            set
            {
                _recordComparisonIsChecked = value;
                RaisePropertyChanged();

                if (value)
                    OnRecordComparisonIsCheckedChanged();
            }
        }

        public bool SensorIsChecked
        {
            get { return _sensorIsChecked; }
            set
            {
                _sensorIsChecked = value;
                RaisePropertyChanged();

                if (value)
                    OnSensorIsCheckedChanged();
            }
        }

        public bool PmdIsChecked
        {
            get { return _pmdIsChecked; }
            set
            {
                _pmdIsChecked = value;
                RaisePropertyChanged();

                if (value)
                    OnPmdIsCheckedChanged();
            }
        }

        public bool ReportIsChecked
        {
            get { return _reportIsChecked; }
            set
            {
                _reportIsChecked = value;
                RaisePropertyChanged();

                if (value)
                    OnReportIsCheckedChanged();
            }
        }

        public bool SynchronizationIsChecked
        {
            get { return _synchronizationIsChecked; }
            set
            {
                _synchronizationIsChecked = value;
                RaisePropertyChanged();

                if (value)
                    OnSynchronizationIsCheckedChanged();
            }
        }
        public bool CloudIsChecked
        {
            get { return _cloudIsChecked; }
            set
            {
                _cloudIsChecked = value;
                RaisePropertyChanged();

                if (value)
                    OnCloudIsCheckedChanged();
            }
        }

        public int SelectedAverageTimeWindow
        {
            get { return _appConfiguration.IntervalAverageWindowTime; }
            set
            {
                _appConfiguration.IntervalAverageWindowTime = value;
                RaisePropertyChanged();
            }
        }

        public int FpsValuesRoundingDigits
        {
            get { return _appConfiguration.FpsValuesRoundingDigits; }
            set
            {
                _appConfiguration.FpsValuesRoundingDigits = value;
                RaisePropertyChanged();
            }
        }

        public string ScreenshotDirectory
        {
            get { return _appConfiguration.ScreenshotDirectory; }
            set
            {
                _appConfiguration.ScreenshotDirectory = value;
                RaisePropertyChanged();
            }
        }

        public int HorizontalGraphExportRes
        {
            get { return _appConfiguration.HorizontalGraphExportRes; }
            set
            {
                _appConfiguration.HorizontalGraphExportRes = value;
                RaisePropertyChanged();
            }
        }

        public int VerticalGraphExportRes
        {
            get { return _appConfiguration.VerticalGraphExportRes; }
            set
            {
                _appConfiguration.VerticalGraphExportRes = value;
                RaisePropertyChanged();
            }
        }

        public EHardwareInfoSource SelectedHardwareInfoSource
        {
            get { return _appConfiguration.HardwareInfoSource.ConvertToEnum<EHardwareInfoSource>(); }
            set
            {
                _appConfiguration.HardwareInfoSource = value.ConvertToString();
                OnHardwareInfoSourceChanged();
                RaisePropertyChanged();
            }
        }
        public ECaptureFileMode SelectedCaptureFileMode
        {
            get { return _appConfiguration.CaptureFileMode.ConvertToEnum<ECaptureFileMode>(); }
            set
            {
                _appConfiguration.CaptureFileMode = value.ConvertToString();
                RaisePropertyChanged();
            }
        }

        public bool HasCustomInfo
        {
            get { return _hasCustomInfo; }
            set
            {
                _hasCustomInfo = value;
                RaisePropertyChanged();
            }
        }

        public string CustomCpuDescription
        {
            get { return _appConfiguration.CustomCpuDescription; }
            set
            {
                _appConfiguration.CustomCpuDescription = value;
                RaisePropertyChanged();
            }
        }

        public string CustomGpuDescription
        {
            get { return _appConfiguration.CustomGpuDescription; }
            set
            {
                _appConfiguration.CustomGpuDescription = value;
                RaisePropertyChanged();
            }
        }

        public string CustomRamDescription
        {
            get { return _appConfiguration.CustomRamDescription; }
            set
            {
                _appConfiguration.CustomRamDescription = value;
                RaisePropertyChanged();
            }
        }

        public bool IsGpuAccelerationActive
        {
            get { return _appConfiguration.IsGpuAccelerationActive; }
            set
            {
                _appConfiguration.IsGpuAccelerationActive = value;
                RaisePropertyChanged();
            }
        }

        public string SelectedView
        {
            get { return _selectedView; }
            set
            {
                _selectedView = value;
                RaisePropertyChanged();
            }
        }


        public bool OptionsViewSelected
        {
            get { return _optionsViewSelected; }
            set
            {
                _optionsViewSelected = value;
                OnViewSelectionChanged();
                RaisePropertyChanged();
            }
        }

        public bool AppViewSelected
        {
            get { return _appViewSelected; }
            set
            {
                _appViewSelected = value;
                OnViewSelectionChanged();
                RaisePropertyChanged();
            }
        }
        public bool RemoteViewSelected
        {
            get { return _remoteViewSelected; }
            set
            {
                _remoteViewSelected = value;
                OnViewSelectionChanged();
                RaisePropertyChanged();
            }
        }

        public bool HelpViewSelected
        {
            get { return _helpViewSelected; }
            set
            {
                _helpViewSelected = value;
                OnViewSelectionChanged();
                RaisePropertyChanged();
            }
        }

        public bool StartMinimized
        {
            get { return _appConfiguration.StartMinimized; }
            set
            {
                _appConfiguration.StartMinimized = value;
                RaisePropertyChanged();
            }
        }

        public bool Autostart
        {
            get { return _appConfiguration.Autostart; }
            set
            {
                _appConfiguration.Autostart = value;
                RaisePropertyChanged();
                OnAutostartChanged();
            }
        }

        public bool IsDarkModeToggleChecked
        {
            get { return _appConfiguration.UseDarkMode; }
            set
            {
                _appConfiguration.UseDarkMode = value;
                _themeChanged.Publish(new ViewMessages.ThemeChanged());
            }
        }

        public bool ShowNotification
        {
            get { return _showNotification; }
            set
            {
                _showNotification = value;
                RaisePropertyChanged();
            }
        }

        public bool AppNotificationsActive
        {
            get { return _appConfiguration.AppNotificationsActive; }
            set
            {
                _appConfiguration.AppNotificationsActive = value;
                RaisePropertyChanged();
            }
        }

        public bool HideOverlay
        {
            get { return _appConfiguration.HideOverlay; }
            set
            {
                _appConfiguration.HideOverlay = value;
                RaisePropertyChanged();
            }
        }

        public string WebservicePort
        {
            get { return _appConfiguration.WebservicePort; }
            set
            {
                _appConfiguration.WebservicePort = value.Replace(" ", string.Empty);
                RaisePropertyChanged();
            }
        }

        public bool UseTBPSim
        {
            get { return _appConfiguration.UseTBPSim; }
            set
            {
                _appConfiguration.UseTBPSim = value;
                RaisePropertyChanged();
            }
        }

		public bool UseAdlFallback
		{
			get { return _appConfiguration.UseAdlFallback; }
			set
			{
				_appConfiguration.UseAdlFallback = value;
				RaisePropertyChanged();
			}
		}

		public string PingURL
        {
            get { return _appConfiguration.PingURL; }
            set
            {
                if (CheckURL(value))
                    _appConfiguration.PingURL = value;
                else
                    _appConfiguration.PingURL = "google.com";
                RaisePropertyChanged();
            }
        }

        public string AppNotification { get; private set; }

        public string HelpText => File.ReadAllText(@"HelpTexts\ChartControls.rtf");

        public bool IsCompatibleWithRunningOS => CaptureServiceInfo.IsCompatibleWithRunningOS;

        public Array HardwareInfoSourceItems => Enum.GetValues(typeof(EHardwareInfoSource))
                                           .Cast<EHardwareInfoSource>()
                                           .ToArray();
        public Array CaptureFileModeItems => Enum.GetValues(typeof(ECaptureFileMode))
                                   .Cast<ECaptureFileMode>()
                                   .ToArray();

        public Array AverageTimeWindows => new int[] { 250, 500, 1000 };

        public IAppConfiguration AppConfiguration => _appConfiguration;

        public ILogger<ColorbarViewModel> Logger => _logger;

        public IShell Shell => _shell;

        public ICommand SelectScreenshotFolderCommand { get; }

        public ICommand OpenScreenshotFolderCommand { get; }

        public ICommand CloseNotificationCommand { get; }

        public IList<int> RoundingDigits { get; }

        public bool IsLoggedIn { get; private set; }

        public ColorbarViewModel(IRegionManager regionManager,
                                 IEventAggregator eventAggregator,
                                 IAppConfiguration appConfiguration,
                                 ILogger<ColorbarViewModel> logger,
                                 IShell shell,
                                 ISystemInfo systemInfo,
                                 LoginManager loginManager)
        {
            _regionManager = regionManager;
            _eventAggregator = eventAggregator;
            _appConfiguration = appConfiguration;
            _logger = logger;
            _shell = shell;
            _systemInfo = systemInfo;
            _loginManager = loginManager;

            RoundingDigits = new List<int>(Enumerable.Range(0, 8));
            SelectScreenshotFolderCommand = new DelegateCommand(OnSelectScreenshotFolder);
            OpenScreenshotFolderCommand = new DelegateCommand(OnOpenScreenshotFolder);
            CloseNotificationCommand = new DelegateCommand(OnCloseNotification);

            HasCustomInfo = SelectedHardwareInfoSource == EHardwareInfoSource.Custom;
            IsLoggedIn = _loginManager.State.Token != null;
            SetAggregatorEvents();
            SubscribeToAggregatorEvents();
            SetHardwareInfoDefaultsFromDatabase();

            OnAutostartChanged(true);

            if (AppNotificationsActive)
            {
                GetAppNotification();
            }
        }

        public void OpenLoginWindow()
        {
            _openLoginWindow.Publish(new AppMessages.OpenLoginWindow());
        }

        public async void Logout()
        {
            await _loginManager.Logout();
        }

        private void SetHardwareInfoDefaultsFromDatabase()
        {
            if (CustomCpuDescription == "CPU")
                CustomCpuDescription = _systemInfo.GetProcessorName();

            if (CustomGpuDescription == "GPU")
                CustomGpuDescription = _systemInfo.GetGraphicCardName();

            if (CustomRamDescription == "RAM")
                CustomRamDescription = _systemInfo.GetSystemRAMInfoName();
        }

        private void OnSelectScreenshotFolder()
        {
            var dialog = new CommonOpenFileDialog
            {
                IsFolderPicker = true
            };

            CommonFileDialogResult result = dialog.ShowDialog();

            if (result == CommonFileDialogResult.Ok)
            {
                _appConfiguration.ScreenshotDirectory = dialog.FileName;
                ScreenshotDirectory = dialog.FileName;
            }
        }

        private void OnOpenScreenshotFolder()
        {
            try
            {
                var path = _appConfiguration.ScreenshotDirectory;
                if (path.Contains(@"MyDocuments\CapFrameX\Screenshots"))
                {
                    var documentFolder = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                    path = Path.Combine(documentFolder, @"CapFrameX\Screenshots");
                }
                Process.Start(path);
            }
            catch { _logger.LogError("Error while opening screenshot folder."); }
        }

        private void OnCaptureIsCheckedChanged()
        {
            _regionManager.RequestNavigate("DataRegion", "CaptureView");
            CurrentPageName = "Capture";
        }
        private void OnOverlayIsCheckedChanged()
        {
            _regionManager.RequestNavigate("DataRegion", "OverlayView");
            CurrentPageName = "Overlay";
        }

        private void OnSingleRecordIsCheckedChanged()
        {
            _regionManager.RequestNavigate("DataRegion", "DataView");
            CurrentPageName = "Analysis";
        }

        private void OnAggregationIsCheckedChanged()
        {
            _regionManager.RequestNavigate("DataRegion", "AggregationView");
            CurrentPageName = "Aggregation";
        }

        private void OnRecordComparisonIsCheckedChanged()
        {
            _regionManager.RequestNavigate("DataRegion", "ComparisonView");
            CurrentPageName = "Comparison";
        }

        private void OnSensorIsCheckedChanged()
        {
            _regionManager.RequestNavigate("DataRegion", "SensorView");
            CurrentPageName = "Sensor";
        }

        private void OnPmdIsCheckedChanged()
        {
            _regionManager.RequestNavigate("DataRegion", "PmdView");
            CurrentPageName = "Pmd";
        }

        private void OnReportIsCheckedChanged()
        {
            _regionManager.RequestNavigate("DataRegion", "ReportView");
            CurrentPageName = "Report";
        }

        private void OnSynchronizationIsCheckedChanged()
        {
            _regionManager.RequestNavigate("DataRegion", "SynchronizationView");
            CurrentPageName = "Synchronization";
        }

        private void OnCloudIsCheckedChanged()
        {
            _regionManager.RequestNavigate("DataRegion", "CloudView");
            CurrentPageName = "Cloud";
        }

        private void OnHardwareInfoSourceChanged()
        {
            // mange visibility
            HasCustomInfo = SelectedHardwareInfoSource == EHardwareInfoSource.Custom;

            // manage defaults
            SetHardwareInfoDefaultsFromDatabase();
        }

        private void OnViewSelectionChanged()
        {
            if (OptionsViewSelected)
                SelectedView = "Options";

            if (AppViewSelected)
                SelectedView = "App";

            if (RemoteViewSelected)
            { 
                SelectedView = "Remote"; 
                RaisePropertyChanged(nameof(OsdHttpUrl));
                RaisePropertyChanged(nameof(OsdWSUrl));
                RaisePropertyChanged(nameof(SensorsWSUrl));
                RaisePropertyChanged(nameof(ActiveSensorsWSUrl));
            }

            if (HelpViewSelected)
                SelectedView = "Help";
        }

        private void OnCloseNotification()
        {
            // save notification timestamp to config and disable notification
            _appConfiguration.LastAppNotificationTimestamp = _notificationTimestamp;
            ShowNotification = false;
        }

        public void OnAutostartChanged(bool cleanup = false)
        {
            const string appName = "CapFrameX";

            using (TaskService ts = new TaskService())
            {
                try
                {
                    var taskExists = ts.RootFolder.GetTasks().Any(t => t.Name == appName);

                    if (Autostart && !taskExists)
                    {
                        string appPath = System.Reflection.Assembly.GetEntryAssembly().Location;


                        TaskDefinition td = ts.NewTask();
                        td.RegistrationInfo.Description = "Autostart";
                        td.Settings.DisallowStartIfOnBatteries = false;
                        td.Settings.StopIfGoingOnBatteries = false;
                        td.Principal.RunLevel = TaskRunLevel.Highest;

                        var trigger = new LogonTrigger();
                        trigger.UserId = Environment.UserName;
                        trigger.Delay = TimeSpan.FromSeconds(20);

                        td.Triggers.Add(trigger);


                        td.Actions.Add(new ExecAction(appPath));

                        ts.RootFolder.RegisterTaskDefinition(appName, td, TaskCreation.CreateOrUpdate,
                        Environment.UserDomainName + "\\" + Environment.UserName, null, TaskLogonType.InteractiveToken);

                    }
                    else if (!Autostart && taskExists)
                    {

                        ts.RootFolder.DeleteTask(appName);
                    }
                }
                catch (Exception e)
                {
                    _logger.LogError("Unable to perform autostart task", e);
                }
            }

            if (cleanup)
            {
                // delete old registry autostart option(remove in later versions)
                try
                {
                    RegistryKey startKey = Registry.LocalMachine.OpenSubKey("SOFTWARE\\WOW6432Node\\Microsoft\\Windows\\CurrentVersion\\Run", true);
                    startKey?.DeleteValue(appName);
                }
                catch (ArgumentException) { };
            }
        }

        private void SetAggregatorEvents()
        {
            OptionPopupClosed = _eventAggregator.GetEvent<PubSubEvent<ViewMessages.OptionPopupClosed>>();
            _openLoginWindow = _eventAggregator.GetEvent<PubSubEvent<AppMessages.OpenLoginWindow>>();
            _themeChanged = _eventAggregator.GetEvent<PubSubEvent<ViewMessages.ThemeChanged>>();
        }
        private void SubscribeToAggregatorEvents()
        {
            _eventAggregator.GetEvent<PubSubEvent<AppMessages.LoginState>>()
                .Subscribe(state =>
                {
                    IsLoggedIn = state.IsLoggedIn;
                    RaisePropertyChanged(nameof(IsLoggedIn));
                });

            _eventAggregator.GetEvent<PubSubEvent<ViewMessages.UpdateSession>>()
                .Subscribe(msg =>
                {
                    RecordInfo = msg.RecordInfo;
                });
        }

        private async void GetAppNotification()
        {
            // read notification timestamp from server and compare it with config.
            // If request fails or timestamp is older do nothing, else show notification
            try
            {
                using (var client = new HttpClient()
                {
                    BaseAddress = new Uri(ConfigurationManager.AppSettings["WebserviceUri"])
                })
                {
                    client.DefaultRequestHeaders.AddCXClientUserAgent();
                    var response = await client.GetAsync("appnotification");

                    if (response.IsSuccessStatusCode)
                    {
                        var notification = JsonConvert.DeserializeObject<SqAppNotificationDataDTO>(await response.Content.ReadAsStringAsync());
                        if (notification.IsActive && notification.Timestamp > _appConfiguration.LastAppNotificationTimestamp)
                        {
                            _notificationTimestamp = notification.Timestamp;
                            AppNotification = notification.Message;
                            RaisePropertyChanged(nameof(AppNotification));
                            ShowNotification = true;
                        }
                    }
                }
            }
            catch
            {
                ShowNotification = false;
            }
        }

        private bool CheckURL(string url)
        {
            Ping pingSender = new Ping();
            try 
            { 
                PingReply reply = pingSender.Send(url);
                return true;
            }
            catch { return false; };
        }
    }
}
