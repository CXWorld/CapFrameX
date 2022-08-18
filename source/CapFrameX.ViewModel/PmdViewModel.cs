using CapFrameX.Contracts.Configuration;
using CapFrameX.Contracts.Data;
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
using System.Reactive.Subjects;
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

        private IDisposable _pmdChannelStreamChartsDisposable;
        private IDisposable _pmdChannelStreamMetricsDisposable;
        private List<PmdChannel[]> _chartaDataBuffer = new List<PmdChannel[]>(1000 * 10);
        private PmdDataChartManager _pmdDataChartManager = new PmdDataChartManager();
        private PmdMetricsManager _pmdDataMetricsManager = new PmdMetricsManager(500, 10);

        public PlotModel EPS12VModel => _pmdDataChartManager.Eps12VModel;

        public PlotModel PciExpressModel => _pmdDataChartManager.PciExpressModel;

        public PmdMetricsManager PmdMetrics => _pmdDataMetricsManager;

        public string CpuName => _systemInfo.GetProcessorName();

        public string GpuName => _systemInfo.GetGraphicCardName();

        public ICommand ResetPmdMetricsCommand { get; }

        public bool UpdateCharts
        {
            get => _updateCharts;
            set
            {
                _updateCharts = value;
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

        public int ChartDownSamplingSize
        {
            get => _appConfiguration.ChartDownSamplingSize;
            set
            {
                _appConfiguration.ChartDownSamplingSize = value;
                RaisePropertyChanged();
            }
        }

        public int PmdDataWindowSeconds
        {
            get => _pmdDataWindowSeconds;
            set
            {
                if (_pmdDataWindowSeconds != value)
                {
                    var oldChartWindowSeconds = _pmdDataWindowSeconds;
                    _pmdDataWindowSeconds = value;
                    _pmdDataMetricsManager.PmdDataWindowSeconds = value;
                    RaisePropertyChanged();

                    var newChartBuffer = new List<PmdChannel[]>(_pmdDataWindowSeconds * 1000);
                    lock (_updateChartBufferLock)
                    {
                        newChartBuffer.AddRange(_chartaDataBuffer.TakeLast(oldChartWindowSeconds * 1000));
                        _chartaDataBuffer = newChartBuffer;
                    }

                    _pmdDataChartManager.AxisDefinitions["X_Axis_Time"].AbsoluteMaximum = _pmdDataWindowSeconds;
                    EPS12VModel.InvalidatePlot(false);
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

            UpdatePmdChart();
            SubscribeToThemeChanged();
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
            _pmdChannelStreamMetricsDisposable?.Dispose();
            _pmdChannelStreamMetricsDisposable = _pmdService.PmdChannelStream
                .Buffer(TimeSpan.FromMilliseconds(PmdMetricRefreshPeriod))
                .Where(_ => UpdateMetrics)
                .SubscribeOn(new EventLoopScheduler())
                .Subscribe(metricsData => _pmdDataMetricsManager.UpdateMetrics(metricsData));
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
                while (range < dataCount && lastTimeStamp - _chartaDataBuffer[range][0].TimeStamp > PmdDataWindowSeconds * 1000L) range++;
                _chartaDataBuffer.RemoveRange(0, range);
                _chartaDataBuffer.AddRange(chartData);

                eps12VPowerDrawPoints = _pmdService.GetEPS12VPowerPmdDataPoints(_chartaDataBuffer, ChartDownSamplingSize)
                    .Select(p => new DataPoint(p.X, p.Y));
                pciExpressPowerDrawPoints = _pmdService.GetPciExpressPowerPmdDataPoints(_chartaDataBuffer, ChartDownSamplingSize)
                    .Select(p => new DataPoint(p.X, p.Y));
            }

            _pmdDataChartManager.DrawEps12VChart(eps12VPowerDrawPoints);
            _pmdDataChartManager.DrawPciExpressChart(pciExpressPowerDrawPoints);
        }

        public bool IsNavigationTarget(NavigationContext navigationContext) => true;

        public void OnNavigatedFrom(NavigationContext navigationContext)
        {
            _pmdChannelStreamChartsDisposable?.Dispose();
            _pmdService.ShutDownDriver();
        }

        public void OnNavigatedTo(NavigationContext navigationContext)
        {
            _chartaDataBuffer.Clear();
            _pmdDataChartManager.ResetAllPLotModels();
            _pmdService.PortName = _pmdService.GetPortNames().FirstOrDefault();
            _pmdService.StartDriver();

            SubscribePmdDataStreamCharts();
            SubscribePmdDataStreamMetrics();
        }

        private void SubscribeToThemeChanged()
        {
            _eventAggregator.GetEvent<PubSubEvent<ViewMessages.ThemeChanged>>()
                              .Subscribe(msg =>
                              {
                                  UpdatePmdChart();
                              });
        }

        private void UpdatePmdChart()
        {
            _pmdDataChartManager.UseDarkMode = _appConfiguration.UseDarkMode;
            _pmdDataChartManager.UpdateCharts();
        }
    }
}
