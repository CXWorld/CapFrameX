using CapFrameX.Contracts.Configuration;
using CapFrameX.Contracts.Data;
using CapFrameX.Contracts.Sensor;
using CapFrameX.Data;
using CapFrameX.Data.Session.Contracts;
using CapFrameX.EventAggregation.Messages;
using CapFrameX.Sensor;
using CapFrameX.Sensor.Reporting;
using CapFrameX.Sensor.Reporting.Contracts;
using CapFrameX.Sensor.Reporting.Data;
using CapFrameX.ViewModel.SubModels;
using Microsoft.Extensions.Logging;
using Prism.Commands;
using Prism.Events;
using Prism.Mvvm;
using Prism.Regions;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
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
        private readonly IPathService _pathService;
        private readonly ISensorEntryProvider _sensorEntryProvider;
        private readonly IRecordDataServer _localRecordDataServer;
        private readonly ILogger<SensorViewModel> _logger;
        private readonly CaptureManager _captureManager;
        private readonly IRecordManager _recordManager;
        private readonly ApplicationState _applicationState;
        private ISession _session;
        private ISession _previousSession;
        private int _selectedSensorEntryIndex;
        private SensorEntryWrapper _selectedSensorEntry;
        private bool _saveButtonIsEnable;
        private bool _isActive;
        private bool _aggregateButtonIsEnable = true;
        private string _aggregationButtonText = "Evaluate" + Environment.NewLine + "multiple entries";
        private string _sensorStatisticsText = "Sensor statistics for selected record";
        private bool _selectedRecordChanged;

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
        public string EvaluationMethod
        {
            get
            {
                return _appConfiguration
                  .SensorReportEvaluationMethod;
            }
            set
            {
                _appConfiguration
                   .SensorReportEvaluationMethod = value;
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

        public bool AggregateButtonIsEnable
        {
            get { return _aggregateButtonIsEnable; }
            set
            {
                _aggregateButtonIsEnable = value;
                RaisePropertyChanged();
            }
        }

        public bool CopyRawSensorsEnable { get; set; }

        public string AggregateButtonText
        {
            get { return _aggregationButtonText; }
            set
            {
                _aggregationButtonText = value;
                RaisePropertyChanged();
            }
        }

        public string SensorStatisticsText
        {
            get { return _sensorStatisticsText; }
            set
            {
                _sensorStatisticsText = value;
                RaisePropertyChanged();
            }
        }

        public Array LoggingPeriodItemsSource => new[] { 250, 500 };

        public Array EvaluationMethodItemsSource => new[] { "Aggregate", "Average" };

        public ObservableCollection<SensorEntryWrapper> SensorEntries { get; }
            = new ObservableCollection<SensorEntryWrapper>();

        public ObservableCollection<ISensorReportItem> SensorReportItems { get; }
            = new ObservableCollection<ISensorReportItem>();

        public ICommand SaveConfigCommand { get; }

        public ICommand ResetToDefaultCommand { get; }

        public ICommand CopySensorInfoCommand { get; }

        public ICommand CopyRawSensorInfoCommand { get; }

        public ICommand AggregateSensorEntriesCommand { get; }

        public ICommand OpenConfigFolderCommand { get; }

        public SensorGroupControl SensorSubModelGroupControl { get; }

        public SensorViewModel(IAppConfiguration appConfiguration,
            IEventAggregator eventAggregator,
            ISensorService sensorService,
            IPathService pathService,
            ISensorEntryProvider sensorEntryProvider,
            ILogger<SensorViewModel> logger,
            CaptureManager captureManager,
            IRecordManager recordManager,
            ApplicationState applicationState)
        {
            _appConfiguration = appConfiguration;
            _eventAggregator = eventAggregator;
            _sensorService = sensorService;
            _pathService = pathService;
            _sensorEntryProvider = sensorEntryProvider;
            _logger = logger;
            _captureManager = captureManager;
            _recordManager = recordManager;
            _applicationState = applicationState;
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
            OpenConfigFolderCommand = new DelegateCommand(OnOpenConfigFolder);
            AggregateSensorEntriesCommand = new DelegateCommand(() =>
            {
                Task.Run(() =>
                {
                    if (_applicationState.SelectedRecords != null && _applicationState.SelectedRecords.Count > 1)
                    {
                        AggregateButtonText = "Working";
                        AggregateButtonIsEnable = false;
                        CopyRawSensorsEnable = false;
                        _selectedRecordChanged = false;
                        Thread.Sleep(100);
                        var sessions = _applicationState.SelectedRecords.Select(ri =>
                    {
                        var session = _recordManager.LoadData(ri.FileInfo.FullName);
                        return session;
                    });
                        if (EvaluationMethod == "Average")
                            AverageSensorDataOfSessions(sessions);
                        else
                            AggregateSensorDataOfSessions(sessions);

                        AggregateButtonText = "Evaluate" + Environment.NewLine + "multiple entries";
                        SensorStatisticsText = "Sensor statistics for multiple selected records";
                        AggregateButtonIsEnable = true;
                    }
                });
            });

            _sensorEntryProvider.ConfigChanged = () => SaveButtonIsEnable = true;
            SubscribeToUpdateSession();

            Task.Run(async () =>
            {
                await _sensorService.SensorServiceCompletionSource.Task;
                await SetWrappedSensorEntries();
            });
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
                    CopyRawSensorsEnable = true;
                    _selectedRecordChanged = true;
                    SensorStatisticsText = "Sensor statistics for selected record";
                });
        }

        private void UpdateSensorSessionReport(ISession session)
        {
            SensorReportItems.Clear();

            if (RecordInfo == null || !_isActive)
                return;

            var sessionSensorData = session.Runs.Select(run => run.SensorData2);
            var items = SensorReport.GetFullReportFromSessionSensorData(sessionSensorData);
            foreach (var item in items)
            {
                SensorReportItems.Add(item);
            }
            ;
        }

        private void AggregateSensorDataOfSessions(IEnumerable<ISession> sessions)
        {
            var runs = sessions.SelectMany(s => s.Runs);
            var items = SensorReport.GetFullReportFromSessionSensorData(runs.Select(r => r.SensorData2));

            if (!_selectedRecordChanged)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    SensorReportItems.Clear();
                    foreach (var item in items)
                    {
                        SensorReportItems.Add(item);
                    }                   
                });
            }
        }

        private void AverageSensorDataOfSessions(IEnumerable<ISession> sessions)
        {
            var sessionCount = sessions.Count();
            var sensorReportFromSessions = sessions.SelectMany(s => SensorReport.GetFullReportFromSessionSensorData(s.Runs.Select(r => r.SensorData2)))
                .GroupBy(x => x.Name)
                .Where(x => x.Count() == sessionCount)
                .Select(group => new SensorReportItem()
                {
                    Name = group.Key,
                    AverageValue = Math.Round(group.Average(g => g.AverageValue), group.First().RoundingDigits),
                    MaxValue = Math.Round(group.Average(g => g.MaxValue), group.First().RoundingDigits),
                    MinValue = Math.Round(group.Average(g => g.MinValue), group.First().RoundingDigits)
                });


            if (!_selectedRecordChanged)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    SensorReportItems.Clear();
                    SensorReportItems.AddRange(sensorReportFromSessions);
                });
            }
        }

        private void OnCopySensorInfo()
        {
            if (RecordInfo == null)
                return;

            StringBuilder builder = new StringBuilder();

            foreach (var sensorInfo in SensorReportItems)
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
                builder.AppendLine(string.Join("\t", data.Select(x =>
                {
                    SensorReport.roundingDictionary.TryGetValue(x.Type, out var roundingDigit);
                    return Math.Round(x.Values.ElementAtOrDefault(i), roundingDigit);
                })));
            }

            Clipboard.SetDataObject(builder.ToString(), false);
        }

        private void OnOpenConfigFolder()
        {
            try
            {
                Process.Start(_pathService.ConfigFolder);
            }
            catch { }
        }

        public bool IsNavigationTarget(NavigationContext navigationContext) => true;

        public void OnNavigatedFrom(NavigationContext navigationContext)
        {
            _previousSession = _session;
            _isActive = false;
        }

        public void OnNavigatedTo(NavigationContext navigationContext)
        {
            _isActive = true;
            if (_session?.Hash != _previousSession?.Hash)
                UpdateSensorSessionReport(_session);
        }
    }
}
