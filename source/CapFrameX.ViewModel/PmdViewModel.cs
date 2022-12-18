using CapFrameX.Contracts.Configuration;
using CapFrameX.Contracts.Data;
using CapFrameX.Contracts.PMD;
using CapFrameX.Contracts.Sensor;
using CapFrameX.Data.Session.Contracts;
using CapFrameX.EventAggregation.Messages;
using CapFrameX.Extensions;
using CapFrameX.PMD;
using CapFrameX.Statistics.NetStandard.Contracts;
using Microsoft.Extensions.Logging;
using OxyPlot;
using Prism.Commands;
using Prism.Events;
using Prism.Mvvm;
using Prism.Regions;
using System;
using System.Collections.Generic;
using System.Data.Odbc;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Windows;
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
        private string _lostPackets = "0";
        private string _selectedChartView = "Frametimes";
        private bool _useUpdateSession = false;
        private ISession _session;
        private ISession _previousSession;
        private string _cpuName;
        private string _gpuName;

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

        public PlotModel FrametimeModel => _pmdDataChartManager.PerformanceModel;

        public PmdMetricsManager PmdMetrics => _pmdDataMetricsManager;

        public string CpuName
        {
            get => _cpuName;
            set { _cpuName = value; RaisePropertyChanged(); }
		}
		public string GpuName
		{
			get => _gpuName;
			set { _gpuName = value; RaisePropertyChanged(); }
		}

		public string SessionCpuName => _session?.Info.Processor;
        public string SessionGpuName => _session?.Info.GPU;

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

        public string AvgPmdGPUPower { get; set; } = "NaN W";
        public string AvgPmdCPUPower { get; set; } = "NaN W";
        public string AvgPmdSystemPower { get; set; } = "NaN W";
        public string AvgSensorGPUPower { get; set; } = "NaN W";
        public string AvgSensorCPUPower { get; set; } = "NaN W";
        public double AvgFPS { get; set; } = double.NaN;
        public double GpuEfficiency { get; set; } = double.NaN;
        public double CpuEfficiency { get; set; } = double.NaN;
        public double SystemEfficiency { get; set; } = double.NaN;


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
                _updateCharts = value;
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

        public bool UseLogging
        {
            get => _appConfiguration.UsePmdDataLogging;
            set
            {
                _appConfiguration.UsePmdDataLogging = value;
                RaisePropertyChanged();
            }
        }

        public bool DrawPerformanceChart
        {
            get => _pmdDataChartManager.DrawPerformanceChart;
            set
            {
                _pmdDataChartManager.DrawPerformanceChart = value;
                RaisePropertyChanged();
                _pmdDataChartManager.UpdatePerformanceChart(_session, _selectedChartView);
            }
        }

        public bool DrawPmdPower
        {
            get => _pmdDataChartManager.DrawPmdPower;
            set
            {
                _pmdDataChartManager.DrawPmdPower = value;
                RaisePropertyChanged();
                _pmdDataChartManager.UpdateCpuPowerChart(_session);
                _pmdDataChartManager.UpdateGpuPowerChart(_session, _appConfiguration.UseTBPSim);
            }
        }
        public bool DrawAvgPmdPower
        {
            get => _pmdDataChartManager.DrawAvgPmdPower;
            set
            {
                _pmdDataChartManager.DrawAvgPmdPower = value;
                RaisePropertyChanged();
                _pmdDataChartManager.UpdateCpuPowerChart(_session);
                _pmdDataChartManager.UpdateGpuPowerChart(_session, _appConfiguration.UseTBPSim);
            }
        }

        public bool DrawSensorPower
        {
            get => _pmdDataChartManager.DrawSensorPower;
            set
            {
                _pmdDataChartManager.DrawSensorPower = value;
                RaisePropertyChanged();
                _pmdDataChartManager.UpdateCpuPowerChart(_session);
                _pmdDataChartManager.UpdateGpuPowerChart(_session, _appConfiguration.UseTBPSim);
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

        public string SelectedChartView
        {
            get { return _selectedChartView; }
            set
            {
                _selectedChartView = value;
                RaisePropertyChanged();
                _pmdDataChartManager.UpdatePerformanceChart(_session, value);
            }
        }

        public PmdViewModel(IPmdService pmdService, IAppConfiguration appConfiguration, ISensorService sensorService,
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

            Task.Factory.StartNew(async () =>
            {
                await sensorService.SensorServiceCompletionSource.Task;
                await Task.Delay(500);

				Application.Current.Dispatcher.Invoke(() =>
				{
					CpuName = _systemInfo.GetProcessorName();
		            GpuName = _systemInfo.GetGraphicCardName();
	            });
			});

            SubscribeToAggregatorEvents();

            _pmdService.PmdstatusStream
                .SubscribeOnDispatcher()
                .Subscribe(status => PmdCaptureStatus = status);

            _pmdService.LostPacketsCounterStream
                .SubscribeOnDispatcher()
                .Subscribe(lostPackets => LostPackets = LostPackets.ToString());
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
            ManageChartsUpdate();
            _previousSession = _session;
        }

        public void OnNavigatedTo(NavigationContext navigationContext)
        {
            _useUpdateSession = true;
            ManageChartsUpdate();

            if (_session == null || _session?.Hash != _previousSession?.Hash)
            {
                UpdatePMDAnalysis();
            }
        }

        private void SubscribeToAggregatorEvents()
        {
            _eventAggregator.GetEvent<PubSubEvent<ViewMessages.UpdateSession>>()
                .Subscribe(msg =>
                {
                    _session = msg.CurrentSession;

                    if (_useUpdateSession)
                    {
                        UpdatePMDAnalysis();
                    }
                });

            _eventAggregator.GetEvent<PubSubEvent<ViewMessages.ThemeChanged>>()
                .Subscribe(msg =>
                {
                    _pmdDataChartManager.UseDarkMode = _appConfiguration.UseDarkMode;
                    _pmdDataChartManager.UpdateChartsTheme();
                });
        }

        private void UpdatePMDAnalysis()
        {
            _pmdDataChartManager.UpdateCpuPowerChart(_session);
            _pmdDataChartManager.UpdateGpuPowerChart(_session, _appConfiguration.UseTBPSim);
            _pmdDataChartManager.UpdatePerformanceChart(_session, _selectedChartView);
            _pmdDataChartManager.ResetAnalysisPlotModels();
            RaisePropertyChanged(nameof(SessionCpuName));
            RaisePropertyChanged(nameof(SessionGpuName));
            GetAverageMetrics();
        }

        private void ManagePmdService()
        {
            if (UsePmdService)
            {
                _chartaDataBuffer.Clear();
                _pmdDataChartManager.ResetRealTimePlotModels();
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

        private void ManageChartsUpdate()
        {
            if (!_updateCharts)
            {
                _pmdDataChartManager.ResetRealTimePlotModels();
                _chartaDataBuffer.Clear();
            }
        }

        private void GetAverageMetrics()
        {
            //Initialize values
            double cpuPmdAverage = double.NaN;
            double gpuPmdAverage = double.NaN;
            double systemPmdAverage = double.NaN;
            double cpuSensorAverage = double.NaN;
            double gpuSensorAverage = double.NaN;
            double cpuEfficiency = double.NaN;
            double gpuEfficiency = double.NaN;
            double systemEfficiency = double.NaN;
            double fpsaverage = double.NaN;

            void UpdateMetrics()
            {
                AvgPmdGPUPower = $"{gpuPmdAverage} W";
                AvgPmdCPUPower = $"{cpuPmdAverage} W";
                AvgPmdSystemPower = $"{Math.Round(systemPmdAverage, 0)} W";
                AvgSensorGPUPower = $"{gpuSensorAverage} W";
                AvgSensorCPUPower = $"{cpuSensorAverage} W";
                AvgFPS = fpsaverage;
                GpuEfficiency = Math.Round(gpuEfficiency, 2);
                CpuEfficiency = Math.Round(cpuEfficiency, 2);
                SystemEfficiency = Math.Round(systemEfficiency, 2);

                RaisePropertyChanged(nameof(AvgPmdGPUPower));
                RaisePropertyChanged(nameof(AvgPmdCPUPower));
                RaisePropertyChanged(nameof(AvgSensorGPUPower));
                RaisePropertyChanged(nameof(AvgSensorCPUPower));
                RaisePropertyChanged(nameof(AvgPmdSystemPower));
                RaisePropertyChanged(nameof(AvgFPS));
                RaisePropertyChanged(nameof(GpuEfficiency));
                RaisePropertyChanged(nameof(CpuEfficiency));
                RaisePropertyChanged(nameof(SystemEfficiency));
            }

            if (_session == null || !_session.Runs.Where(r => r.SensorData2 != null).Any())
            {
                UpdateMetrics();
                return;
            }

            // Power & Performance Metrics

            // CPU PMD Power           
            var cpuPmdPowers = _session.Runs.Where(r => !r.PmdCpuPower.IsNullOrEmpty());
            if (cpuPmdPowers != null && cpuPmdPowers.Any())
            {
                cpuPmdAverage = Math.Round(cpuPmdPowers.SelectMany(x => x.PmdCpuPower).Average(), 0);
            }

            // GPU PMD Power           
            var gpuPmdPowers = _session.Runs.Where(r => !r.PmdGpuPower.IsNullOrEmpty());
            if (gpuPmdPowers != null && gpuPmdPowers.Any())
            {
                gpuPmdAverage = Math.Round(gpuPmdPowers.SelectMany(x => x.PmdGpuPower).Average(), 0);
            }

            // System PMD Power           
            var systemPmdPowers = _session.Runs.Where(r => !r.PmdSystemPower.IsNullOrEmpty());
            if (systemPmdPowers != null && systemPmdPowers.Any())
            {
                systemPmdAverage = Math.Round(systemPmdPowers.SelectMany(x => x.PmdSystemPower).Average(), 0);
            }

            // CPU Sensor Power
            var cpuSensorPowers = _session.Runs.Where(r => !r.SensorData2.CpuPower.IsNullOrEmpty());
            if (cpuSensorPowers != null && cpuSensorPowers.Any())
            {
                cpuSensorAverage = Math.Round(cpuSensorPowers.SelectMany(x => x.SensorData2.CpuPower).Average(), 0);
            }

            // GPU Sensor Power
            var gpuSensorPowers = _session.Runs.Where(r => !r.SensorData2.GpuPower.IsNullOrEmpty());

            if (_appConfiguration.UseTBPSim)
            {
                var gpuTBPSimPowers = _session.Runs.Where(r => !r.SensorData2.GpuTBPSim.IsNullOrEmpty());

                if (gpuTBPSimPowers != null && gpuTBPSimPowers.Any())
                {
                    gpuSensorAverage = Math.Round(gpuSensorPowers.SelectMany(x => x.SensorData2.GpuTBPSim).Average(), 0);
                }
                else if (gpuSensorPowers != null && gpuSensorPowers.Any())
                {
                    gpuSensorAverage = Math.Round(gpuSensorPowers.SelectMany(x => x.SensorData2.GpuPower).Average(), 0);
                }

            }
            else if (gpuSensorPowers != null && gpuSensorPowers.Any())
            {
                gpuSensorAverage = Math.Round(gpuSensorPowers.SelectMany(x => x.SensorData2.GpuPower).Average(), 0);
            }

            // FPS
            double frametimes = _session.Runs.SelectMany(r => r.CaptureData.MsBetweenPresents).Average();
            fpsaverage = Math.Round(1000 / frametimes, 1);

            // Efficiency Metrics

            if (!double.IsNaN(gpuPmdAverage))
                gpuEfficiency = fpsaverage / gpuPmdAverage * 10;
            else if (!double.IsNaN(gpuSensorAverage))
                gpuEfficiency = fpsaverage / gpuSensorAverage * 10;

            if (!double.IsNaN(cpuPmdAverage))
                cpuEfficiency = fpsaverage / cpuPmdAverage * 10;
            else if (!double.IsNaN(cpuSensorAverage))
                cpuEfficiency = fpsaverage / cpuSensorAverage * 10;

            if (!double.IsNaN(systemPmdAverage))
                systemEfficiency = fpsaverage / systemPmdAverage * 10;

            UpdateMetrics();
        }
    }
}
