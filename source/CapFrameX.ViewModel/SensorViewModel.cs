using CapFrameX.Contracts.Configuration;
using CapFrameX.Contracts.Data;
using CapFrameX.Contracts.Sensor;
using CapFrameX.Data;
using CapFrameX.Data.Session.Contracts;
using CapFrameX.EventAggregation.Messages;
using CapFrameX.Sensor;
using CapFrameX.Sensor.Reporting;
using CapFrameX.Sensor.Reporting.Contracts;
using CapFrameX.ViewModel.SubModels;
using Microsoft.Extensions.Logging;
using Prism.Commands;
using Prism.Events;
using Prism.Mvvm;
using Prism.Regions;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace CapFrameX.ViewModel
{
    public class SensorViewModel : BindableBase, INavigationAware
    {
        private readonly ISensorService _sensorService;
        private readonly IEventAggregator _eventAggregator;
        private readonly IAppConfiguration _appConfiguration;
        private readonly ISensorEntryProvider _sensorEntryProvider;
        private readonly IRecordDataServer _localRecordDataServer;
        private readonly ILogger<SensorViewModel> _logger;
        private readonly CaptureManager _captureManager;

        private ISession _session;
        private int _selectedSensorEntryIndex;
        private SensorEntryWrapper _selectedSensorEntry;
        private bool _saveButtonIsEnable;

        public IFileRecordInfo RecordInfo { get; private set; }

        public bool UseSensorLogging
        {
            get { return _appConfiguration.UseSensorLogging; }
            set
            {
                _appConfiguration.UseSensorLogging = value;
                _captureManager.ToggleSensorLogging(value);
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

        public int SelectedSensorEntryIndex
        {
            get => _selectedSensorEntryIndex;
            set
            {
                _selectedSensorEntryIndex = value;
                RaisePropertyChanged();
            }
        }

        public SensorEntryWrapper SelectedSensorEntry
        {
            get => _selectedSensorEntry;
            set
            {
                _selectedSensorEntry = value;
                RaisePropertyChanged();
            }
        }

        public bool SaveButtonIsEnable
        {
            get => _saveButtonIsEnable;
            set
            {
                _saveButtonIsEnable = value;
                RaisePropertyChanged();
            }
        }

        public Array LoggingPeriodItemsSource => new[] { 250, 500 };

        public ObservableCollection<SensorEntryWrapper> SensorEntries { get; }
            = new ObservableCollection<SensorEntryWrapper>();

        public ObservableCollection<ISensorReportItem> SensorReportItems { get; }
            = new ObservableCollection<ISensorReportItem>();

        public ICommand SaveConfigCommand { get; }

        public ICommand ResetToDefaultCommand { get; }

        public ICommand CopySensorInfoCommand { get; }

        public ICommand CopyRawSensorInfoCommand { get; }

        public SensorGroupControl SensorSubModelGroupControl { get; }

        public SensorViewModel(IAppConfiguration appConfiguration,
            IEventAggregator eventAggregator,
            ISensorService sensorService,
            ISensorEntryProvider sensorEntryProvider,
            ILogger<SensorViewModel> logger,
            CaptureManager captureManager)
        {
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();

            _appConfiguration = appConfiguration;
            _eventAggregator = eventAggregator;
            _sensorService = sensorService;
            _sensorEntryProvider = sensorEntryProvider;
            _logger = logger;
            _captureManager = captureManager;

            _localRecordDataServer = new LocalRecordDataServer(appConfiguration);
            // define submodels
            SensorSubModelGroupControl = new SensorGroupControl(this);

            SaveConfigCommand = new DelegateCommand(
                async () =>
                {
                    await _sensorEntryProvider.SaveSensorConfig();
                    SaveButtonIsEnable = false;
                });

            CopySensorInfoCommand = new DelegateCommand(OnCopySensorInfo);
            CopyRawSensorInfoCommand = new DelegateCommand(OnCopyRawSensorInfo);
            ResetToDefaultCommand = new DelegateCommand(OnResetToDefault);
            _sensorEntryProvider.ConfigChanged = () => SaveButtonIsEnable = true;

            Task.Run(async () => await SetWrappedSensorEntries());
            SubscribeToUpdateSession();



            stopwatch.Stop();
            _logger.LogInformation(this.GetType().Name + " {initializationTime}s initialization time", Math.Round(stopwatch.ElapsedMilliseconds * 1E-03, 1));
        }

        private void OnResetToDefault()
        {
            foreach (var entry in SensorEntries)
            {
                entry.UseForLogging = _sensorEntryProvider.GetIsDefaultActiveSensor(entry);
            }
        }

        private async Task SetWrappedSensorEntries()
        {
            var wrappedSensorEntries = await _sensorEntryProvider.GetWrappedSensorEntries();

            Application.Current.Dispatcher.Invoke(() =>
            {
                SensorEntries.AddRange(wrappedSensorEntries.Select(entry => entry as SensorEntryWrapper));
            });
        }

        private void SubscribeToUpdateSession()
        {
            _eventAggregator.GetEvent<PubSubEvent<ViewMessages.UpdateSession>>()
                            .Subscribe(msg =>
                            {
                                _session = msg.CurrentSession;
                                RecordInfo = msg.RecordInfo;
                                UpdateSensorSessionReport(msg.CurrentSession);
                            });
        }

        private void UpdateSensorSessionReport(ISession session)
        {
            SensorReportItems.Clear();

            if (RecordInfo == null)
                return;
           
            var items = SensorReport.GetFullReportFromSessionSensorData(session.Runs.Select(run => run.SensorData2));
            foreach (var item in items)
            {
                SensorReportItems.Add(item);
            };
        }

        private void OnCopySensorInfo()
        {
            if (RecordInfo == null)
                return;

            var sensorInfos = SensorReport.GetFullReportFromSessionSensorData(_session.Runs.Select(run => run.SensorData2));

            StringBuilder builder = new StringBuilder();

            foreach (var sensorInfo in sensorInfos)
            {
                builder.Append(sensorInfo.Name + "\t" + sensorInfo.MinValue + "\t" +
                sensorInfo.AverageValue + "\t" + sensorInfo.MaxValue + Environment.NewLine);
            }

            Clipboard.SetDataObject(builder.ToString(), false);
        }

        private void OnCopyRawSensorInfo()
        {
            if (RecordInfo == null)
                return;

            var data = SensorReport.GetSensorReportEntries(_session.Runs.Select(s => s.SensorData2))
                .Where(x => x.Name != "BetweenMeasureTime");


            StringBuilder builder = new StringBuilder();

            // Header
            builder.AppendLine(string.Join("\t", data.Select(x => x.DisplayName)));



            for (int i = 0; i < data.First().Values.Count(); i++)
            {
                builder.AppendLine(string.Join("\t", data.Select(x => {
                    SensorReport.roundingDictionary.TryGetValue(x.Type, out var roundingDigit);
                    return Math.Round(x.Values[i], roundingDigit);
                })));
            }

            Clipboard.SetDataObject(builder.ToString(), false);
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
    }
}
