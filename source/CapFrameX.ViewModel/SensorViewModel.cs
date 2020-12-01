using CapFrameX.Contracts.Configuration;
using CapFrameX.Contracts.Sensor;
using CapFrameX.Data;
using Microsoft.Extensions.Logging;
using Prism.Events;
using Prism.Mvvm;
using Prism.Regions;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CapFrameX.ViewModel
{
    public class SensorViewModel : BindableBase, INavigationAware
    {
        private readonly ISensorService _sensorService;
        private readonly IEventAggregator _eventAggregator;
        private readonly IAppConfiguration _appConfiguration;
        private readonly ISensorConfig _sensorConfig;
        private readonly ILogger<SensorViewModel> _logger;
        private readonly CaptureManager _captureManager;

        public bool UseSensorLogging
        {
            get { return _appConfiguration.UseSensorLogging; }
            set
            {
                _appConfiguration.UseSensorLogging = value;
                _captureManager.ToggleSensorLogging(value);
                _sensorConfig.GlobalIsActivated = value;
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

        public Array LoggingPeriodItemsSource => new[] { 250, 500 };

        public SensorViewModel(IAppConfiguration appConfiguration,
                               IEventAggregator eventAggregator,
                               ISensorService sensorService,
                               ILogger<SensorViewModel> logger,
                               CaptureManager captureManager,
                               ISensorConfig sensorConfig)
        {
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();

            _appConfiguration = appConfiguration;
            _eventAggregator = eventAggregator;
            _sensorService = sensorService;
            _logger = logger;
            _captureManager = captureManager;
            _sensorConfig = sensorConfig;

            _sensorConfig.GlobalIsActivated = UseSensorLogging;

            stopwatch.Stop();
            _logger.LogInformation(this.GetType().Name + " {initializationTime}s initialization time", Math.Round(stopwatch.ElapsedMilliseconds * 1E-03, 1));
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
