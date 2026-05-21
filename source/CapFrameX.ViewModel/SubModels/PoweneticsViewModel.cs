using CapFrameX.Contracts.Configuration;
using CapFrameX.Contracts.PMD;
using CapFrameX.PMD;
using CapFrameX.PMD.Powenetics;
using OxyPlot;
using Prism.Commands;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Windows.Input;

namespace CapFrameX.ViewModel.SubModels
{
    public class PoweneticsViewModel : PmdViewModelBase
    {
        private bool _updateMetrics = true;
        private bool _usePmdService;
        private string _sampleRate = "0 [1/s]";
        private string _lostPackets = "0";
        private int _pmdDataWindowSeconds = 10;
        private EPmdDriverStatus _pmdCaptureStatus;

        private List<PoweneticsChannel[]> _chartaDataBuffer = new List<PoweneticsChannel[]>(1000 * 10);
        private PoweneticsMetricsManager _pmdDataMetricsManager = new PoweneticsMetricsManager(500, 10);
        private PmdAnalysisChartManager _pmdDataChartManager = new PmdAnalysisChartManager();

        private readonly object _updateChartBufferLock = new object();
        private readonly IPoweneticsService _pmdService;
        private readonly IAppConfiguration _appConfiguration;

        private IDisposable _pmdChannelStreamChartsDisposable;
        private IDisposable _pmdChannelStreamMetricsDisposable;
        private IDisposable _pmdThroughputDisposable;

        public PoweneticsMetricsManager PmdMetrics => _pmdDataMetricsManager;

        public Array ComPortsItemsSource => _pmdService.GetPortNames();

        /// <summary>
        /// Chart length [s]
        /// </summary>
        public Array PmdDataWindows => new[] { 5, 10, 20, 30, 60, 300, 600 };

        /// <summary>
        /// Refresh rates [ms]
        /// </summary>
        public Array DownsamplingFactors => new[] { 1, 2, 5, 10, 20, 50, 100, 200, 250, 500 };

        public Array DownsamplingModes => Enum.GetValues(typeof(PmdSampleFilterMode))
            .Cast<PmdSampleFilterMode>()
            .ToArray();

        public ICommand ResetPmdMetricsCommand { get; }


        public bool UsePmdService
        {
            get => _usePmdService;
            set
            {
                _usePmdService = value;
                RaisePropertyChanged();
                RaisePropertyChanged(nameof(IsComPortEnabled));
                ManagePmdService(value);
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
            get => _appConfiguration.PoweneticsUpdateCharts;
            set
            {
                _appConfiguration.PoweneticsUpdateCharts = value;
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

        public string LostPackets
        {
            get => _lostPackets;
            set
            {
                _lostPackets = value;
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

        public PoweneticsViewModel(IPoweneticsService pmdService, IAppConfiguration appConfiguration)
        {
            _pmdService = pmdService;
            _appConfiguration = appConfiguration;

            ResetPmdMetricsCommand = new DelegateCommand(() => _pmdDataMetricsManager.ResetHistory());

            _pmdDataChartManager.UseDarkMode = _appConfiguration.UseDarkMode;
            _pmdDataChartManager.UpdateChartsTheme();

            _pmdService.PmdStatusStream
                .SubscribeOnDispatcher()
                .Subscribe(status => PmdCaptureStatus = status);

            _pmdService.LostPacketsCounterStream
                .SubscribeOnDispatcher()
                .Subscribe(lostPackets => LostPackets = LostPackets.ToString());
        }

        internal void ManageChartsUpdate()
        {
            if (!UpdateCharts)
            {
                _pmdDataChartManager.ResetRealTimePlotModels();
                _chartaDataBuffer.Clear();
            }
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

        private void ManagePmdService(bool startService)
        {
            if (startService)
            {
                _chartaDataBuffer.Clear();
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

        private void DrawPmdData(IList<PoweneticsChannel[]> chartData)
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

        internal void UpdatePmdDataWindow(int pmdDataWindowSeconds)
        {
            _pmdDataMetricsManager.PmdDataWindowSeconds = pmdDataWindowSeconds;

            lock (_updateChartBufferLock)
            {
                _chartaDataBuffer = new List<PoweneticsChannel[]>(pmdDataWindowSeconds * 1000);
            }
        }
    }
}
