using CapFrameX.Contracts.Configuration;
using CapFrameX.Contracts.PMD;
using CapFrameX.PMD;
using CapFrameX.PMD.Benchlab;
using OxyPlot;
using Prism.Commands;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Threading;

namespace CapFrameX.ViewModel.SubModels
{
    public class BenchlabViewModel : PmdViewModelBase
    {
        private bool _updateMetrics = true;
        private bool _usePmdService;
        private int _pmdDataWindowSeconds = 10;
        private EPmdServiceStatus _pmdServiceStatus = EPmdServiceStatus.Waiting;

        private List<SensorSample> _chartaDataBuffer = new List<SensorSample>(1000 * 10);
        private BenchlabMetricsManager _pmdDataMetricsManager;
        private PmdAnalysisChartManager _pmdDataChartManager = new PmdAnalysisChartManager();

        private readonly object _updateChartBufferLock = new object();
        private readonly IBenchlabService _pmdService;
        private readonly IAppConfiguration _appConfiguration;

        private IDisposable _pmdChannelStreamChartsDisposable;
        private IDisposable _pmdChannelStreamMetricsDisposable;

        public BenchlabMetricsManager PmdMetrics => _pmdDataMetricsManager;

        /// <summary>
        /// Chart length [s]
        /// </summary>
        public Array PmdDataWindows => new[] { 5, 10, 20, 30, 60, 300, 600 };

        /// <summary>
        /// Refresh intervals [ms]
        /// </summary>
        public Array MonitoringIntervals => new[] { 25, 50, 100, 200, 250, 500, 1000, 2000 };

        public ICommand ResetPmdMetricsCommand { get; }

        public bool UsePmdService
        {
            get => _usePmdService;
            set
            {
                if (_usePmdService == value)
                {
                    return;
                }

                _usePmdService = value;
                RaisePropertyChanged();
                ManagePmdService(value);
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

        public bool UpdateCharts
        {
            get => _appConfiguration.BenchlabUpdateCharts;
            set
            {
                _appConfiguration.BenchlabUpdateCharts = value;
                ManageChartsUpdate();
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

        public bool AutoStartPmd
        {
            get => _appConfiguration.BenchlabAutoStartPmd;
            set
            {
                _appConfiguration.BenchlabAutoStartPmd = value;
                RaisePropertyChanged();
            }
        }

        public int SelectedMonitoringInterval
        {
            get => _pmdService.MonitoringInterval;
            set
            {
                if (_pmdService.MonitoringInterval != value)
                {
                    _pmdService.MonitoringInterval = value;
                    RaisePropertyChanged();
                }
            }
        }

        public EPmdServiceStatus PmdServiceStatus
        {
            get => _pmdServiceStatus;
            set
            {
                _pmdServiceStatus = value;
                RaisePropertyChanged();
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
                    RaisePropertyChanged();

                    UpdatePmdDataWindow(_pmdDataWindowSeconds);

                    _pmdDataChartManager.AxisDefinitions["X_Axis_Time_GPU"].Maximum = _pmdDataWindowSeconds;
                    _pmdDataChartManager.AxisDefinitions["X_Axis_Time_GPU"].AbsoluteMaximum = _pmdDataWindowSeconds;
                    _pmdDataChartManager.AxisDefinitions["X_Axis_Time_CPU"].Maximum = _pmdDataWindowSeconds;
                    _pmdDataChartManager.AxisDefinitions["X_Axis_Time_CPU"].AbsoluteMaximum = _pmdDataWindowSeconds;
                }
            }
        }

        public PlotModel EPS12VModel => _pmdDataChartManager.Eps12VModel;

        public PlotModel PciExpressModel => _pmdDataChartManager.PciExpressModel;

        public BenchlabViewModel(IBenchlabService benchlabService, IAppConfiguration appConfiguration)
        {
            _pmdService = benchlabService;
            _appConfiguration = appConfiguration;

            _pmdDataMetricsManager = new BenchlabMetricsManager(benchlabService, 500, 10);

            ResetPmdMetricsCommand = new DelegateCommand(() => _pmdDataMetricsManager.ResetHistory());
            SelectedMonitoringInterval = _pmdService.MonitoringInterval;

            _pmdDataChartManager.UseDarkMode = _appConfiguration.UseDarkMode;
            _pmdDataChartManager.UpdateChartsTheme();

            _pmdService.PmdServiceStatusStream
                .Subscribe(status =>
                {
                    Dispatcher.CurrentDispatcher.Invoke(() =>
                    {
                        PmdServiceStatus = status;

                        if (status != EPmdServiceStatus.Running)
                        {
                            _pmdDataChartManager.ResetRealTimePlotModels();
                            _chartaDataBuffer.Clear();
                        }
                    });
                });
        }

        private void SubscribePmdDataStreamCharts()
        {
            _pmdChannelStreamChartsDisposable?.Dispose();
            _pmdChannelStreamChartsDisposable = _pmdService.PmdSensorStream
                .Buffer(TimeSpan.FromMilliseconds(PmdChartRefreshPeriod))
                .Where(_ => UpdateCharts)
                .SubscribeOn(new EventLoopScheduler())
                .Subscribe(chartData => DrawPmdData(chartData));
        }

        private void SubscribePmdDataStreamMetrics()
        {
            if (_pmdService.PmdSensorStream == null) return;

            _pmdChannelStreamMetricsDisposable?.Dispose();
            _pmdChannelStreamMetricsDisposable = _pmdService.PmdSensorStream
                .Buffer(TimeSpan.FromMilliseconds(PmdMetricRefreshPeriod))
                .Where(_ => UpdateMetrics)
                .SubscribeOn(new EventLoopScheduler())
                .Subscribe(metricsData => _pmdDataMetricsManager.UpdateMetrics(metricsData));
        }

        private void DrawPmdData(IList<SensorSample> chartData)
        {
            if (!chartData.Any()) return;

            var dataCount = _chartaDataBuffer.Count;
            var lastTimeStamp = chartData.Last().TimeStamp;
            int range = 0;

            IEnumerable<DataPoint> eps12VPowerDrawPoints = null;
            IEnumerable<DataPoint> pciExpressPowerDrawPoints = null;
            lock (_updateChartBufferLock)
            {
                while (range < dataCount && lastTimeStamp - _chartaDataBuffer[range].TimeStamp > SelectedPmdDataWindow * 1000L) range++;
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

        private void ManagePmdService(bool startService)
        {
            if (startService)
            {
                SubscribePmdDataStreamCharts();
                SubscribePmdDataStreamMetrics();

                _chartaDataBuffer.Clear();
                _pmdDataMetricsManager.ResetHistory();
                Task.Run(async () => await _pmdService.StartService());
            }
            else
            {
                _pmdChannelStreamChartsDisposable?.Dispose();
                _pmdChannelStreamMetricsDisposable?.Dispose();
                _pmdService.ShutDownService();
            }
        }

        internal void ManageChartsUpdate()
        {
            if (!UpdateCharts)
            {
                _pmdDataChartManager.ResetRealTimePlotModels();
                _chartaDataBuffer.Clear();
            }
        }

        internal void UpdatePmdDataWindow(int pmdDataWindowSeconds)
        {
            _pmdDataMetricsManager.PmdDataWindowSeconds = pmdDataWindowSeconds;

            lock (_updateChartBufferLock)
            {
                _chartaDataBuffer = new List<SensorSample>(pmdDataWindowSeconds * 1000);
            }
        }

        internal void AutoStartPmdService()
        {
            if (!AutoStartPmd || UsePmdService || PmdServiceStatus == EPmdServiceStatus.Running)
            {
                return;
            }

            UsePmdService = true;
        }
    }
}
