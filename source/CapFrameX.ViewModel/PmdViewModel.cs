using CapFrameX.Contracts.Configuration;
using CapFrameX.Contracts.Data;
using CapFrameX.Contracts.PMD;
using CapFrameX.Data;
using CapFrameX.Data.Session.Contracts;
using CapFrameX.EventAggregation.Messages;
using CapFrameX.Extensions;
using CapFrameX.PMD;
using Microsoft.Extensions.Logging;
using OxyPlot;
using Prism.Commands;
using Prism.Events;
using Prism.Mvvm;
using Prism.Regions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Windows.Input;

namespace CapFrameX.ViewModel
{
    public class PmdViewModel : BindableBase, INavigationAware
    {
        private readonly IAppConfiguration _appConfiguration;
        private readonly ILogger<PmdViewModel> _logger;
        private readonly IPmdService _pmdService;
        private readonly object _updateChartBufferLock = new object();
        private readonly IEventAggregator _eventAggregator;
        private readonly ISystemInfo _systemInfo;

        private bool _updateCharts = true;
        private bool _updateMetrics = true;
        private int _pmdDataWindowSeconds = 10;
        private bool _usePmdService;
        private string _sampleRate = "0 [1/s]";
        private bool _useUpdateSession = false;
        private ISession _session;

        private IDisposable _pmdChannelStreamChartsDisposable;
        private IDisposable _pmdChannelStreamMetricsDisposable;
        private IDisposable _pmdThroughputDisposable;
        private List<PmdChannel[]> _chartaDataBuffer = new List<PmdChannel[]>(1000 * 10);
        private PmdDataChartManager _pmdDataChartManager = new PmdDataChartManager();
        private PmdMetricsManager _pmdDataMetricsManager = new PmdMetricsManager(500, 10);
        private EPmdDriverStatus _pmdCaptureStatus;

        public PlotModel EPS12VModel => _pmdDataChartManager.Eps12VModel;

        public PlotModel PciExpressModel => _pmdDataChartManager.PciExpressModel;

        public PlotModel CpuAnalysisModel => _pmdDataChartManager.CpuAnalysisModel;

        public PlotModel GpuAnalysisModel => _pmdDataChartManager.GpuAnalysisModel;

        public PmdMetricsManager PmdMetrics => _pmdDataMetricsManager;

        public string CpuName => _systemInfo.GetProcessorName();

        public string GpuName => _systemInfo.GetGraphicCardName();

        public Array ComPortsItemsSource => _pmdService.GetPortNames();

        /// <summary>
        /// Refresh rates [ms]
        /// </summary>
        public Array DownsamplingFactors => new[] { 1, 2, 5, 10, 20, 50, 100, 200, 250, 500 };

        public Array DownsamplingModes => Enum.GetValues(typeof(PmdSampleFilterMode))
                                             .Cast<PmdSampleFilterMode>()
                                             .ToArray();

        /// <summary>
        /// Chart length [s]
        /// </summary>
        public Array PmdDataWindows => new[] { 5, 10, 20, 30, 60, 300, 600 };

        public ICommand ResetPmdMetricsCommand { get; }

        public bool UsePmdService
        {
            get => _usePmdService;
            set
            {
                _usePmdService = value;
                RaisePropertyChanged();
                RaisePropertyChanged(nameof(IsComPortEnabled));
                ManagePmdService();
            }
        }

        public bool IsComPortEnabled
        {
            get => !_usePmdService;
        }

        public string SelectedComPort
        {
            get => _pmdService.PortName;
            set
            {
                _pmdService.PortName = value;
                RaisePropertyChanged();
            }
        }

        public bool UpdateCharts
        {
            get => _updateCharts;
            set
            {
                ManageChartsUpdate(value);
                RaisePropertyChanged();
            }
        }

        public bool UpdateMetrics
        {
            get => _updateMetrics;
            set
            {
                _updateMetrics = value;
                RaisePropertyChanged();
            }
        }

        public int PmdChartRefreshPeriod
        {
            get => _appConfiguration.PmdChartRefreshPeriod;
            set
            {
                _appConfiguration.PmdChartRefreshPeriod = value;
                SubscribePmdDataStreamCharts();
                RaisePropertyChanged();
            }
        }

        public int PmdMetricRefreshPeriod
        {
            get => _appConfiguration.PmdMetricRefreshPeriod;
            set
            {
                _appConfiguration.PmdMetricRefreshPeriod = value;
                _pmdDataMetricsManager.PmdMetricRefreshPeriod = value;
                SubscribePmdDataStreamMetrics();
                RaisePropertyChanged();
            }
        }

        public EPmdDriverStatus PmdCaptureStatus
        {
            get => _pmdCaptureStatus;
            set
            {
                _pmdCaptureStatus = value;
                RaisePropertyChanged();
            }
        }

        public int SelectedDownSamplingFactor
        {
            get => _pmdService.DownSamplingSize;
            set
            {
                _pmdService.DownSamplingSize = value;
                RaisePropertyChanged();
            }
        }

        public string SampleRate
        {
            get => _sampleRate;
            set
            {
                _sampleRate = value;
                RaisePropertyChanged();
            }
        }

        public PmdSampleFilterMode SelectedDownSamlingMode
        {
            get => _pmdService.DownSamplingMode;
            set
            {
                _pmdService.DownSamplingMode = value;
                RaisePropertyChanged();
            }
        }

        public bool UseLogging
        {
            get => _appConfiguration.UsePmdDataLogging;
            set
            {
                _appConfiguration.UsePmdDataLogging = value;
                RaisePropertyChanged();
            }
        }

        public bool DrawFrametimesCpu
        {
            get => _pmdDataChartManager.DrawFrametimesCpu;
            set
            {
                _pmdDataChartManager.DrawFrametimesCpu = value;
                RaisePropertyChanged();
                _pmdDataChartManager.UpdateCpuPowerFramtimesChart(_session);
            }
        }

        public bool DrawFrametimesGpu
        {
            get => _pmdDataChartManager.DrawFrametimesGpu;
            set
            {
                _pmdDataChartManager.DrawFrametimesGpu = value;
                RaisePropertyChanged();
                _pmdDataChartManager.UpdateGpuPowerFramtimesChart(_session);
            }
        }

        public bool DrawPmdCpuPower
        {
            get => _pmdDataChartManager.DrawPmdCpuPower;
            set
            {
                _pmdDataChartManager.DrawPmdCpuPower = value;
                RaisePropertyChanged();
                _pmdDataChartManager.UpdateCpuPowerFramtimesChart(_session);
            }
        }

        public bool DrawPmdGpuPower
        {
            get => _pmdDataChartManager.DrawPmdGpuPower;
            set
            {
                _pmdDataChartManager.DrawPmdGpuPower = value;
                RaisePropertyChanged();
                _pmdDataChartManager.UpdateGpuPowerFramtimesChart(_session);
            }
        }

        public bool DrawSensorCpuPower
        {
            get => _pmdDataChartManager.DrawSensorCpuPower;
            set
            {
                _pmdDataChartManager.DrawSensorCpuPower = value;
                RaisePropertyChanged();
                _pmdDataChartManager.UpdateCpuPowerFramtimesChart(_session);
            }
        }

        public bool DrawSensorGpuPower
        {
            get => _pmdDataChartManager.DrawSensorGpuPower;
            set
            {
                _pmdDataChartManager.DrawSensorGpuPower = value;
                RaisePropertyChanged();
                _pmdDataChartManager.UpdateGpuPowerFramtimesChart(_session);
            }
        }

        public int SelectedPmdDataWindow
        {
            get => _pmdDataWindowSeconds;
            set
            {
                if (_pmdDataWindowSeconds != value)
                {
                    _pmdDataWindowSeconds = value;
                    _pmdDataMetricsManager.PmdDataWindowSeconds = value;
                    RaisePropertyChanged();

                    lock (_updateChartBufferLock)
                    {
                        _chartaDataBuffer = new List<PmdChannel[]>(_pmdDataWindowSeconds * 1000);
                    }

                    _pmdDataChartManager.AxisDefinitions["X_Axis_Time_GPU"].Maximum = _pmdDataWindowSeconds;
                    _pmdDataChartManager.AxisDefinitions["X_Axis_Time_GPU"].AbsoluteMaximum = _pmdDataWindowSeconds;
                    _pmdDataChartManager.AxisDefinitions["X_Axis_Time_CPU"].Maximum = _pmdDataWindowSeconds;
                    _pmdDataChartManager.AxisDefinitions["X_Axis_Time_CPU"].AbsoluteMaximum = _pmdDataWindowSeconds;
                }
            }
        }

        public PmdViewModel(IPmdService pmdService, IAppConfiguration appConfiguration,
            ILogger<PmdViewModel> logger, IEventAggregator eventAggregator, ISystemInfo systemInfo)
        {
            _pmdService = pmdService;
            _appConfiguration = appConfiguration;
            _eventAggregator = eventAggregator;
            _logger = logger;
            _systemInfo = systemInfo;

            ResetPmdMetricsCommand = new DelegateCommand(() => _pmdDataMetricsManager.ResetHistory());

            _pmdDataChartManager.UseDarkMode = _appConfiguration.UseDarkMode;
            _pmdDataChartManager.UpdateChartsTheme();

            SubscribeToAggregatorEvents();

            _pmdService.PmdstatusStream
                .SubscribeOnDispatcher()
                .Subscribe(status => PmdCaptureStatus = status);
        }

        private void SubscribePmdDataStreamCharts()
        {
            _pmdChannelStreamChartsDisposable?.Dispose();
            _pmdChannelStreamChartsDisposable = _pmdService.PmdChannelStream
                .Buffer(TimeSpan.FromMilliseconds(PmdChartRefreshPeriod))
                .Where(_ => UpdateCharts)
                .SubscribeOn(new EventLoopScheduler())
                .Subscribe(chartData => DrawPmdData(chartData));
        }

        private void SubscribePmdDataStreamMetrics()
        {
            if (_pmdService.PmdChannelStream == null) return;

            _pmdChannelStreamMetricsDisposable?.Dispose();
            _pmdChannelStreamMetricsDisposable = _pmdService.PmdChannelStream
                .Buffer(TimeSpan.FromMilliseconds(PmdMetricRefreshPeriod))
                .Where(_ => UpdateMetrics)
                .SubscribeOn(new EventLoopScheduler())
                .Subscribe(metricsData => _pmdDataMetricsManager.UpdateMetrics(metricsData));
        }

        private void SubscribePmdThroughput()
        {
            if (_pmdService.PmdThroughput == null) return;

            _pmdThroughputDisposable?.Dispose();
            _pmdThroughputDisposable = _pmdService.PmdThroughput
                .SubscribeOnDispatcher()
                .Subscribe(sampleCount => SampleRate = $"{(int)(Math.Round(sampleCount / (2 * 10d)) * 10)} [1/s]");
        }

        private void DrawPmdData(IList<PmdChannel[]> chartData)
        {
            if (!chartData.Any()) return;

            var dataCount = _chartaDataBuffer.Count;
            var lastTimeStamp = chartData.Last()[0].TimeStamp;
            int range = 0;

            IEnumerable<DataPoint> eps12VPowerDrawPoints = null;
            IEnumerable<DataPoint> pciExpressPowerDrawPoints = null;
            lock (_updateChartBufferLock)
            {
                while (range < dataCount && lastTimeStamp - _chartaDataBuffer[range][0].TimeStamp > SelectedPmdDataWindow * 1000L) range++;
                _chartaDataBuffer.RemoveRange(0, range);
                _chartaDataBuffer.AddRange(chartData);

                eps12VPowerDrawPoints = _pmdService.GetEPS12VPowerPmdDataPoints(_chartaDataBuffer)
                    .Select(p => new DataPoint(p.X, p.Y));
                pciExpressPowerDrawPoints = _pmdService.GetPciExpressPowerPmdDataPoints(_chartaDataBuffer)
                    .Select(p => new DataPoint(p.X, p.Y));
            }

            _pmdDataChartManager.DrawEps12VChart(eps12VPowerDrawPoints);
            _pmdDataChartManager.DrawPciExpressChart(pciExpressPowerDrawPoints);
        }

        public bool IsNavigationTarget(NavigationContext navigationContext) => true;

        public void OnNavigatedFrom(NavigationContext navigationContext)
        {
            _useUpdateSession = false;
            ManageChartsUpdate(false);
        }

        public void OnNavigatedTo(NavigationContext navigationContext)
        {
            _useUpdateSession = true;
            ManageChartsUpdate(true);
        }

        private void SubscribeToAggregatorEvents()
        {
            _eventAggregator.GetEvent<PubSubEvent<ViewMessages.UpdateSession>>()
                .Subscribe(msg =>
                {
                    _session = msg.CurrentSession;

                    if (_useUpdateSession)
                    {
                        _pmdDataChartManager.UpdateCpuPowerFramtimesChart(_session);
                        _pmdDataChartManager.UpdateGpuPowerFramtimesChart(_session);
                    }
                });

            _eventAggregator.GetEvent<PubSubEvent<ViewMessages.ThemeChanged>>()
                .Subscribe(msg =>
                {
                    _pmdDataChartManager.UseDarkMode = _appConfiguration.UseDarkMode;
                    _pmdDataChartManager.UpdateChartsTheme();
                });
        }

        private void ManagePmdService()
        {
            if (UsePmdService)
            {
                _chartaDataBuffer.Clear();
                _pmdDataChartManager.ResetAllPLotModels();
                _pmdDataMetricsManager.ResetHistory();
                _pmdService.StartDriver();

                SubscribePmdDataStreamCharts();
                SubscribePmdDataStreamMetrics();
                SubscribePmdThroughput();
            }
            else
            {
                _pmdChannelStreamChartsDisposable?.Dispose();
                _pmdChannelStreamMetricsDisposable?.Dispose();
                _pmdThroughputDisposable?.Dispose();
                _pmdService.ShutDownDriver();
            }
        }

        private void ManageChartsUpdate(bool show)
        {
            if (show)
            {
                _updateCharts = true;
            }
            else
            {
                _updateCharts = false;
                _pmdDataChartManager.ResetAllPLotModels();
                _chartaDataBuffer.Clear();
            }
        }
    }
}
