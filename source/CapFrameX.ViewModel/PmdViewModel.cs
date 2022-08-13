using CapFrameX.Contracts.Configuration;
using CapFrameX.Extensions;
using CapFrameX.PMD;
using Microsoft.Extensions.Logging;
using OxyPlot;
using Prism.Mvvm;
using Prism.Regions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;

namespace CapFrameX.ViewModel
{
    public class PmdViewModel : BindableBase, INavigationAware
    {
        private readonly IAppConfiguration _appConfiguration;
        private readonly ILogger<PmdViewModel> _logger;
        private readonly IPmdService _pmdService;
        private readonly object _updateChartBufferLock = new object();

        private bool _updateCharts = true;
        private int _chartWindowSeconds = 10;
        private IDisposable _pmdChannelStreamDisposable;
        private List<PmdChannel[]> _chartaDataBuffer = new List<PmdChannel[]>(1000 * 10);
        private PmdDataChartManager _pmdDataChartManager = new PmdDataChartManager();

        public PlotModel EPS12VModel => _pmdDataChartManager.Eps12VModel;

        public PlotModel PciExpressModel => _pmdDataChartManager.PciExpressModel;

        public bool UpdateCharts
        {
            get => _updateCharts;
            set
            {
                _updateCharts = value;
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

        public int ChartDownSamplingSize
        {
            get => _appConfiguration.ChartDownSamplingSize;
            set
            {
                _appConfiguration.ChartDownSamplingSize = value;
                RaisePropertyChanged();
            }
        }

        public int ChartWindowSeconds
        {
            get => _chartWindowSeconds;
            set
            {
                if (_chartWindowSeconds != value)
                {
                    var oldChartWindowSeconds = _chartWindowSeconds;
                    _chartWindowSeconds = value;
                    RaisePropertyChanged();

                    var newChartBuffer = new List<PmdChannel[]>(_chartWindowSeconds * 1000);
                    lock (_updateChartBufferLock)
                    {
                        newChartBuffer.AddRange(_chartaDataBuffer.TakeLast(oldChartWindowSeconds * 1000));
                        _chartaDataBuffer = newChartBuffer;
                    }

                    _pmdDataChartManager.AxisDefinitions["X_Axis_Time"].AbsoluteMaximum = _chartWindowSeconds;
                    EPS12VModel.InvalidatePlot(false);
                }
            }
        }

        public PmdViewModel(IPmdService pmdService, IAppConfiguration appConfiguration,
            ILogger<PmdViewModel> logger)
        {
            _pmdService = pmdService;
            _appConfiguration = appConfiguration;
            _logger = logger;
        }

        private void SubscribePmdDataStreamCharts()
        {
            _pmdChannelStreamDisposable?.Dispose();
            _pmdChannelStreamDisposable = _pmdService.PmdChannelStream
                .Buffer(TimeSpan.FromMilliseconds(PmdChartRefreshPeriod))
                .Where(_ => UpdateCharts)
                .SubscribeOn(new EventLoopScheduler())
                .Subscribe(chartData => DrawPmdData(chartData));
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
                while (range < dataCount && lastTimeStamp - _chartaDataBuffer[range][0].TimeStamp > ChartWindowSeconds * 1000L) range++;
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
            _pmdChannelStreamDisposable?.Dispose();
            _pmdService.ShutDownDriver();
        }

        public void OnNavigatedTo(NavigationContext navigationContext)
        {
            _chartaDataBuffer.Clear();
            _pmdDataChartManager.ResetAllPLotModels();
            _pmdService.PortName = _pmdService.GetPortNames().FirstOrDefault();
            _pmdService.StartDriver();

            SubscribePmdDataStreamCharts();
        }
    }
}
