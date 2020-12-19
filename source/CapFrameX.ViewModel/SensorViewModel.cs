using CapFrameX.Contracts.Configuration;
using CapFrameX.Contracts.Sensor;
using CapFrameX.Data;
using CapFrameX.Sensor;
using Microsoft.Extensions.Logging;
using Prism.Events;
using Prism.Mvvm;
using Prism.Regions;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace CapFrameX.ViewModel
{
    public class SensorViewModel : BindableBase, INavigationAware
    {
        private readonly ISensorService _sensorService;
        private readonly IEventAggregator _eventAggregator;
        private readonly IAppConfiguration _appConfiguration;
        private readonly ISensorEntryProvider _sensorEntryProvider;
        private readonly ILogger<SensorViewModel> _logger;
        private readonly CaptureManager _captureManager;

        private int _selectedSensorEntryIndex;
        private SensorEntryWrapper _selectedSensorEntry;

        public bool UseSensorLogging
        {
            get { return _appConfiguration.UseSensorLogging; }
            set
            {
                _appConfiguration.UseSensorLogging = value;
                _captureManager.ToggleSensorLogging(value);
                //_sensorConfig.GlobalIsActivated = value;
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

        public Array LoggingPeriodItemsSource => new[] { 250, 500 };

        public ObservableCollection<SensorEntryWrapper> SensorEntries { get; }
          = new ObservableCollection<SensorEntryWrapper>();

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

            //// ToDo: Später durch Einzelsteuerungskonzept ersetzen
            //_sensorConfig.GlobalIsActivated = UseSensorLogging;

            _ = Task.Run(async () => await SetWrappedSensorEntries());

            stopwatch.Stop();
            _logger.LogInformation(this.GetType().Name + " {initializationTime}s initialization time", Math.Round(stopwatch.ElapsedMilliseconds * 1E-03, 1));
        }

        private async Task SetWrappedSensorEntries()
        {
            var wrappedSensorEntries = await _sensorEntryProvider.GetWrappedSensorEntries();

            Application.Current.Dispatcher.Invoke(() =>
            {
                SensorEntries.AddRange(wrappedSensorEntries.Select(entry => entry as SensorEntryWrapper));
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
    }
}
