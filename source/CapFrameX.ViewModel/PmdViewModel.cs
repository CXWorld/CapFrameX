using CapFrameX.Contracts.Configuration;
using CapFrameX.Extensions;
using CapFrameX.PMD;
using CapFrameX.Statistics.PlotBuilder;
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
using System.Windows.Threading;

namespace CapFrameX.ViewModel
{
    public class PmdViewModel : BindableBase, INavigationAware
    {
        private readonly IAppConfiguration _appConfiguration;
        private readonly ILogger<PmdViewModel> _logger;
        private readonly IPmdService _pmdService;
        private readonly object _updateChartBufferLock = new object();

        private bool _updateCharts = true;
        private ISubject<TimeSpan> _chartUpdateSubject;
        private int _chartWindowSeconds = 10;
        private List<PmdChannel[]> _chartaDataBuffer = new List<PmdChannel[]>(1000 * 10);

        public PlotModel EPS12VModel = new PlotModel
        {
            PlotMargins = new OxyThickness(50, 0, 50, 35),
            PlotAreaBorderColor = OxyColor.FromArgb(64, 204, 204, 204)
        };

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
                _chartUpdateSubject.OnNext(TimeSpan.FromMilliseconds(value));
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
                }
            }
        }

        public PmdViewModel(IPmdService pmdService, IAppConfiguration appConfiguration,
            ILogger<PmdViewModel> logger)
        {
            _pmdService = pmdService;
            _appConfiguration = appConfiguration;
            _logger = logger;

            _chartUpdateSubject = new BehaviorSubject<TimeSpan>(TimeSpan.FromMilliseconds(PmdChartRefreshPeriod));

            SubscribePmdDataStreamCharts();
        }

        private void SubscribePmdDataStreamCharts()
        {
            _pmdService.PmdChannelStream
                .Buffer(_chartUpdateSubject)
                .Where(_ => UpdateCharts)
                .SubscribeOn(new EventLoopScheduler())
                .Subscribe(chartData => DrawPmdData(chartData));
        }

        private void DrawPmdData(IList<PmdChannel[]> chartData)
        {
            if (!chartData.Any()) return;

            var lastTimeStamp = chartData.Last()[0].TimeStamp;
            int range = 0;
            lock (_updateChartBufferLock)
            {
                while (lastTimeStamp - _chartaDataBuffer[range][0].TimeStamp > ChartWindowSeconds * 1000L) range++;
                _chartaDataBuffer.RemoveRange(0, range);
                _chartaDataBuffer.AddRange(chartData);
                var eps12VPowerDrawPoints = _pmdService.GetEPS12VPowerPmdDataPoints(_chartaDataBuffer)
                    .Select(p => new DataPoint(p.X, p.Y));
            }

            Dispatcher.CurrentDispatcher.Invoke(() =>
            {
                EPS12VModel.Series.Clear();

                var eps12VPowerSeries = new LineSeries
                {
                    Title = "CPU (Sum EPS 12V)",
                    StrokeThickness = 1,
                    Color = OxyColors.Black,
                    EdgeRenderingMode = EdgeRenderingMode.PreferSpeed
                };

                EPS12VModel.Series.Add(eps12VPowerSeries);
                EPS12VModel.InvalidatePlot(true);
            });
        }

        public bool IsNavigationTarget(NavigationContext navigationContext) => true;

        public void OnNavigatedFrom(NavigationContext navigationContext)
        {
            UpdateCharts = false;
            _pmdService.ShutDownDriver();
        }

        public void OnNavigatedTo(NavigationContext navigationContext)
        {
            UpdateCharts = true;
            _pmdService.PortName = _pmdService.GetPortNames().FirstOrDefault();
            _pmdService.StartDriver();
        }
    }
}
