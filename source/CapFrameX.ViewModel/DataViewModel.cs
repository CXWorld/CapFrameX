using CapFrameX.Contracts.Configuration;
using CapFrameX.Contracts.Data;
using CapFrameX.EventAggregation.Messages;
using CapFrameX.Data;
using CapFrameX.ViewModel.DataContext;
using LiveCharts;
using LiveCharts.Defaults;
using LiveCharts.Wpf;
using Prism.Commands;
using Prism.Events;
using Prism.Mvvm;
using Prism.Regions;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reactive;
using System.Reactive.Subjects;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using CapFrameX.MVVM.Dialogs;
using CapFrameX.Data.Session.Contracts;
using System.Collections.ObjectModel;
using System.Reactive.Linq;
using CapFrameX.Sensor.Reporting.Contracts;
using CapFrameX.Sensor.Reporting;
using CapFrameX.Statistics.NetStandard.Contracts;
using CapFrameX.Statistics.PlotBuilder.Contracts;
using Microsoft.Extensions.Logging;
using CapFrameX.Extensions;
using CapFrameX.Statistics.NetStandard;

namespace CapFrameX.ViewModel
{
    public partial class DataViewModel : BindableBase, INavigationAware
    {
        private readonly IStatisticProvider _frametimeStatisticProvider;
        private readonly IFrametimeAnalyzer _frametimeAnalyzer;
        private readonly IEventAggregator _eventAggregator;
        private readonly IAppConfiguration _appConfiguration;
        private readonly RecordManager _recordManager;
        private readonly IRecordDataServer _localRecordDataServer;
        private static ILogger<DataViewModel> _logger;

        private bool _useUpdateSession;
        private ISession _session;
        private ISession _previousSession;
        private SeriesCollection _statisticCollection;
        private SeriesCollection _lShapeCollection;
        private SeriesCollection _stutteringStatisticCollection;
        private SeriesCollection _variancetatisticCollection;
        private string[] _parameterLabels;
        private string[] _lShapeLabels;
        private string[] _advancedParameterLabels;
        private bool _removeOutliers;
        private List<ISystemInfoEntry> _systemInfos;
        private bool _isRangeSliderActive;
        private bool _doUpdateCharts = true;
        private Func<double, string> _parameterFormatter;
        private TabItem _selectedChartItem;
        private string _currentGameName;
        private double _maxRecordingTime;
        private string _remainingRecordingTime;
        private double _firstSeconds;
        private double _lastSeconds;
        private EChartYAxisSetting _selecetedChartYAxisSetting = EChartYAxisSetting.FullFit;
        private ConditionalMessageDialog _messageDialogContent;
        private bool _messageDialogContentIsOpen;
        private string _messageText;
        private int _barMaxValue = 100;
        private bool _showGpuLoad;
        private bool _showCpuLoad;
        private bool _showCpuMaxThreadLoad;
        private bool _showGpuPowerLimit;
        private bool _showPcLatency;
        private bool _aggregationSeparators;
        private bool _showStutteringThresholds;
        private string _avgPcLatency;
        private bool _isPcLatencyAvailable;
        private bool _isCpuLoadAvailable;
        private bool _isCpuMaxLoadAvailable;
        private bool _isGpuLoadAvailable;
        private bool _isGpuPowerLimitAvailable;
        private bool _isGpuActiveChartAvailable;
        private bool _showGpuActiveChart;
        private bool _useFrametimeStatisticParameters;
        private EFilterMode _selectedFilterMode = EFilterMode.None;
        private ELShapeMetrics _lShapeMetric = ELShapeMetrics.Frametimes;
        private string _lShapeYaxisLabel = "Frametimes (ms)" + Environment.NewLine + " ";

        private ISubject<Unit> _onUpdateChart = new BehaviorSubject<Unit>(default);

        public IFileRecordInfo RecordInfo { get; private set; }

        public FrametimeGraphDataContext FrametimeGraphDataContext { get; }

        public FpsGraphDataContext FpsGraphDataContext { get; }

        public IAppConfiguration AppConfiguration => _appConfiguration;

        public Array ChartYAxisSettings => Enum.GetValues(typeof(EChartYAxisSetting));

        public Array FilterModes => Enum.GetValues(typeof(EFilterMode))
                                          .Cast<EFilterMode>()
                                          .ToArray();

        public Array LShapeMetrics => Enum.GetValues(typeof(ELShapeMetrics))
                                  .Cast<ELShapeMetrics>()
                                  .ToArray();

        public ObservableCollection<ISensorReportItem> SensorReportItems { get; }
            = new ObservableCollection<ISensorReportItem>();

        /// <summary>
        /// https://docs.microsoft.com/en-us/dotnet/standard/base-types/standard-numeric-format-strings
        /// </summary>
        public Func<double, string> ParameterFormatter
        {
            get { return _parameterFormatter; }
            set
            {
                _parameterFormatter = value;
                RaisePropertyChanged();
            }
        }

        /// <summary>
        /// https://docs.microsoft.com/en-us/dotnet/standard/base-types/standard-numeric-format-strings
        /// </summary>
        public Func<ChartPoint, string> PieChartPointLabel { get; } =
            chartPoint => string.Format(CultureInfo.InvariantCulture, "{0:0.##} ({1:P})", chartPoint.Y, chartPoint.Participation);

        public ISubject<Unit> ResetLShapeChart = new Subject<Unit>();

        public string[] ParameterLabels
        {
            get { return _parameterLabels; }
            set
            {
                _parameterLabels = value;
                RaisePropertyChanged();
            }
        }

        public string[] LShapeLabels
        {
            get { return _lShapeLabels; }
            set
            {
                _lShapeLabels = value;
                RaisePropertyChanged();
            }
        }

        public string[] AdvancedParameterLabels
        {
            get { return _advancedParameterLabels; }
            set
            {
                _advancedParameterLabels = value;
                RaisePropertyChanged();
            }
        }

        public SeriesCollection StatisticCollection
        {
            get { return _statisticCollection; }
            set
            {
                _statisticCollection = value;
                RaisePropertyChanged();
            }
        }

        public SeriesCollection LShapeCollection
        {
            get { return _lShapeCollection; }
            set
            {
                _lShapeCollection = value;
                RaisePropertyChanged();
            }
        }

        public SeriesCollection StutteringStatisticCollection
        {
            get { return _stutteringStatisticCollection; }
            set
            {
                _stutteringStatisticCollection = value;
                RaisePropertyChanged();
            }
        }

        public SeriesCollection VarianceStatisticCollection
        {
            get { return _variancetatisticCollection; }
            set
            {
                _variancetatisticCollection = value;
                RaisePropertyChanged();
            }
        }

        public bool RemoveOutliers
        {
            get { return _removeOutliers; }
            set
            {
                _removeOutliers = value;
                RaisePropertyChanged();
                OnRemoveOutliersChanged();
            }
        }

        public TabItem SelectedChartItem
        {
            get { return _selectedChartItem; }
            set
            {
                _selectedChartItem = value;
                RaisePropertyChanged();
                OnChartItemChanged();
            }
        }

        public List<ISystemInfoEntry> SystemInfos
        {
            get { return _systemInfos; }
            set { _systemInfos = value; RaisePropertyChanged(); }
        }

        public bool IsRangeSliderActive
        {
            get { return _isRangeSliderActive; }
            set
            {
                _isRangeSliderActive = value;
                RaisePropertyChanged();
                OnSliderRangeChanged();
            }
        }

        public string CurrentGameName
        {
            get { return _currentGameName; }
            set
            {
                _currentGameName = value;
                RaisePropertyChanged();
            }
        }

        public string RemainingRecordingTime
        {
            get { return _remainingRecordingTime; }
            set
            {
                _remainingRecordingTime = value;
                RaisePropertyChanged();
            }
        }

        public double MaxRecordingTime
        {
            get { return _maxRecordingTime; }
            set
            {
                _maxRecordingTime = value;
                RaisePropertyChanged();
                RaisePropertyChanged(nameof(MinRangeSliderRange));
            }
        }

        public double MinRangeSliderRange
        {
            get { return MaxRecordingTime * 0.01; }
        }

        public double FirstSeconds
        {
            get { return _firstSeconds; }
            set
            {
                _firstSeconds = value;
                OnRangeSliderValueChanged();
                RaisePropertyChanged();
            }
        }

        public double LastSeconds
        {
            get { return _lastSeconds; }
            set
            {
                _lastSeconds = value;
                OnRangeSliderValueChanged();
                RaisePropertyChanged();
            }
        }

        public double StutteringFactor
        {
            get { return _appConfiguration.StutteringFactor; }
            set
            {
                _appConfiguration.StutteringFactor = value;
                RaisePropertyChanged();
                _onUpdateChart.OnNext(default);
            }
        }

        public double StutteringLowFPSThreshold
        {
            get { return _appConfiguration.StutteringThreshold; }
            set
            {
                _appConfiguration.StutteringThreshold = value;
                RaisePropertyChanged();
                _onUpdateChart.OnNext(default);
            }
        }
        public EChartYAxisSetting SelecetedChartYAxisSetting
        {
            get { return _selecetedChartYAxisSetting; }
            set
            {
                _selecetedChartYAxisSetting = value;
                RaisePropertyChanged();
                SetFrametimeChartYAxisSetting(GetYAxisSettingFromSelection(value));
            }
        }

        public ELShapeMetrics SelectedLShapeMetric
        {
            get { return _lShapeMetric; }
            set
            {
                _lShapeMetric = value;
                LShapeYaxisLabel = value == ELShapeMetrics.Frametimes ? "Frametimes (ms)" + Environment.NewLine + " " : "FPS" + Environment.NewLine + " ";
                RaisePropertyChanged();
                UpdateSecondaryCharts();
            }
        }

        public string LShapeYaxisLabel
        {
            get { return _lShapeYaxisLabel; }
            set
            {
                _lShapeYaxisLabel = value;
                RaisePropertyChanged();
            }
        }

        public bool NeverShowDialog
        {
            get { return !_appConfiguration.ShowOutlierWarning; }
            set
            {
                _appConfiguration.ShowOutlierWarning = !value;
                RaisePropertyChanged();
            }
        }

        public ConditionalMessageDialog MessageDialogContent
        {
            get { return _messageDialogContent; }
            set
            {
                _messageDialogContent = value;
                RaisePropertyChanged();
            }
        }

        public bool MessageDialogContentIsOpen
        {
            get { return _messageDialogContentIsOpen; }
            set
            {
                _messageDialogContentIsOpen = value;
                RaisePropertyChanged();
            }
        }

        public string MessageText
        {
            get { return _messageText; }
            set
            {
                _messageText = value;
                RaisePropertyChanged();
            }
        }

        public int BarMaxValue
        {
            get { return _barMaxValue; }
            set
            {
                _barMaxValue = value;
                RaisePropertyChanged();
                RaisePropertyChanged(nameof(BarChartSeparators));
            }
        }

        public string AvgPcLatency
        {
            get { return _avgPcLatency; }
            set
            {
                _avgPcLatency = value;
                RaisePropertyChanged();
            }
        }

        public bool IsPcLatencyAvailable
        {
            get { return _isPcLatencyAvailable; }
            set
            {
                _isPcLatencyAvailable = value;
                RaisePropertyChanged();
            }
        }

        public bool IsCpuLoadAvailable
        {
            get { return _isCpuLoadAvailable; }
            set
            {
                _isCpuLoadAvailable = value;
                RaisePropertyChanged();
            }
        }

        public bool IsCpuMaxLoadAvailable
        {
            get { return _isCpuMaxLoadAvailable; }
            set
            {
                _isCpuMaxLoadAvailable = value;
                RaisePropertyChanged();
            }
        }

        public bool IsGpuLoadAvailable
        {
            get { return _isGpuLoadAvailable; }
            set
            {
                _isGpuLoadAvailable = value;
                RaisePropertyChanged();
            }
        }

        public bool IsGpuPowerLimitAvailable
        {
            get { return _isGpuPowerLimitAvailable; }
            set
            {
                _isGpuPowerLimitAvailable = value;
                RaisePropertyChanged();
            }
        }

        public bool IsGpuActiveChartAvailable
        {
            get { return _isGpuActiveChartAvailable; }
            set
            {
                _isGpuActiveChartAvailable = value;
                RaisePropertyChanged();
            }
        }

        public int BarChartSeparators
        {
            get
            {
                int steps;
                double maxValueFracture = _barMaxValue / 3;

                if (maxValueFracture <= 10)
                    steps = 3;
                else if (maxValueFracture <= 15)
                    steps = 5;
                else if (maxValueFracture <= 20)
                    steps = 10;
                else if (maxValueFracture <= 25)
                    steps = 15;
                else if (maxValueFracture <= 50)
                    steps = 25;
                else if (maxValueFracture <= 75)
                    steps = 50;
                else if (maxValueFracture <= 100)
                    steps = 75;
                else if (maxValueFracture <= 150)
                    steps = 100;
                else if (maxValueFracture <= 250)
                    steps = 150;
                else if (maxValueFracture <= 350)
                    steps = 200;
                else if (maxValueFracture <= 500)
                    steps = 300;
                else if (maxValueFracture <= 750)
                    steps = 500;
                else if (maxValueFracture <= 1000)
                    steps = 750;
                else if (maxValueFracture <= 2000)
                    steps = 1000;
                else if (maxValueFracture <= 3000)
                    steps = 2000;
                else
                    steps = 3000;

                return steps;
            }
        }

        public bool AdditionalGraphsEnabled
        {
            get => _session == null ? false
                : _session.Runs.Any(r => r.SensorData != null
                || r.SensorData2 != null
                || _session.Runs.All(run => !run.CaptureData.PcLatency.IsNullOrEmpty()));
        }

        public ICommand CopyStatisticalParameterCommand { get; }

        public ICommand CopyLShapeQuantilesCommand { get; }

        public ICommand CopySystemInfoCommand { get; }

        public ICommand CopySensorInfoCommand { get; }

        public ICommand CopyRawSensorInfoCommand { get; }

        public ICommand CutRecordCommand { get; }

        public ICommand CutRecordInverseCommand { get; }

        public bool ShowGpuLoad
        {
            get => _showGpuLoad;
            set
            {
                _showGpuLoad = value;
                RaisePropertyChanged();
                _onUpdateChart.OnNext(default);
            }
        }

        public bool ShowCpuLoad
        {
            get => _showCpuLoad;
            set
            {
                _showCpuLoad = value;
                RaisePropertyChanged();
                _onUpdateChart.OnNext(default);
            }
        }

        public bool ShowCpuMaxThreadLoad
        {
            get => _showCpuMaxThreadLoad;
            set
            {
                _showCpuMaxThreadLoad = value;
                RaisePropertyChanged();
                _onUpdateChart.OnNext(default);
            }
        }

        public bool ShowGpuPowerLimit
        {
            get => _showGpuPowerLimit && IsGpuPowerLimitAvailable;
            set
            {
                _showGpuPowerLimit = value;
                RaisePropertyChanged();
                _onUpdateChart.OnNext(default);
            }
        }

        public bool ShowPcLatency
        {
            get => _showPcLatency;
            set
            {
                _showPcLatency = value;
                RaisePropertyChanged();
                _onUpdateChart.OnNext(default);
            }
        }

        public bool ShowGpuActiveChart
        {
            get => _showGpuActiveChart;
            set
            {
                _showGpuActiveChart = value;
                RaisePropertyChanged();
                _onUpdateChart.OnNext(default);
            }
        }

        public EFilterMode SelectedFilterMode
        {
            get { return _selectedFilterMode; }
            set
            {
                _selectedFilterMode = value;
                RaisePropertyChanged();
                OnFilterModeChanged();
            }
        }

        public bool ShowAggregationSeparators
        {
            get { return _aggregationSeparators; }
            set
            {
                _aggregationSeparators = value;
                RaisePropertyChanged();
                _onUpdateChart.OnNext(default);
            }
        }

        public bool ShowStutteringThresholds
        {
            get { return _showStutteringThresholds; }
            set
            {
                _showStutteringThresholds = value;
                RaisePropertyChanged();
                _onUpdateChart.OnNext(default);
            }
        }

        public bool UseFrametimeStatisticParameters
        {
            get { return _useFrametimeStatisticParameters; }
            set
            {
                _useFrametimeStatisticParameters = value;
                RaisePropertyChanged();
                UpdateMainCharts();
            }
        }

        public double GpuActiveDeviationPercentage
        {
            get { return ShowGpuActiveChart ? _localRecordDataServer.GetGpuActiveDeviationPercentage() : 0; }

        }

        public DataViewModel(IStatisticProvider frametimeStatisticProvider,
                             IFrametimeAnalyzer frametimeAnalyzer,
                             IEventAggregator eventAggregator,
                             IAppConfiguration appConfiguration,
                             RecordManager recordManager,
                             ILogger<DataViewModel> logger)
        {
            _frametimeStatisticProvider = frametimeStatisticProvider;
            _frametimeAnalyzer = frametimeAnalyzer;
            _eventAggregator = eventAggregator;
            _appConfiguration = appConfiguration;
            _recordManager = recordManager;
            _logger = logger;

            CopyStatisticalParameterCommand = new DelegateCommand(OnCopyStatisticalParameter);
            CopyLShapeQuantilesCommand = new DelegateCommand(OnCopyQuantiles);
            CopySystemInfoCommand = new DelegateCommand(OnCopySystemInfoCommand);
            CopySensorInfoCommand = new DelegateCommand(OnCopySensorInfo);
            CopyRawSensorInfoCommand = new DelegateCommand(OnCopyRawSensorInfo);
            CutRecordCommand = new DelegateCommand(() => OnCutRecord(false));
            CutRecordInverseCommand = new DelegateCommand(() => OnCutRecord(true));
            CopyFPSThresholdDataCommand = new DelegateCommand(OnCopyFPSThresholdData);
            ThresholdCountsCommand = new DelegateCommand(() => _appConfiguration.ShowThresholdTimes = false);
            ThresholdTimesCommand = new DelegateCommand(() => _appConfiguration.ShowThresholdTimes = true);

            ParameterFormatter = value => value.ToString(CultureInfo.InvariantCulture);
            _localRecordDataServer = new LocalRecordDataServer(appConfiguration);
            FrametimeGraphDataContext = new FrametimeGraphDataContext(_localRecordDataServer,
                _appConfiguration, _frametimeStatisticProvider, _eventAggregator);
            FpsGraphDataContext = new FpsGraphDataContext(_localRecordDataServer,
                _appConfiguration, _frametimeStatisticProvider, _eventAggregator);

            MessageDialogContent = new ConditionalMessageDialog();

            SubscribeToAggregatorEvents();
            InitializeStatisticParameter();
            SetThresholdLabels();
            Setup();
        }

        private bool GetIsPowerLimitAvailable()
        {
            if (_session == null)
                return false;

            if (_session.Runs == null
                || !_session.Runs.Any())
                return false;

            if (_session.Runs
                .All(run => run.SensorData != null)
               && _session.Runs
                .All(run => !run.SensorData.GPUPowerLimit.IsNullOrEmpty()))
                return true;

            if (_session.Runs
                .All(run => run.SensorData2 != null)
               && _session.Runs
                .All(run => !run.SensorData2.GPUPowerLimit.IsNullOrEmpty()))
                return true;

            return false;
        }

        private bool GetIsGpuActiveChartAvailable()
        {
            if (_session == null)
                return false;

            if (_session.Runs == null
                || !_session.Runs.Any())
                return false;

            if (_session.Runs
                .All(run => run.CaptureData != null)
                && _session.Runs
                .All(run => !run.CaptureData.GpuActive.IsNullOrEmpty()))
                return true;

            return false;
        }

        private void Setup()
        {
            _onUpdateChart.Subscribe(_ =>
            {
                FpsGraphDataContext.BuildPlotmodel(new VisibleGraphs(ShowGpuLoad, ShowCpuLoad, ShowCpuMaxThreadLoad, ShowGpuPowerLimit, ShowPcLatency, ShowAggregationSeparators, ShowStutteringThresholds, StutteringFactor, StutteringLowFPSThreshold, ShowGpuActiveChart));

                FrametimeGraphDataContext.BuildPlotmodel(new VisibleGraphs(ShowGpuLoad, ShowCpuLoad, ShowCpuMaxThreadLoad, ShowGpuPowerLimit, ShowPcLatency, ShowAggregationSeparators, ShowStutteringThresholds, StutteringFactor, StutteringLowFPSThreshold, ShowGpuActiveChart), plotModel =>
                {
                    FrametimeGraphDataContext.UpdateAxis(EPlotAxis.YAXISFRAMETIMES, axis =>
                    {
                        var tuple = GetYAxisSettingFromSelection(SelecetedChartYAxisSetting);
                        SetFrametimeChartYAxisSetting(tuple);
                    });
                });
                RaisePropertyChanged(nameof(GpuActiveDeviationPercentage));
            });
        }

        partial void InitializeStatisticParameter();

        private void OnAcceptParameterSettings()
        {
            Task.Factory.StartNew(() => SetStaticChart(GetFrametimesSubset(), GetGpuActiveTimesSubset()));
        }

        private void OnSliderRangeChanged()
        {
            if (_session == null)
                return;

            UpdateRangeSliderParameter();
            UpdateMainCharts();
            UpdateSecondaryCharts();
        }

        private void OnRangeSliderValueChanged()
        {
            _localRecordDataServer.SetTimeWindow(FirstSeconds, LastSeconds - FirstSeconds);
            RealTimeUpdateCharts();
            UpdateSecondaryCharts();
            RemainingRecordingTime = "(" + Math.Round(LastSeconds - FirstSeconds, 2)
                .ToString("0.00", CultureInfo.InvariantCulture) + " s)";
        }

        private void OnCopyStatisticalParameter()
        {
            if (_session == null)
                return;
            var gpuActiveTimes = GetGpuActiveTimesSubset();
            var frametimes = GetFrametimesSubset();

            double GeMetricValue(IList<double> sequence, EMetric metric) =>
                Math.Round(_frametimeStatisticProvider.GetFpsMetricValue(sequence, metric), 2);

            var max = GeMetricValue(frametimes, EMetric.Max);
            var p99_quantile = GeMetricValue(frametimes, EMetric.P99);
            var p95_quantile = GeMetricValue(frametimes, EMetric.P95);
            var median = GeMetricValue(frametimes, EMetric.Median);
            var average = GeMetricValue(frametimes, EMetric.Average);
            var gpuActiveAverage = GeMetricValue(gpuActiveTimes, EMetric.GpuActiveAverage);
            var p0dot1_quantile = GeMetricValue(frametimes, EMetric.P0dot1);
            var p0dot2_quantile = GeMetricValue(frametimes, EMetric.P0dot2);
            var p1_quantile = GeMetricValue(frametimes, EMetric.P1);
            var gpuActiveP1_quantile = GeMetricValue(gpuActiveTimes, EMetric.GpuActiveP1);
            var p5_quantile = GeMetricValue(frametimes, EMetric.P5);
            var p1_LowAverage = GeMetricValue(frametimes, EMetric.OnePercentLowAverage);
            var GpuActiveP1_LowAverage = GeMetricValue(gpuActiveTimes, EMetric.GpuActiveOnePercentLowAverage);
            var p0dot1_LowAverage = GeMetricValue(frametimes, EMetric.ZerodotOnePercentLowAverage);
            var p1_LowIntegral = GeMetricValue(frametimes, EMetric.OnePercentLowIntegral);
            var p0dot1_LowIntegral = GeMetricValue(frametimes, EMetric.ZerodotOnePercentLowIntegral);

            var min = GeMetricValue(frametimes, EMetric.Min);
            var adaptiveStandardDeviation = GeMetricValue(frametimes, EMetric.AdaptiveStd);
            var cpuFpsPerWatt = _frametimeStatisticProvider
                 .GetPhysicalMetricValue(frametimes, EMetric.CpuFpsPerWatt,
                 SensorReport.GetAverageSensorValues(_session.Runs.Select(run => run.SensorData2), EReportSensorName.CpuPower,
                 _localRecordDataServer.CurrentTime, _localRecordDataServer.CurrentTime + _localRecordDataServer.WindowLength));
            var gpuFpsPerWatt = _frametimeStatisticProvider
            .GetPhysicalMetricValue(frametimes, EMetric.GpuFpsPerWatt,
            SensorReport.GetAverageSensorValues(_session.Runs.Select(run => run.SensorData2), EReportSensorName.GpuPower,
            _localRecordDataServer.CurrentTime, _localRecordDataServer.CurrentTime + _localRecordDataServer.WindowLength, _appConfiguration.UseTBPSim));

            StringBuilder builder = new StringBuilder();

            // Vice versa!
            // "Adaptive STDEV","Min","0.1% Low Integral","0.1% Low Average" ,"0.1%","0.2%" ,"1% Low Integral","1% Low Average", "1%" ,"5%" ,"Average" ,"95%" ,"99%" ,"Max"
            if (_appConfiguration.UseSingleRecordMaxStatisticParameter)
                builder.Append("Max" + "\t" + max.ToString(CultureInfo.InvariantCulture) + Environment.NewLine);
            if (_appConfiguration.UseSingleRecord99QuantileStatisticParameter)
                builder.Append("P99" + "\t" + p99_quantile.ToString(CultureInfo.InvariantCulture) + Environment.NewLine);
            if (_appConfiguration.UseSingleRecordP95QuantileStatisticParameter)
                builder.Append("P95" + "\t" + p95_quantile.ToString(CultureInfo.InvariantCulture) + Environment.NewLine);
            if (_appConfiguration.UseSingleRecordMedianStatisticParameter)
                builder.Append("Median" + "\t" + median.ToString(CultureInfo.InvariantCulture) + Environment.NewLine);
            if (_appConfiguration.UseSingleRecordAverageStatisticParameter)
                builder.Append("Average" + "\t" + average.ToString(CultureInfo.InvariantCulture) + Environment.NewLine);
            if (_appConfiguration.UseSingleRecordGpuActiveAverageStatisticParameter)
                builder.Append("GPU-Busy Average" + "\t" + gpuActiveAverage.ToString(CultureInfo.InvariantCulture) + Environment.NewLine);
            if (_appConfiguration.UseSingleRecordP5QuantileStatisticParameter)
                builder.Append("P5" + "\t" + p5_quantile.ToString(CultureInfo.InvariantCulture) + Environment.NewLine);
            if (_appConfiguration.UseSingleRecordP1QuantileStatisticParameter)
                builder.Append("P1" + "\t" + p1_quantile.ToString(CultureInfo.InvariantCulture) + Environment.NewLine);
            if (_appConfiguration.UseSingleRecordGpuActiveP1QuantileStatisticParameter)
                builder.Append("GPU-Busy P1" + "\t" + gpuActiveP1_quantile.ToString(CultureInfo.InvariantCulture) + Environment.NewLine);
            if (_appConfiguration.UseSingleRecordP1LowAverageStatisticParameter && !double.IsNaN(p1_LowAverage))
                builder.Append("1% Low Average" + "\t" + p1_LowAverage.ToString(CultureInfo.InvariantCulture) + Environment.NewLine);
            if (_appConfiguration.UseSingleRecordGpuActiveP1LowAverageStatisticParameter && !double.IsNaN(GpuActiveP1_LowAverage))
                builder.Append("Gpu-Busy 1% Low Average" + "\t" + GpuActiveP1_LowAverage.ToString(CultureInfo.InvariantCulture) + Environment.NewLine);
            if (_appConfiguration.UseSingleRecordP1LowIntegralStatisticParameter && !double.IsNaN(p1_LowIntegral))
                builder.Append("1% Low Integral" + "\t" + p1_LowIntegral.ToString(CultureInfo.InvariantCulture) + Environment.NewLine);
            if (_appConfiguration.UseSingleRecordP0Dot2QuantileStatisticParameter)
                builder.Append("P0.2" + "\t" + p0dot2_quantile.ToString(CultureInfo.InvariantCulture) + Environment.NewLine);
            if (_appConfiguration.UseSingleRecordP0Dot1QuantileStatisticParameter)
                builder.Append("P0.1" + "\t" + p0dot1_quantile.ToString(CultureInfo.InvariantCulture) + Environment.NewLine);
            if (_appConfiguration.UseSingleRecordP0Dot1LowAverageStatisticParameter && !double.IsNaN(p0dot1_LowAverage))
                builder.Append("0.1% Low Average" + "\t" + p0dot1_LowAverage.ToString(CultureInfo.InvariantCulture) + Environment.NewLine);
            if (_appConfiguration.UseSingleRecordP0Dot1LowIntegralStatisticParameter && !double.IsNaN(p0dot1_LowIntegral))
                builder.Append("0.1% Low Integral" + "\t" + p0dot1_LowIntegral.ToString(CultureInfo.InvariantCulture) + Environment.NewLine);
            if (_appConfiguration.UseSingleRecordMinStatisticParameter)
                builder.Append("Min" + "\t" + min.ToString(CultureInfo.InvariantCulture) + Environment.NewLine);
            if (_appConfiguration.UseSingleRecordAdaptiveSTDStatisticParameter)
                builder.Append("Adaptive STDEV" + "\t" + adaptiveStandardDeviation.ToString(CultureInfo.InvariantCulture) + Environment.NewLine);
            if (_appConfiguration.UseSingleRecordCpuFpsPerWattParameter)
                builder.Append("CPU FPS/10W" + "\t" + cpuFpsPerWatt.ToString(CultureInfo.InvariantCulture) + Environment.NewLine);
            if (_appConfiguration.UseSingleRecordGpuFpsPerWattParameter)
                builder.Append("GPU FPS/10W" + "\t" + gpuFpsPerWatt.ToString(CultureInfo.InvariantCulture) + Environment.NewLine);

            Clipboard.SetDataObject(builder.ToString(), false);
        }

        private void OnCopyQuantiles()
        {
            if (RecordInfo == null)
                return;

            var lShapeQuantiles = _frametimeAnalyzer.GetLShapeQuantiles(SelectedLShapeMetric);
            var frametimes = GetFrametimesSubset();
            var fps = GetFPSSubset();
            double action(double q) => Math.Round(_frametimeStatisticProvider.GetPQuantileSequence(SelectedLShapeMetric == ELShapeMetrics.Frametimes ? frametimes : fps, q / 100), 2);

            StringBuilder builder = new StringBuilder();

            foreach (var quantile in lShapeQuantiles)
            {
                builder.Append(quantile.ToString(CultureInfo.InvariantCulture) + "%" + "\t" + action(quantile)
                    .ToString(CultureInfo.InvariantCulture) + Environment.NewLine);
            }

            Clipboard.SetDataObject(builder.ToString(), false);
        }

        private void OnCopySystemInfoCommand()
        {
            if (RecordInfo == null)
                return;

            var systemInfos = _recordManager.GetSystemInfos(RecordInfo);

            StringBuilder builder = new StringBuilder();

            foreach (var systemInfo in systemInfos)
            {
                builder.Append(systemInfo.Key + "\t" + systemInfo.Value + Environment.NewLine);
            }

            Clipboard.SetDataObject(builder.ToString(), false);
        }

        private void OnCopySensorInfo()
        {
            if (RecordInfo == null)
                return;

            var sensorInfos = SensorReport.GetReportFromSessionSensorData(_session.Runs.Select(run => run.SensorData2),
                _localRecordDataServer.CurrentTime, _localRecordDataServer.CurrentTime + _localRecordDataServer.WindowLength);

            StringBuilder builder = new StringBuilder();

            foreach (var sensorInfo in sensorInfos)
            {
                builder.Append(sensorInfo.Name + "\t" + sensorInfo.MinValue + "\t" +
                sensorInfo.AverageValue + "\t" + sensorInfo.MaxValue + Environment.NewLine);
            }

            Clipboard.SetDataObject(builder.ToString(), false);
        }

        private void OnCopyRawSensorInfo()
        {
            if (RecordInfo == null)
                return;


            var rawSensorInfos = _session.Runs.Select(run => run.SensorData2);

            var propertyInfos = typeof(ISessionSensorData).GetProperties()
                .Where(pi => pi.GetCustomAttributes(false).OfType<SensorDataExportAttribute>().Any())
                .Where(pi => rawSensorInfos.All(rsi =>
                {
                    var values = pi.GetValue(rsi) as Array;
                    return values.Length > 0;
                }));

            StringBuilder builder = new StringBuilder();

            // Header
            builder.AppendLine(string.Join("\t", propertyInfos.Select(pi =>
            {
                var attribute = pi.GetCustomAttributes(false).OfType<SensorDataExportAttribute>().FirstOrDefault();
                return attribute.Description;
            })) + "\t" + "Time in GPU limit(%)");

            //Content
            foreach (var run in rawSensorInfos)
            {
                for (int i = 0; i < run.MeasureTime.Values.Count; i++)
                {
                    var gpuLoadLimit = SensorReport.GetPercentageInGpuLoadLimit(run.GpuUsage.ToList());

                    var lineValues = propertyInfos.Select(pi =>
                    {
                        var array = pi.GetValue(run) as Array;
                        return array.Length >= run.MeasureTime.Values.Count ? string.Format("{0:0.##}", array.GetValue(i)) : string.Empty;
                    });
                    builder.AppendLine(string.Join("\t", lineValues) + "\t" + gpuLoadLimit);
                }
            }

            Clipboard.SetDataObject(builder.ToString(), false);
        }

        private void OnChartItemChanged()
        {
            UpdateSecondaryCharts();
        }

        private void OnRemoveOutliersChanged()
        {
            if (RecordInfo == null)
                return;

            _localRecordDataServer.RemoveOutlierMethod = RemoveOutliers ?
                ERemoveOutlierMethod.DeciPercentile : ERemoveOutlierMethod.None;

            CurrentGameName = RemoveOutliers ? $"{RecordInfo.GameName} (outlier-cleaned)"
                : RecordInfo.GameName;

            UpdateMainCharts();
            UpdateSecondaryCharts();

            if (!NeverShowDialog && RemoveOutliers)
            {
                MessageText = $"Remove outliers is only a function to simulate how the parameters would be like if there were no outliers. " +
                    Environment.NewLine +
                    $"This doesn't qualify as a conclusive evaluation of the benchmark run.";
                MessageDialogContentIsOpen = true;
            }
        }

        private void OnFilterModeChanged()
        {
            _localRecordDataServer.FilterMode = SelectedFilterMode;
            FpsGraphDataContext.BuildPlotmodel(new VisibleGraphs(ShowGpuLoad, ShowCpuLoad, ShowCpuMaxThreadLoad, ShowGpuPowerLimit, ShowPcLatency, ShowAggregationSeparators, ShowStutteringThresholds, StutteringFactor, StutteringLowFPSThreshold, ShowGpuActiveChart));
        }

        private async void OnCutRecord(bool inverse)
        {
            await _recordManager.DuplicateSession(_localRecordDataServer.CurrentSession, inverse, FirstSeconds, LastSeconds);
        }

        private void UpdateRangeSliderParameter()
        {
            if (_session == null)
                return;

            MaxRecordingTime = _session.Runs.SelectMany(r => r.CaptureData.TimeInSeconds).Last();

            _doUpdateCharts = false;
            FirstSeconds = 0;
            LastSeconds = MaxRecordingTime;
            _doUpdateCharts = true;
            RemainingRecordingTime = "(" + Math.Round(MaxRecordingTime, 2)
                .ToString("0.00", CultureInfo.InvariantCulture) + " s)";
        }

        private void SubscribeToAggregatorEvents()
        {
            _eventAggregator.GetEvent<PubSubEvent<ViewMessages.UpdateSession>>()
                            .Subscribe(msg =>
                            {
                                _session = msg.CurrentSession;
                                RecordInfo = msg.RecordInfo;
                                RaisePropertyChanged(nameof(AdditionalGraphsEnabled));

                                if (_useUpdateSession)
                                {
                                    UpdateAnalysisPage();
                                }
                            });

            _eventAggregator.GetEvent<PubSubEvent<ViewMessages.ThemeChanged>>()
                            .Subscribe(msg =>
                            {
                                if (_useUpdateSession)
                                {
                                    UpdateAnalysisPage();
                                }
                            });
        }

        private void UpdateAnalysisPage()
        {
            if (_session != null && RecordInfo != null)
            {
                CurrentGameName = RemoveOutliers ? $"{RecordInfo.GameName} (outlier-cleaned)"
                    : RecordInfo.GameName;
                SystemInfos = _recordManager.GetSystemInfos(RecordInfo);

                // Update PC latency
                IsPcLatencyAvailable = _session.Runs.All(run => !run.CaptureData.PcLatency.IsNullOrEmpty()) && _session.Runs.All(run => run.CaptureData.PcLatency.Average() > 0);
                if (!IsPcLatencyAvailable)
                {
                    _showPcLatency = false;
                    RaisePropertyChanged(nameof(ShowPcLatency));
                }

                if (IsPcLatencyAvailable)
                {
                    AvgPcLatency = $"PC Latency: {Math.Round(_session.Runs.Average(run => run.CaptureData.PcLatency.Average()))}ms";
                }

                // Check load metrics
                IsCpuLoadAvailable = _session.Runs.All(run => run.SensorData2 != null && !run.SensorData2.CpuUsage.IsNullOrEmpty());
                if (!IsCpuLoadAvailable)
                {
                    _showCpuLoad = false;
                    RaisePropertyChanged(nameof(ShowCpuLoad));
                }

                IsCpuMaxLoadAvailable = _session.Runs.All(run => run.SensorData2 != null && !run.SensorData2.CpuMaxThreadUsage.IsNullOrEmpty());
                if (!IsCpuMaxLoadAvailable)
                {
                    _showCpuMaxThreadLoad = false;
                    RaisePropertyChanged(nameof(ShowCpuMaxThreadLoad));
                }

                IsGpuLoadAvailable = _session.Runs.All(run => run.SensorData2 != null && !run.SensorData2.GpuUsage.IsNullOrEmpty());
                if (!IsGpuLoadAvailable)
                {
                    _showGpuLoad = false;
                    RaisePropertyChanged(nameof(ShowGpuLoad));
                }

                IsGpuPowerLimitAvailable = GetIsPowerLimitAvailable();
                if (!IsGpuPowerLimitAvailable)
                {
                    _showGpuPowerLimit = false;
                    RaisePropertyChanged(nameof(ShowGpuPowerLimit));
                }

                //Check GPU Active metric
                IsGpuActiveChartAvailable = GetIsGpuActiveChartAvailable();
                if (!IsGpuActiveChartAvailable)
                {
                    _showGpuActiveChart = false;
                    RaisePropertyChanged(nameof(ShowGpuActiveChart));
                }


                // Do update actions
                FrametimeGraphDataContext.RecordSession = _session;
                FpsGraphDataContext.RecordSession = _session;

                UpdateRangeSliderParameter();
                UpdateMainCharts();
                UpdateSecondaryCharts();
                UpdateSensorSessionReport();
            }
            else
            {
                ResetData();
            }
        }

        private void UpdateSensorSessionReport()
        {
            SensorReportItems.Clear();
            SensorReport.GetReportFromSessionSensorData(_session.Runs.Select(run => run.SensorData2).Cast<ISessionSensorData>(),
               _localRecordDataServer.CurrentTime, _localRecordDataServer.CurrentTime + _localRecordDataServer.WindowLength)
               .ForEach(SensorReportItems.Add);
        }

        private void UpdateMainCharts()
        {
            if (!_doUpdateCharts)
                return;

            var subset = GetFrametimesSubset();
            var gpuActiveSubset = GetGpuActiveTimesSubset();

            if (subset != null)
            {
                _onUpdateChart.OnNext(default);

                Task.Factory.StartNew(() => SetStaticChart(subset, gpuActiveSubset));
                Task.Factory.StartNew(() => SetStutteringChart(subset));
                Task.Factory.StartNew(() => SetVarianceChart());
                Task.Factory.StartNew(() => SetFpsThresholdChart(subset));
            }
        }

        private void RealTimeUpdateCharts()
        {
            if (!_doUpdateCharts)
                return;

            var subset = GetFrametimesSubset();

            if (subset != null)
            {
                _onUpdateChart.OnNext(default);
            }
        }

        private void DemandUpdateCharts()
        {
            if (!_doUpdateCharts)
                return;

            var subset = GetFrametimesSubset();
            var gpuActiveSubset = GetGpuActiveTimesSubset();
            if (subset != null)
            {
                Task.Factory.StartNew(() => SetStaticChart(subset, gpuActiveSubset));
                Task.Factory.StartNew(() => SetStutteringChart(subset));
                Task.Factory.StartNew(() => SetVarianceChart());
                Task.Factory.StartNew(() => SetFpsThresholdChart(subset));
                UpdateSensorSessionReport();
            }
        }

        private void UpdateSecondaryCharts()
        {
            if (SelectedChartItem == null)
                return;

            var headerName = SelectedChartItem.Header.ToString();
            var frametimeSubset = GetFrametimesSubset();
            var fpsSubset = GetFPSSubset();

            if (frametimeSubset == null || fpsSubset == null)
                return;

            if (headerName.Contains("L-shape"))
            {
                Task.Factory.StartNew(() => SetLShapeChart(frametimeSubset, fpsSubset));
            }
        }

        private IList<double> GetFrametimesSubset()
            => _localRecordDataServer?.GetFrametimeTimeWindow();

        private IList<double> GetFPSSubset()
            => _localRecordDataServer?.GetFpsTimeWindow();

        private IList<double> GetGpuActiveTimesSubset()
            => _localRecordDataServer?.GetGpuActiveTimeTimeWindow();

        private IList<double> GetGpuActiveFPSSubset()
            => _localRecordDataServer?.GetGpuActiveFpsTimeWindow();

        private void SetStaticChart(IList<double> frametimes, IList<double> gpuActiveTimes)
        {
            if (frametimes == null || !frametimes.Any())
                return;

            double GetFrametimeMetricValue(IList<double> sequence, EMetric metric) =>
                _frametimeStatisticProvider.GetFrametimeMetricValue(sequence, metric);

            double GetMetricValue(IList<double> sequence, EMetric metric) =>
                _frametimeStatisticProvider.GetFpsMetricValue(sequence, metric);

            var max = double.NaN;
            var p99_quantile = double.NaN;
            var p95_quantile = double.NaN;
            var median = double.NaN;
            var average = double.NaN;
            var gpuActiveAverage = double.NaN;
            var p0dot1_quantile = double.NaN;
            var p0dot2_quantile = double.NaN;
            var p1_quantile = double.NaN;
            var gpuActiveP1_quantile = double.NaN;
            var p5_quantile = double.NaN;
            var p1_LowAverage = double.NaN;
            var gpuActiveP1_LowAverage = double.NaN;
            var p0dot1_LowAverage = double.NaN;
            var p1_LowIntegral = double.NaN;
            var p0dot1_LowIntegral = double.NaN;
            var min = double.NaN;
            var adaptiveStandardDeviation = double.NaN;
            var cpuFpsPerWatt = double.NaN;
            var gpuFpsPerWatt = double.NaN;

            if (_useFrametimeStatisticParameters)
            {
                max = GetFrametimeMetricValue(frametimes, EMetric.Max);
                p99_quantile = GetFrametimeMetricValue(frametimes, EMetric.P99);
                p95_quantile = GetFrametimeMetricValue(frametimes, EMetric.P95);
                median = GetFrametimeMetricValue(frametimes, EMetric.Median);
                average = GetFrametimeMetricValue(frametimes, EMetric.Average);
                gpuActiveAverage = !gpuActiveTimes.IsNullOrEmpty() ? GetFrametimeMetricValue(gpuActiveTimes, EMetric.GpuActiveAverage) : double.NaN;
                p0dot1_quantile = GetFrametimeMetricValue(frametimes, EMetric.P0dot1);
                p0dot2_quantile = GetFrametimeMetricValue(frametimes, EMetric.P0dot2);
                p1_quantile = GetFrametimeMetricValue(frametimes, EMetric.P1);
                gpuActiveP1_quantile = !gpuActiveTimes.IsNullOrEmpty() ? GetFrametimeMetricValue(gpuActiveTimes, EMetric.GpuActiveP1) : double.NaN;
                p5_quantile = GetFrametimeMetricValue(frametimes, EMetric.P5);
                p1_LowAverage = GetFrametimeMetricValue(frametimes, EMetric.OnePercentLowAverage);
                gpuActiveP1_LowAverage = !gpuActiveTimes.IsNullOrEmpty() ? GetFrametimeMetricValue(gpuActiveTimes, EMetric.GpuActiveOnePercentLowAverage) : double.NaN;
                p0dot1_LowAverage = GetFrametimeMetricValue(frametimes, EMetric.ZerodotOnePercentLowAverage);
                p1_LowIntegral = GetFrametimeMetricValue(frametimes, EMetric.OnePercentLowIntegral);
                p0dot1_LowIntegral = GetFrametimeMetricValue(frametimes, EMetric.ZerodotOnePercentLowIntegral);
                min = GetFrametimeMetricValue(frametimes, EMetric.Min);
                adaptiveStandardDeviation = GetFrametimeMetricValue(frametimes, EMetric.AdaptiveStd);
            }
            else
            {
                max = GetMetricValue(frametimes, EMetric.Max);
                p99_quantile = GetMetricValue(frametimes, EMetric.P99);
                p95_quantile = GetMetricValue(frametimes, EMetric.P95);
                median = GetMetricValue(frametimes, EMetric.Median);
                average = GetMetricValue(frametimes, EMetric.Average);
                gpuActiveAverage = !gpuActiveTimes.IsNullOrEmpty() ? GetMetricValue(gpuActiveTimes, EMetric.GpuActiveAverage) : double.NaN;
                p0dot1_quantile = GetMetricValue(frametimes, EMetric.P0dot1);
                p0dot2_quantile = GetMetricValue(frametimes, EMetric.P0dot2);
                p1_quantile = GetMetricValue(frametimes, EMetric.P1);
                gpuActiveP1_quantile = !gpuActiveTimes.IsNullOrEmpty() ? GetMetricValue(gpuActiveTimes, EMetric.GpuActiveP1) : double.NaN;
                p5_quantile = GetMetricValue(frametimes, EMetric.P5);
                p1_LowAverage = GetMetricValue(frametimes, EMetric.OnePercentLowAverage);
                gpuActiveP1_LowAverage = !gpuActiveTimes.IsNullOrEmpty() ? GetMetricValue(gpuActiveTimes, EMetric.GpuActiveOnePercentLowAverage) : double.NaN;
                p0dot1_LowAverage = GetMetricValue(frametimes, EMetric.ZerodotOnePercentLowAverage);
                p1_LowIntegral = GetMetricValue(frametimes, EMetric.OnePercentLowIntegral);
                p0dot1_LowIntegral = GetMetricValue(frametimes, EMetric.ZerodotOnePercentLowIntegral);
                min = GetMetricValue(frametimes, EMetric.Min);
                adaptiveStandardDeviation = GetMetricValue(frametimes, EMetric.AdaptiveStd);
                cpuFpsPerWatt = _frametimeStatisticProvider
                    .GetPhysicalMetricValue(frametimes, EMetric.CpuFpsPerWatt,
                    SensorReport.GetAverageSensorValues(_session.Runs.Select(run => run.SensorData2), EReportSensorName.CpuPower,
                    _localRecordDataServer.CurrentTime, _localRecordDataServer.CurrentTime + _localRecordDataServer.WindowLength));
                gpuFpsPerWatt = _frametimeStatisticProvider
                .GetPhysicalMetricValue(frametimes, EMetric.GpuFpsPerWatt,
                SensorReport.GetAverageSensorValues(_session.Runs.Select(run => run.SensorData2), EReportSensorName.GpuPower,
                _localRecordDataServer.CurrentTime, _localRecordDataServer.CurrentTime + _localRecordDataServer.WindowLength, _appConfiguration.UseTBPSim));
            }




            Application.Current.Dispatcher.Invoke(new Action(() =>
            {
            IChartValues values = new ChartValues<double>();
            if (UseFrametimeStatisticParameters)
            {
                    if (_appConfiguration.UseSingleRecordGpuFpsPerWattParameter && !double.IsNaN(gpuFpsPerWatt))
                        values.Add(gpuFpsPerWatt);
                    if (_appConfiguration.UseSingleRecordCpuFpsPerWattParameter && !double.IsNaN(cpuFpsPerWatt))
                        values.Add(cpuFpsPerWatt);
                    if (_appConfiguration.UseSingleRecordAdaptiveSTDStatisticParameter && !double.IsNaN(adaptiveStandardDeviation))
                        values.Add(adaptiveStandardDeviation);
                    if (_appConfiguration.UseSingleRecordMaxStatisticParameter)
                        values.Add(max);
                    if (_appConfiguration.UseSingleRecordP0Dot1LowIntegralStatisticParameter && !double.IsNaN(p0dot1_LowIntegral))
                        values.Add(p0dot1_LowIntegral);
                    if (_appConfiguration.UseSingleRecordP0Dot1LowAverageStatisticParameter && !double.IsNaN(p0dot1_LowAverage))
                        values.Add(p0dot1_LowAverage);
                    if (_appConfiguration.UseSingleRecordP0Dot1QuantileStatisticParameter)
                        values.Add(p0dot1_quantile);
                    if (_appConfiguration.UseSingleRecordP0Dot2QuantileStatisticParameter)
                        values.Add(p0dot2_quantile);
                    if (_appConfiguration.UseSingleRecordP1LowIntegralStatisticParameter && !double.IsNaN(p1_LowIntegral))
                        values.Add(p1_LowIntegral);
                    if (_appConfiguration.UseSingleRecordGpuActiveP1LowAverageStatisticParameter && !double.IsNaN(gpuActiveP1_LowAverage))
                        values.Add(gpuActiveP1_LowAverage);
                    if (_appConfiguration.UseSingleRecordP1LowAverageStatisticParameter && !double.IsNaN(p1_LowAverage))
                        values.Add(p1_LowAverage);
                    if (_appConfiguration.UseSingleRecordGpuActiveP1QuantileStatisticParameter && !double.IsNaN(gpuActiveP1_quantile))
                        values.Add(gpuActiveP1_quantile);
                    if (_appConfiguration.UseSingleRecordP1QuantileStatisticParameter)
                        values.Add(p1_quantile);
                    if (_appConfiguration.UseSingleRecordP5QuantileStatisticParameter)
                        values.Add(p5_quantile);
                    if (_appConfiguration.UseSingleRecordMedianStatisticParameter)
                        values.Add(median);
                    if (_appConfiguration.UseSingleRecordGpuActiveAverageStatisticParameter && !double.IsNaN(gpuActiveAverage))
                        values.Add(gpuActiveAverage);
                    if (_appConfiguration.UseSingleRecordAverageStatisticParameter)
                        values.Add(average);
                    if (_appConfiguration.UseSingleRecordP95QuantileStatisticParameter)
                        values.Add(p95_quantile);
                    if (_appConfiguration.UseSingleRecord99QuantileStatisticParameter)
                        values.Add(p99_quantile);
                    if (_appConfiguration.UseSingleRecordMinStatisticParameter)
                        values.Add(min);
                }

                else
                {
                    if (_appConfiguration.UseSingleRecordGpuFpsPerWattParameter && !double.IsNaN(gpuFpsPerWatt))
                        values.Add(gpuFpsPerWatt);
                    if (_appConfiguration.UseSingleRecordCpuFpsPerWattParameter && !double.IsNaN(cpuFpsPerWatt))
                        values.Add(cpuFpsPerWatt);
                    if (_appConfiguration.UseSingleRecordAdaptiveSTDStatisticParameter && !double.IsNaN(adaptiveStandardDeviation))
                        values.Add(adaptiveStandardDeviation);
                    if (_appConfiguration.UseSingleRecordMinStatisticParameter)
                        values.Add(min);
                    if (_appConfiguration.UseSingleRecordP0Dot1LowIntegralStatisticParameter && !double.IsNaN(p0dot1_LowIntegral))
                        values.Add(p0dot1_LowIntegral);
                    if (_appConfiguration.UseSingleRecordP0Dot1LowAverageStatisticParameter && !double.IsNaN(p0dot1_LowAverage))
                        values.Add(p0dot1_LowAverage);
                    if (_appConfiguration.UseSingleRecordP0Dot1QuantileStatisticParameter)
                        values.Add(p0dot1_quantile);
                    if (_appConfiguration.UseSingleRecordP0Dot2QuantileStatisticParameter)
                        values.Add(p0dot2_quantile);
                    if (_appConfiguration.UseSingleRecordP1LowIntegralStatisticParameter && !double.IsNaN(p1_LowIntegral))
                        values.Add(p1_LowIntegral);
                    if (_appConfiguration.UseSingleRecordGpuActiveP1LowAverageStatisticParameter && !double.IsNaN(gpuActiveP1_LowAverage))
                        values.Add(gpuActiveP1_LowAverage);
                    if (_appConfiguration.UseSingleRecordP1LowAverageStatisticParameter && !double.IsNaN(p1_LowAverage))
                        values.Add(p1_LowAverage);
                    if (_appConfiguration.UseSingleRecordGpuActiveP1QuantileStatisticParameter && !double.IsNaN(gpuActiveP1_quantile))
                        values.Add(gpuActiveP1_quantile);
                    if (_appConfiguration.UseSingleRecordP1QuantileStatisticParameter)
                        values.Add(p1_quantile);
                    if (_appConfiguration.UseSingleRecordP5QuantileStatisticParameter)
                        values.Add(p5_quantile);
                    if (_appConfiguration.UseSingleRecordGpuActiveAverageStatisticParameter && !double.IsNaN(gpuActiveAverage))
                        values.Add(gpuActiveAverage);
                    if (_appConfiguration.UseSingleRecordAverageStatisticParameter)
                        values.Add(average);
                    if (_appConfiguration.UseSingleRecordMedianStatisticParameter)
                        values.Add(median);
                    if (_appConfiguration.UseSingleRecordP95QuantileStatisticParameter)
                        values.Add(p95_quantile);
                    if (_appConfiguration.UseSingleRecord99QuantileStatisticParameter)
                        values.Add(p99_quantile);
                    if (_appConfiguration.UseSingleRecordMaxStatisticParameter)
                        values.Add(max);
                }


                StatisticCollection = new SeriesCollection
                {
                    new RowSeries
                    {
                        Title = RecordInfo.GameName,
                        Fill = new SolidColorBrush(Color.FromRgb(241, 125, 32)),
                        Values = values,
                        DataLabels = true,
                        FontSize = 12,
                        MaxRowHeigth = 30,
                        Foreground = new SolidColorBrush(_appConfiguration.UseDarkMode ? Colors.White : Colors.Black)
                    }
                };

                double maxOffset = (values as IList<double>).Max() * 0.15;
                BarMaxValue = (int)((values as IList<double>).Max() + maxOffset);

                var parameterLabelList = new List<string>();

                //{ "Adaptive STDEV", "Min", "0.1% Low Integral", "0.1% Low Average", "0.1%", "0.2%", "1% Low Integral", "1% Low Average", "1%", "5%", "Average", "95%", "99%", "Max" }
                if (UseFrametimeStatisticParameters)
                {
                    if (_appConfiguration.UseSingleRecordGpuFpsPerWattParameter && !double.IsNaN(gpuFpsPerWatt))
                        parameterLabelList.Add("GPU FPS/10W");
                    if (_appConfiguration.UseSingleRecordCpuFpsPerWattParameter && !double.IsNaN(cpuFpsPerWatt))
                        parameterLabelList.Add("CPU FPS/10W");
                    if (_appConfiguration.UseSingleRecordAdaptiveSTDStatisticParameter && !double.IsNaN(adaptiveStandardDeviation))
                        parameterLabelList.Add("Adaptive STDEV");
                    if (_appConfiguration.UseSingleRecordMaxStatisticParameter)
                        parameterLabelList.Add("Max");
                    if (_appConfiguration.UseSingleRecordP0Dot1LowIntegralStatisticParameter && !double.IsNaN(p0dot1_LowIntegral))
                        parameterLabelList.Add("0.1% High Integral");
                    if (_appConfiguration.UseSingleRecordP0Dot1LowAverageStatisticParameter && !double.IsNaN(p0dot1_LowAverage))
                        parameterLabelList.Add("0.1% High Average");
                    if (_appConfiguration.UseSingleRecordP0Dot1QuantileStatisticParameter)
                        parameterLabelList.Add("P99.9");
                    if (_appConfiguration.UseSingleRecordP0Dot2QuantileStatisticParameter)
                        parameterLabelList.Add("P99.8");
                    if (_appConfiguration.UseSingleRecordP1LowIntegralStatisticParameter && !double.IsNaN(p1_LowIntegral))
                        parameterLabelList.Add("1% High Integral");
                    if (_appConfiguration.UseSingleRecordGpuActiveP1LowAverageStatisticParameter && !double.IsNaN(gpuActiveP1_LowAverage))
                        parameterLabelList.Add("Gpu-Busy 1% High Avg.");
                    if (_appConfiguration.UseSingleRecordP1LowAverageStatisticParameter && !double.IsNaN(p1_LowAverage))
                        parameterLabelList.Add("1% High Average");
                    if (_appConfiguration.UseSingleRecordP1QuantileStatisticParameter)
                        parameterLabelList.Add("P99");
                    if (_appConfiguration.UseSingleRecordGpuActiveP1QuantileStatisticParameter && !double.IsNaN(gpuActiveP1_quantile))
                        parameterLabelList.Add("GPU-Busy P99");
                    if (_appConfiguration.UseSingleRecordP5QuantileStatisticParameter)
                        parameterLabelList.Add("P95");
                    if (_appConfiguration.UseSingleRecordMedianStatisticParameter)
                        parameterLabelList.Add("Median");
                    if (_appConfiguration.UseSingleRecordGpuActiveAverageStatisticParameter && !double.IsNaN(gpuActiveAverage))
                        parameterLabelList.Add("GPU-Busy Average");
                    if (_appConfiguration.UseSingleRecordAverageStatisticParameter)
                        parameterLabelList.Add("Average");
                    if (_appConfiguration.UseSingleRecordP95QuantileStatisticParameter)
                        parameterLabelList.Add("P5");
                    if (_appConfiguration.UseSingleRecord99QuantileStatisticParameter)
                        parameterLabelList.Add("P1");
                    if (_appConfiguration.UseSingleRecordMinStatisticParameter)
                        parameterLabelList.Add("Min");
                }
                else
                {
                    if (_appConfiguration.UseSingleRecordGpuFpsPerWattParameter)
                        parameterLabelList.Add("GPU FPS/10W");
                    if (_appConfiguration.UseSingleRecordCpuFpsPerWattParameter)
                        parameterLabelList.Add("CPU FPS/10W");
                    if (_appConfiguration.UseSingleRecordAdaptiveSTDStatisticParameter && !double.IsNaN(adaptiveStandardDeviation))
                        parameterLabelList.Add("Adaptive STDEV");
                    if (_appConfiguration.UseSingleRecordMinStatisticParameter)
                        parameterLabelList.Add("Min");
                    if (_appConfiguration.UseSingleRecordP0Dot1LowIntegralStatisticParameter && !double.IsNaN(p0dot1_LowIntegral))
                        parameterLabelList.Add("0.1% Low Integral");
                    if (_appConfiguration.UseSingleRecordP0Dot1LowAverageStatisticParameter && !double.IsNaN(p0dot1_LowAverage))
                        parameterLabelList.Add("0.1% Low Average");
                    if (_appConfiguration.UseSingleRecordP0Dot1QuantileStatisticParameter)
                        parameterLabelList.Add("P0.1");
                    if (_appConfiguration.UseSingleRecordP0Dot2QuantileStatisticParameter)
                        parameterLabelList.Add("P0.2");
                    if (_appConfiguration.UseSingleRecordP1LowIntegralStatisticParameter && !double.IsNaN(p1_LowIntegral))
                        parameterLabelList.Add("1% Low Integral");
                    if (_appConfiguration.UseSingleRecordGpuActiveP1LowAverageStatisticParameter && !double.IsNaN(gpuActiveP1_LowAverage))
                        parameterLabelList.Add("Gpu-Busy 1% Low Avg.");
                    if (_appConfiguration.UseSingleRecordP1LowAverageStatisticParameter && !double.IsNaN(p1_LowAverage))
                        parameterLabelList.Add("1% Low Average");
                    if (_appConfiguration.UseSingleRecordGpuActiveP1QuantileStatisticParameter && !double.IsNaN(gpuActiveP1_quantile))
                        parameterLabelList.Add("GPU-Busy P1");
                    if (_appConfiguration.UseSingleRecordP1QuantileStatisticParameter)
                        parameterLabelList.Add("P1");
                    if (_appConfiguration.UseSingleRecordP5QuantileStatisticParameter)
                        parameterLabelList.Add("P5");
                    if (_appConfiguration.UseSingleRecordGpuActiveAverageStatisticParameter && !double.IsNaN(gpuActiveAverage))
                        parameterLabelList.Add("GPU-Busy Average");
                    if (_appConfiguration.UseSingleRecordAverageStatisticParameter)
                        parameterLabelList.Add("Average");
                    if (_appConfiguration.UseSingleRecordMedianStatisticParameter)
                        parameterLabelList.Add("Median");
                    if (_appConfiguration.UseSingleRecordP95QuantileStatisticParameter)
                        parameterLabelList.Add("P95");
                    if (_appConfiguration.UseSingleRecord99QuantileStatisticParameter)
                        parameterLabelList.Add("P99");
                    if (_appConfiguration.UseSingleRecordMaxStatisticParameter)
                        parameterLabelList.Add("Max");
                }

                ParameterLabels = parameterLabelList.ToArray();
            }));
        }

        private void SetStutteringChart(IList<double> frametimes)
        {
            if (frametimes == null || !frametimes.Any())
                return;

            var stutteringTimePercentage = _frametimeStatisticProvider.GetStutteringTimePercentage(frametimes, _appConfiguration.StutteringFactor);

            var lowFPSTimePercentage = _frametimeStatisticProvider.GetLowFPSTimePercentage(frametimes, _appConfiguration.StutteringFactor, _appConfiguration.StutteringThreshold);

            double stutteringTotalTime = Math.Round(stutteringTimePercentage / 100 * frametimes.Skip(1).Sum() / 1000, 2);
            double lowFPSTotalTime = Math.Round(lowFPSTimePercentage / 100 * frametimes.Skip(1).Sum() / 1000, 2);
            double smoothTotalTime = Math.Round((1 - (stutteringTimePercentage + lowFPSTimePercentage) / 100) * frametimes.Skip(1).Sum() / 1000, 2);

            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                StutteringStatisticCollection = new SeriesCollection
                {
                    new PieSeries
                    {
                        Title = $"Smooth:  { smoothTotalTime.ToString(CultureInfo.InvariantCulture) }s ({ Math.Round(100 - (stutteringTimePercentage + lowFPSTimePercentage), 1).ToString(CultureInfo.InvariantCulture) }%)",
                        Values = new ChartValues<double>(){ smoothTotalTime },
                        DataLabels = false,
                        Fill = ColorRessource.PieChartSmoothFill,
                        StrokeThickness = 0
                    },

                    new PieSeries
                    {
                        Title = $"Low FPS:  { lowFPSTotalTime.ToString(CultureInfo.InvariantCulture) }s ({ Math.Round(lowFPSTimePercentage, 1).ToString(CultureInfo.InvariantCulture) }%)",
                        Values = new ChartValues<double>(){ lowFPSTotalTime },
                        DataLabels = false,
                        Fill = ColorRessource.PieChartLowFPSFill,
                        StrokeThickness = 0
                    },

                    new PieSeries
                    {
                        Title = $"Stuttering:  { stutteringTotalTime.ToString(CultureInfo.InvariantCulture) }s ({ Math.Round(stutteringTimePercentage, 1).ToString(CultureInfo.InvariantCulture) }%)",
                        Values = new ChartValues<double>(){ stutteringTotalTime },
                        DataLabels = false,
                        Fill = ColorRessource.PieChartStutterFill,
                        StrokeThickness = 0
                    }
                };

            }));
        }

        private void SetVarianceChart()
        {
            if (_session == null)
                return;

            var variances = _frametimeStatisticProvider.GetFrametimeVariancePercentages(_session);


            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                VarianceStatisticCollection = new SeriesCollection
                {
                    new PieSeries
                    {
                        Title = $"< 2ms ({Math.Round(variances[0] *100 , 2).ToString(CultureInfo.InvariantCulture) }%)",
                        Values = new ChartValues<double>() { variances[0] },
                        DataLabels = false,
                        Fill = new SolidColorBrush(Color.FromRgb(34, 151, 243)), // blue
                        StrokeThickness = 0
                    },

                    new PieSeries
                    {
                        Title = $"< 4ms ({ Math.Round(variances[1] *100, 2).ToString(CultureInfo.InvariantCulture) }%)",
                        Values = new ChartValues<double>() { variances[1] },
                        DataLabels = false,
                        Fill = new SolidColorBrush(Color.FromRgb(15, 120, 180)), // dark blue
                        StrokeThickness = 0
                    },

                    new PieSeries
                    {
                        Title = $"< 8ms ({ Math.Round(variances[2] *100, 2).ToString(CultureInfo.InvariantCulture) }%)",
                        Values = new ChartValues<double>() { variances[2] },
                        DataLabels = false,
                        Fill = new SolidColorBrush(Color.FromRgb(255, 180, 0)), // yellow
                        StrokeThickness = 0
                    },

                    new PieSeries
                    {
                        Title = $"< 12ms ({ Math.Round(variances[3] *100, 2).ToString(CultureInfo.InvariantCulture) }%)",
                        Values = new ChartValues<double>() { variances[3] },
                        DataLabels = false,
                        Fill = new SolidColorBrush(Color.FromRgb(241, 125, 32)), // orange
                        StrokeThickness = 0
                    },

                    new PieSeries
                    {
                        Title = $"> 12ms ({ Math.Round(variances[4] *100, 2).ToString(CultureInfo.InvariantCulture) }%)",
                        Values = new ChartValues<double>() { variances[4] },
                        DataLabels = false,
                        Fill = new SolidColorBrush(Color.FromRgb(200, 0, 0)), // red
                        StrokeThickness = 0
                    },
                };
            }));
        }

        private void SetLShapeChart(IList<double> frametimes, IList<double> fps)
        {
            if (frametimes == null || !frametimes.Any())
                return;

            var lShapeQuantiles = _frametimeAnalyzer.GetLShapeQuantiles(SelectedLShapeMetric);
            double action(double q) => _frametimeStatisticProvider.GetPQuantileSequence(SelectedLShapeMetric == ELShapeMetrics.Frametimes ? frametimes : fps, q / 100);
            var observablePoints = lShapeQuantiles.Select(q => new ObservablePoint(q, action(q)));

            var chartValues = new ChartValues<ObservablePoint>();
            chartValues.AddRange(observablePoints);
            string unit = SelectedLShapeMetric == ELShapeMetrics.Frametimes ? " ms" : " fps";

            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                LShapeCollection = new SeriesCollection()
                {
                    new LineSeries
                    {
                        Values = chartValues,
                        Stroke = ColorRessource.LShapeStroke,
                        Fill = Brushes.Transparent,
                        FontSize = 11,
                        StrokeThickness = 2,
                        LineSmoothness= 1,
                        PointGeometrySize = 5,
                        PointGeometry = DefaultGeometries.Square,
                        DataLabels = true,
                        LabelPoint = point => point.X.ToString(CultureInfo.InvariantCulture) + "%, " +
                            Math.Round(point.Y, 1).ToString(CultureInfo.InvariantCulture) + unit
                    }
                };

                ResetLShapeChart.OnNext(default);
            }));
        }

        private void ResetData()
        {
            FrametimeGraphDataContext.Reset();
            FpsGraphDataContext.Reset();
            _localRecordDataServer.CurrentSession = null;
            LShapeCollection?.Clear();
            StatisticCollection?.Clear();
            StutteringStatisticCollection?.Clear();
            VarianceStatisticCollection?.Clear();
            SystemInfos?.Clear();
        }

        private void SetFrametimeChartYAxisSetting(Tuple<double, double> setting)
        {
            FrametimeGraphDataContext.UpdateAxis(EPlotAxis.YAXISFRAMETIMES, axis =>
            {
                axis.Reset();
                axis.Minimum = setting.Item1;
                axis.Maximum = setting.Item2;
            });
        }

        private Tuple<double, double> GetYAxisSettingFromSelection(EChartYAxisSetting selection)
        {
            Tuple<double, double> setting = new Tuple<double, double>(double.NaN, double.NaN);

            if (_localRecordDataServer == null || _localRecordDataServer.CurrentSession == null)
                return setting;

            switch (selection)
            {
                case EChartYAxisSetting.FullFit:
                    {
                        var frametimes = _localRecordDataServer?
                            .GetFrametimePointTimeWindow().Select(pnt => pnt.Y);

                        var yMin = frametimes.Min();
                        var yMax = frametimes.Max();


                        if (GetIsGpuActiveChartAvailable() && ShowGpuActiveChart)
                        {
                            var gpuActiveTimes = _localRecordDataServer?
                            .GetGpuActiveTimePointTimeWindow().Select(pnt => pnt.Y);

                            yMin = Math.Min(frametimes.Min(), gpuActiveTimes.Min());
                            yMax = Math.Max(frametimes.Max(), gpuActiveTimes.Max());
                        }



                        if (ShowStutteringThresholds)
                        {
                            var frametimeStatisticProvider = new FrametimeStatisticProvider(null);
                            var movingAverage = frametimeStatisticProvider.GetMovingAverage(frametimes.ToList());

                            yMax = Math.Max(Math.Max(movingAverage.Max() * _appConfiguration.StutteringFactor, yMax), 1000 / _appConfiguration.StutteringThreshold);
                        }

                        if (IsPcLatencyAvailable && ShowPcLatency)
                        {
                            var maxLatency = _localRecordDataServer.CurrentSession.Runs.Max(run => run.CaptureData.PcLatency.Max());
                            yMax = Math.Max(yMax, maxLatency);
                        }

                        setting = new Tuple<double, double>(yMin - (yMax - yMin) / 6, yMax + (yMax - yMin) / 6);
                    }
                    break;
                case EChartYAxisSetting.IQR:
                    {
                        double iqr = MathNet.Numerics.Statistics.Statistics
                            .InterquartileRange(_localRecordDataServer?
                            .GetFrametimePointTimeWindow().Select(pnt => pnt.Y));
                        double median = MathNet.Numerics.Statistics.Statistics
                            .Median(_localRecordDataServer?
                            .GetFrametimePointTimeWindow().Select(pnt => pnt.Y));

                        double gpuActiveIqr = double.MinValue;
                        double gpuActiveMedian = double.MinValue;

                        if (GetIsGpuActiveChartAvailable() && ShowGpuActiveChart)
                        {
                            gpuActiveIqr = MathNet.Numerics.Statistics.Statistics
                                .InterquartileRange(_localRecordDataServer?
                                .GetGpuActiveTimePointTimeWindow().Select(pnt => pnt.Y));
                            gpuActiveMedian = MathNet.Numerics.Statistics.Statistics
                                .Median(_localRecordDataServer?
                                .GetGpuActiveTimePointTimeWindow().Select(pnt => pnt.Y));

                        }

                        double maxMedian = Math.Max(median, gpuActiveMedian);
                        double maxIqr = Math.Max(iqr, gpuActiveIqr);

                        setting = new Tuple<double, double>(maxMedian - 4 * maxIqr, maxMedian + 6 * maxIqr);
                    }
                    break;
                case EChartYAxisSetting.Zero_Ten:
                    setting = new Tuple<double, double>(0, 10);
                    break;
                case EChartYAxisSetting.Zero_Twenty:
                    setting = new Tuple<double, double>(0, 20);
                    break;
                case EChartYAxisSetting.Zero_Thirty:
                    setting = new Tuple<double, double>(0, 30);
                    break;
                case EChartYAxisSetting.Zero_Forty:
                    setting = new Tuple<double, double>(0, 40);
                    break;
                case EChartYAxisSetting.Zero_Sixty:
                    setting = new Tuple<double, double>(0, 60);
                    break;
                case EChartYAxisSetting.Zero_Eighty:
                    setting = new Tuple<double, double>(0, 80);
                    break;
                case EChartYAxisSetting.Zero_Hundred:
                    setting = new Tuple<double, double>(0, 100);
                    break;
            }

            return setting;
        }


        public void OnStutteringOptionsChanged()
        {
            DemandUpdateCharts();
        }

        public void OnRangeSliderDragCompleted()
        {
            if (FirstSeconds > LastSeconds || FirstSeconds < 0)
                FirstSeconds = 0;

            if (LastSeconds > MaxRecordingTime || LastSeconds <= 0)
                LastSeconds = MaxRecordingTime;


            DemandUpdateCharts();
        }

        public void OnNavigatedTo(NavigationContext navigationContext)
        {
            _useUpdateSession = true;
            if (_session == null || _session?.Hash != _previousSession?.Hash)
            {
                try
                {
                    UpdateAnalysisPage();
                }
                catch
                { return; }
            }
        }

        public bool IsNavigationTarget(NavigationContext navigationContext)
        {
            return true;
        }

        public void OnNavigatedFrom(NavigationContext navigationContext)
        {
            _previousSession = _session;
            _useUpdateSession = false;
        }
    }
}
