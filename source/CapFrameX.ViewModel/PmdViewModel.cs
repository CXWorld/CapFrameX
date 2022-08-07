using CapFrameX.Contracts.Configuration;
using CapFrameX.Extensions;
using CapFrameX.PMD;
using CapFrameX.Statistics.PlotBuilder;
using Microsoft.Extensions.Logging;
using OxyPlot;
using OxyPlot.Axes;
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
        private List<double> _ePS12VModelMaxYValueBuffer = new List<double>(10);
        private List<PmdChannel[]> _chartaDataBuffer = new List<PmdChannel[]>(1000 * 10);
        PlotModel _ePS12VModel = new PlotModel
        {
            PlotMargins = new OxyThickness(50, 0, 50, 60),
            PlotAreaBorderColor = OxyColor.FromArgb(64, 204, 204, 204)
        };

        public PlotModel EPS12VModel => _ePS12VModel;

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

                    _axisDefinitions["X_Axis_Time"].AbsoluteMaximum = _chartWindowSeconds;
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

            _chartUpdateSubject = new BehaviorSubject<TimeSpan>(TimeSpan.FromMilliseconds(PmdChartRefreshPeriod));

            EPS12VModel.Axes.Add(_axisDefinitions["X_Axis_Time"]);
            EPS12VModel.Axes.Add(_axisDefinitions["Y_Axis_CPU_W"]);

            SubscribePmdDataStreamCharts();
        }

        private void SubscribePmdDataStreamCharts()
        {
            _pmdService.PmdChannelStream
                .Buffer(/*_chartUpdateSubject*/TimeSpan.FromMilliseconds(PmdChartRefreshPeriod))
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
            lock (_updateChartBufferLock)
            {
                while (range < dataCount && lastTimeStamp - _chartaDataBuffer[range][0].TimeStamp > ChartWindowSeconds * 1000L) range++;
                _chartaDataBuffer.RemoveRange(0, range);
                _chartaDataBuffer.AddRange(chartData);
                eps12VPowerDrawPoints = _pmdService.GetEPS12VPowerPmdDataPoints(_chartaDataBuffer, ChartDownSamplingSize)
                    .Select(p => new DataPoint(p.X, p.Y));
            }

            // Set maximum y-axis
            if (_ePS12VModelMaxYValueBuffer.Count == 10) _ePS12VModelMaxYValueBuffer.RemoveAt(0);
            _ePS12VModelMaxYValueBuffer.Add((int)Math.Ceiling(1.05 * eps12VPowerDrawPoints.Max(pnt => pnt.Y) / 20.0) * 20);
            var y_Axis_CPU_W_Max = _ePS12VModelMaxYValueBuffer.Max();
            _axisDefinitions["Y_Axis_CPU_W"].Maximum = y_Axis_CPU_W_Max;
            _axisDefinitions["Y_Axis_CPU_W"].AbsoluteMaximum = y_Axis_CPU_W_Max;

            EPS12VModel.Series.Clear();
            Dispatcher.CurrentDispatcher.Invoke(() =>
            {
                var eps12VPowerSeries = new LineSeries
                {
                    Title = "CPU (Sum EPS 12V)",
                    StrokeThickness = 1,
                    Color = OxyColors.Black,
                    EdgeRenderingMode = EdgeRenderingMode.PreferSpeed
                };

                eps12VPowerSeries.Points.AddRange(eps12VPowerDrawPoints);
                EPS12VModel.Series.Add(eps12VPowerSeries);
                EPS12VModel.InvalidatePlot(true);
            });
        }

        private Dictionary<string, LinearAxis> _axisDefinitions { get; }
            = new Dictionary<string, LinearAxis>() {
            { "Y_Axis_CPU_W", new LinearAxis()
                {
                    Key = "Y_Axis_CPU_W",
                    Position = AxisPosition.Left,
                    Title = "Power Consumption CPU [W]",
                    FontSize = 13,
                    MajorGridlineStyle = LineStyle.None,
                    MajorStep = 20,
                    MinorTickSize = 0,
                    MajorTickSize = 0,
                    Minimum = 0,
                    Maximum = 150,
                    AbsoluteMinimum = 0,
                    AbsoluteMaximum = 150,
                    AxisTitleDistance = 15
                }
            },
            { "Y_Axis_GPU_W", new LinearAxis()
                {
                    Key = "Y_Axis_GPU_W",
                    Position = AxisPosition.Right,
                    Title = "Power Consumption GPU [W]",
                    FontSize = 13,
                    MajorGridlineStyle = LineStyle.None,
                    MajorStep = 20,
                    MinorTickSize = 0,
                    MajorTickSize = 0,
                    Minimum = 0,
                    Maximum = 300,
                    AbsoluteMinimum = 0,
                    AbsoluteMaximum = 300,
                    AxisTitleDistance = 15
                }
            },
            { "X_Axis_Time", new LinearAxis()
                {
                    Key = "X_Axis_Time",
                    Position = AxisPosition.Bottom,
                    Title = "Time [s]",
                    FontSize = 13,
                    MajorGridlineStyle = LineStyle.None,
                    MinorTickSize = 0,
                    MajorTickSize = 0,
                    Minimum = 0,
                    Maximum = 10,
                    AbsoluteMinimum = 0,
                    AbsoluteMaximum = 10,
                    AxisTitleDistance = 15
                }
            },
        };

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
