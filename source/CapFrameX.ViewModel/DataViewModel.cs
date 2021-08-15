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
using CapFrameX.Statistics.NetStandard;
using CapFrameX.Statistics.NetStandard.Contracts;
using CapFrameX.Statistics.PlotBuilder.Contracts;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using CapFrameX.Extensions;

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
        private ContitionalMessageDialog _messageDialogContent;
        private bool _messageDialogContentIsOpen;
        private string _messageText;
        private int _barMaxValue = 100;
        private bool _gpuLoad;
        private bool _cpuLoad;
        private bool _cpuMaxThreadLoad;
        private bool _gpuPowerLimit;
        private bool _aggregationSeparators;
        private EFilterMode _selectedFilterMode;
        private ELShapeMetrics _lShapeMetric = ELShapeMetrics.Frametimes;
        private string _lShapeYaxisLabel = "Frametimes (ms)";

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

        public bool IsGpuPowerLimitAvailable => GetIsPowerLimitAvailable();

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
            }
        }

        public double StutteringLowFPSThreshold
        {
            get { return _appConfiguration.StutteringThreshold; }
            set
            {
                _appConfiguration.StutteringThreshold = value;
                RaisePropertyChanged();
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
                LShapeYaxisLabel = value == ELShapeMetrics.Frametimes ? "Frametimes (ms)" : "FPS";
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

        public ContitionalMessageDialog MessageDialogContent
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

        public int BarChartSeparators
        {
            get
            {
                int steps;
                double maxValueFracture = _barMaxValue / 3;

                if (maxValueFracture <= 25)
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
                else
                    steps = 200;

                return steps;
            }
        }

        public bool AdditionalGraphsEnabled
        {
            get => _session == null ? false
                : _session.Runs.Any(r => r.SensorData != null || r.SensorData2 != null);
        }

        public ICommand CopyStatisticalParameterCommand { get; }

        public ICommand CopyLShapeQuantilesCommand { get; }

        public ICommand CopySystemInfoCommand { get; }

        public ICommand CopySensorInfoCommand { get; }

        public ICommand CopyRawSensorInfoCommand { get; }

        public ICommand CutRecordCommand { get; }

        public ICommand CutRecordInverseCommand { get; }

        public bool GpuLoad
        {
            get => _gpuLoad;
            set
            {
                _gpuLoad = value;
                RaisePropertyChanged();
                _onUpdateChart.OnNext(default);
            }
        }
        public bool CpuLoad
        {
            get => _cpuLoad;
            set
            {
                _cpuLoad = value;
                RaisePropertyChanged();
                _onUpdateChart.OnNext(default);
            }
        }
        public bool CpuMaxThreadLoad
        {
            get => _cpuMaxThreadLoad;
            set
            {
                _cpuMaxThreadLoad = value;
                RaisePropertyChanged();
                _onUpdateChart.OnNext(default);
            }
        }
        public bool GpuPowerLimit
        {
            get => _gpuPowerLimit && IsGpuPowerLimitAvailable;
            set
            {
                _gpuPowerLimit = value;
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



        public bool IsAnyGraphActive()
        {
            return GpuLoad || CpuLoad || CpuMaxThreadLoad;
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

        public DataViewModel(IStatisticProvider frametimeStatisticProvider,
                             IFrametimeAnalyzer frametimeAnalyzer,
                             IEventAggregator eventAggregator,
                             IAppConfiguration appConfiguration,
                             RecordManager recordManager,
                             ILogger<DataViewModel> logger)
        {
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();

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

            MessageDialogContent = new ContitionalMessageDialog();

            SubscribeToAggregatorEvents();
            InitializeStatisticParameter();
            SetThresholdLabels();
            Setup();

            stopwatch.Stop();
            _logger.LogInformation(this.GetType().Name + " {initializationTime}s initialization time", Math.Round(stopwatch.ElapsedMilliseconds * 1E-03, 1));
        }

        private bool GetIsPowerLimitAvailable()
        {
            if (_localRecordDataServer == null)
                return false;

            if (_localRecordDataServer.CurrentSession == null)
                return false;

            if (_localRecordDataServer.CurrentSession.Runs == null
                || !_localRecordDataServer.CurrentSession.Runs.Any())
                return false;

            if (_localRecordDataServer.CurrentSession.Runs
                .Any(run => run.SensorData2 == null))
                return false;

            if (_localRecordDataServer.CurrentSession.Runs
                .Any(run => run.SensorData2.GPUPowerLimit == null 
                || !run.SensorData2.GPUPowerLimit.Any()))
                return false;

            return true;
        }

        private void Setup()
        {
            _onUpdateChart.Subscribe(_ =>
            {
                FpsGraphDataContext.BuildPlotmodel(new VisibleGraphs(GpuLoad, CpuLoad, CpuMaxThreadLoad, GpuPowerLimit, ShowAggregationSeparators));

                FrametimeGraphDataContext.BuildPlotmodel(new VisibleGraphs(GpuLoad, CpuLoad, CpuMaxThreadLoad, GpuPowerLimit, ShowAggregationSeparators), plotModel =>
                {
                    FrametimeGraphDataContext.UpdateAxis(EPlotAxis.YAXISFRAMETIMES, axis =>
                    {
                        var tuple = GetYAxisSettingFromSelection(SelecetedChartYAxisSetting);
                        SetFrametimeChartYAxisSetting(tuple);
                    });
                });

                // Warum diese Updates der Properties hier?
                RaisePropertyChanged(nameof(GpuPowerLimit));
                RaisePropertyChanged(nameof(IsGpuPowerLimitAvailable));
            });
        }

        partial void InitializeStatisticParameter();

        private void OnAcceptParameterSettings()
        {
            Task.Factory.StartNew(() => SetStaticChart(GetFrametimesSubset()));
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
            RemainingRecordingTime = Math.Round(LastSeconds - FirstSeconds, 2)
                .ToString(CultureInfo.InvariantCulture) + " s";
        }

        private void OnCopyStatisticalParameter()
        {
            if (_session == null)
                return;

            var frametimes = GetFrametimesSubset();
            double GeMetricValue(IList<double> sequence, EMetric metric) =>
                Math.Round(_frametimeStatisticProvider.GetFpsMetricValue(sequence, metric), 2);

            var max = GeMetricValue(frametimes, EMetric.Max);
            var p99_quantile = GeMetricValue(frametimes, EMetric.P99);
            var p95_quantile = GeMetricValue(frametimes, EMetric.P95);
            var median = GeMetricValue(frametimes, EMetric.Median);
            var average = GeMetricValue(frametimes, EMetric.Average);
            var p0dot1_quantile = GeMetricValue(frametimes, EMetric.P0dot1);
            var p0dot2_quantile = GeMetricValue(frametimes, EMetric.P0dot2);
            var p1_quantile = GeMetricValue(frametimes, EMetric.P1);
            var p5_quantile = GeMetricValue(frametimes, EMetric.P5);
            var p1_averageLow = GeMetricValue(frametimes, EMetric.OnePercentLow);
            var p0dot1_averageLow = GeMetricValue(frametimes, EMetric.ZerodotOnePercentLow);
            var min = GeMetricValue(frametimes, EMetric.Min);
            var adaptiveStandardDeviation = GeMetricValue(frametimes, EMetric.AdaptiveStd);
            var cpuFpsPerWatt = _frametimeStatisticProvider
                 .GetPhysicalMetricValue(frametimes, EMetric.CpuFpsPerWatt,
                 SensorReport.GetAverageSensorValues(_session.Runs.Select(run => run.SensorData2), EReportSensorName.CpuPower,
                 _localRecordDataServer.CurrentTime, _localRecordDataServer.CurrentTime + _localRecordDataServer.WindowLength));
            var gpuFpsPerWatt = _frametimeStatisticProvider
            .GetPhysicalMetricValue(frametimes, EMetric.GpuFpsPerWatt,
            SensorReport.GetAverageSensorValues(_session.Runs.Select(run => run.SensorData2), EReportSensorName.GpuPower,
            _localRecordDataServer.CurrentTime, _localRecordDataServer.CurrentTime + _localRecordDataServer.WindowLength));

            StringBuilder builder = new StringBuilder();

            // Vice versa!
            // "Adaptive STDEV" ,"Min","0.1% Low" ,"0.1%","0.2%" ,"1% Low", "1%" ,"5%" ,"Average" ,"95%" ,"99%" ,"Max"
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
            if (_appConfiguration.UseSingleRecordP5QuantileStatisticParameter)
                builder.Append("P5" + "\t" + p5_quantile.ToString(CultureInfo.InvariantCulture) + Environment.NewLine);
            if (_appConfiguration.UseSingleRecordP1QuantileStatisticParameter)
                builder.Append("P1" + "\t" + p1_quantile.ToString(CultureInfo.InvariantCulture) + Environment.NewLine);
            if (_appConfiguration.UseSingleRecordP1LowAverageStatisticParameter && !double.IsNaN(p1_averageLow))
                builder.Append("1% Low" + "\t" + p1_averageLow.ToString(CultureInfo.InvariantCulture) + Environment.NewLine);
            if (_appConfiguration.UseSingleRecordP0Dot2QuantileStatisticParameter)
                builder.Append("P0.2" + "\t" + p0dot2_quantile.ToString(CultureInfo.InvariantCulture) + Environment.NewLine);
            if (_appConfiguration.UseSingleRecordP0Dot1QuantileStatisticParameter)
                builder.Append("P0.1" + "\t" + p0dot1_quantile.ToString(CultureInfo.InvariantCulture) + Environment.NewLine);
            if (_appConfiguration.UseSingleRecordP0Dot1LowAverageStatisticParameter && !double.IsNaN(p0dot1_averageLow))
                builder.Append("0.1% Low" + "\t" + p0dot1_averageLow.ToString(CultureInfo.InvariantCulture) + Environment.NewLine);
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
            FpsGraphDataContext.BuildPlotmodel(new VisibleGraphs(GpuLoad, CpuLoad, CpuMaxThreadLoad, GpuPowerLimit, ShowAggregationSeparators));
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
            RemainingRecordingTime = Math.Round(MaxRecordingTime, 2)
                .ToString(CultureInfo.InvariantCulture) + " s";
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

            if (subset != null)
            {
                _onUpdateChart.OnNext(default);

                Task.Factory.StartNew(() => SetStaticChart(subset));
                Task.Factory.StartNew(() => SetStutteringChart(subset));
                Task.Factory.StartNew(() => SetVarianceChart(subset));
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

            if (subset != null)
            {
                Task.Factory.StartNew(() => SetStaticChart(subset));
                Task.Factory.StartNew(() => SetStutteringChart(subset));
                Task.Factory.StartNew(() => SetVarianceChart(subset));
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

        private void SetStaticChart(IList<double> frametimes)
        {
            if (frametimes == null || !frametimes.Any())
                return;

            double GetMetricValue(IList<double> sequence, EMetric metric) =>
                _frametimeStatisticProvider.GetFpsMetricValue(sequence, metric);

            var max = GetMetricValue(frametimes, EMetric.Max);
            var p99_quantile = GetMetricValue(frametimes, EMetric.P99);
            var p95_quantile = GetMetricValue(frametimes, EMetric.P95);
            var median = GetMetricValue(frametimes, EMetric.Median);
            var average = GetMetricValue(frametimes, EMetric.Average);
            var p0dot1_quantile = GetMetricValue(frametimes, EMetric.P0dot1);
            var p0dot2_quantile = GetMetricValue(frametimes, EMetric.P0dot2);
            var p1_quantile = GetMetricValue(frametimes, EMetric.P1);
            var p5_quantile = GetMetricValue(frametimes, EMetric.P5);
            var p1_averageLow = GetMetricValue(frametimes, EMetric.OnePercentLow);
            var p0dot1_averageLow = GetMetricValue(frametimes, EMetric.ZerodotOnePercentLow);
            var min = GetMetricValue(frametimes, EMetric.Min);
            var adaptiveStandardDeviation = GetMetricValue(frametimes, EMetric.AdaptiveStd);
            var cpuFpsPerWatt = _frametimeStatisticProvider
                .GetPhysicalMetricValue(frametimes, EMetric.CpuFpsPerWatt,
                SensorReport.GetAverageSensorValues(_session.Runs.Select(run => run.SensorData2), EReportSensorName.CpuPower,
                _localRecordDataServer.CurrentTime, _localRecordDataServer.CurrentTime + _localRecordDataServer.WindowLength));
            var gpuFpsPerWatt = _frametimeStatisticProvider
            .GetPhysicalMetricValue(frametimes, EMetric.GpuFpsPerWatt,
            SensorReport.GetAverageSensorValues(_session.Runs.Select(run => run.SensorData2), EReportSensorName.GpuPower,
            _localRecordDataServer.CurrentTime, _localRecordDataServer.CurrentTime + _localRecordDataServer.WindowLength));

            Application.Current.Dispatcher.Invoke(new Action(() =>
            {
                IChartValues values = new ChartValues<double>();

                if (_appConfiguration.UseSingleRecordGpuFpsPerWattParameter)
                    values.Add(gpuFpsPerWatt);
                if (_appConfiguration.UseSingleRecordCpuFpsPerWattParameter)
                    values.Add(cpuFpsPerWatt);
                if (_appConfiguration.UseSingleRecordAdaptiveSTDStatisticParameter && !double.IsNaN(adaptiveStandardDeviation))
                    values.Add(adaptiveStandardDeviation);
                if (_appConfiguration.UseSingleRecordMinStatisticParameter)
                    values.Add(min);
                if (_appConfiguration.UseSingleRecordP0Dot1LowAverageStatisticParameter && !double.IsNaN(p0dot1_averageLow))
                    values.Add(p0dot1_averageLow);
                if (_appConfiguration.UseSingleRecordP0Dot1QuantileStatisticParameter)
                    values.Add(p0dot1_quantile);
                if (_appConfiguration.UseSingleRecordP0Dot2QuantileStatisticParameter)
                    values.Add(p0dot2_quantile);
                if (_appConfiguration.UseSingleRecordP1LowAverageStatisticParameter && !double.IsNaN(p1_averageLow))
                    values.Add(p1_averageLow);
                if (_appConfiguration.UseSingleRecordP1QuantileStatisticParameter)
                    values.Add(p1_quantile);
                if (_appConfiguration.UseSingleRecordP5QuantileStatisticParameter)
                    values.Add(p5_quantile);
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

                StatisticCollection = new SeriesCollection
                {
                    new RowSeries
                    {
                        Title = RecordInfo.GameName,
                        Fill = new SolidColorBrush(Color.FromRgb(241, 125, 32)),
                        Values = values,
                        DataLabels = true,
                        FontSize = 11,
                        MaxRowHeigth = 30,
                        Foreground = new SolidColorBrush(_appConfiguration.UseDarkMode ? Colors.White : Colors.Black)
                    }
                };

                double maxOffset = (values as IList<double>).Max() * 0.15;
                BarMaxValue = (int)((values as IList<double>).Max() + maxOffset);

                var parameterLabelList = new List<string>();

                //{ "Adaptive STDEV", "Min", "0.1% Low", "0.1%", "0.2%", "1% Low", "1%", "5%", "Average", "95%", "99%", "Max" }

                if (_appConfiguration.UseSingleRecordGpuFpsPerWattParameter)
                    parameterLabelList.Add("GPU FPS/10W");
                if (_appConfiguration.UseSingleRecordCpuFpsPerWattParameter)
                    parameterLabelList.Add("CPU FPS/10W");
                if (_appConfiguration.UseSingleRecordAdaptiveSTDStatisticParameter && !double.IsNaN(adaptiveStandardDeviation))
                    parameterLabelList.Add("Adaptive STDEV");
                if (_appConfiguration.UseSingleRecordMinStatisticParameter)
                    parameterLabelList.Add("Min");
                if (_appConfiguration.UseSingleRecordP0Dot1LowAverageStatisticParameter && !double.IsNaN(p0dot1_averageLow))
                    parameterLabelList.Add("0.1% Low");
                if (_appConfiguration.UseSingleRecordP0Dot1QuantileStatisticParameter)
                    parameterLabelList.Add("P0.1");
                if (_appConfiguration.UseSingleRecordP0Dot2QuantileStatisticParameter)
                    parameterLabelList.Add("P0.2");
                if (_appConfiguration.UseSingleRecordP1LowAverageStatisticParameter && !double.IsNaN(p1_averageLow))
                    parameterLabelList.Add("1% Low");
                if (_appConfiguration.UseSingleRecordP1QuantileStatisticParameter)
                    parameterLabelList.Add("P1");
                if (_appConfiguration.UseSingleRecordP5QuantileStatisticParameter)
                    parameterLabelList.Add("P5");
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

        private void SetVarianceChart(IList<double> frametimes)
        {
            if (frametimes == null || !frametimes.Any())
                return;

            var variances = _frametimeStatisticProvider.GetFrametimeVariancePercentages(frametimes);


            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                VarianceStatisticCollection = new SeriesCollection
                {
                    new PieSeries
                    {
                        Title = $"< 2ms ({Math.Round(variances[0], 2)}%)",
                        Values = new ChartValues<double>() { variances[0] },
                        DataLabels = false,
                        Fill = new SolidColorBrush(Color.FromRgb(34, 151, 243)), // blue
                        StrokeThickness = 0
                    },

                    new PieSeries
                    {
                        Title = $"< 4ms ({ Math.Round(variances[1], 2) }%)",
                        Values = new ChartValues<double>() { variances[1] },
                        DataLabels = false,
                        Fill = new SolidColorBrush(Color.FromRgb(28, 95, 138)), // dark blue
                        StrokeThickness = 0
                    },

                    new PieSeries
                    {
                        Title = $"< 8ms ({ Math.Round(variances[2], 2) }%)",
                        Values = new ChartValues<double>() { variances[2] },
                        DataLabels = false,
                        Fill = new SolidColorBrush(Color.FromRgb(255, 180, 0)), // yellow
                        StrokeThickness = 0
                    },

                    new PieSeries
                    {
                        Title = $"< 12ms ({ Math.Round(variances[3], 2) }%)",
                        Values = new ChartValues<double>() { variances[3] },
                        DataLabels = false,
                        Fill = new SolidColorBrush(Color.FromRgb(241, 125, 32)), // orange
                        StrokeThickness = 0
                    },

                    new PieSeries
                    {
                        Title = $"> 12ms ({ Math.Round(variances[4], 2) }%)",
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

                        setting = new Tuple<double, double>(median - 4 * iqr, median + 6 * iqr);
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
