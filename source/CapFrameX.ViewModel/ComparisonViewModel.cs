using CapFrameX.Contracts.Configuration;
using CapFrameX.Contracts.Data;
using CapFrameX.Data;
using CapFrameX.EventAggregation.Messages;
using CapFrameX.Extensions;
using CapFrameX.Extensions.NetStandard;
using CapFrameX.MVVM.Dialogs;
using CapFrameX.Sensor.Reporting;
using CapFrameX.Statistics.NetStandard;
using CapFrameX.Statistics.NetStandard.Contracts;
using GongSolutions.Wpf.DragDrop;
using LiveCharts;
using LiveCharts.Defaults;
using LiveCharts.Wpf;
using Microsoft.Extensions.Logging;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Legends;
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
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using ComparisonCollection = System.Collections.ObjectModel
    .ObservableCollection<CapFrameX.ViewModel.ComparisonRecordInfoWrapper>;
using DragDropEffects = System.Windows.DragDropEffects;
using IDropTarget = GongSolutions.Wpf.DragDrop.IDropTarget;
using Point = CapFrameX.Statistics.NetStandard.Point;

namespace CapFrameX.ViewModel
{
    public partial class ComparisonViewModel : BindableBase, INavigationAware, IDropTarget
    {
        private static readonly int PART_LENGTH = 42;

        private readonly IStatisticProvider _frametimeStatisticProvider;
        private readonly IFrametimeAnalyzer _frametimeAnalyzer;
        private readonly IEventAggregator _eventAggregator;
        private readonly IAppConfiguration _appConfiguration;
        private readonly RecordManager _recordManager;
        private readonly ILogger<ComparisonViewModel> _logger;

        private const double BARCHART_FACTOR_ONE_METRIC = 1.6;
        private const double BARCHART_FACTOR_TWO_METRICS = 1.1;

        private PlotModel _comparisonFrametimesModel;
        private PlotModel _comparisonFpsModel;
        private PlotModel _comparisonDistributionModel;
        private SeriesCollection _comparisonRowChartSeriesCollection;
        private SeriesCollection _comparisonRowChartSeriesCollectionLegend;
        private SeriesCollection _varianceStatisticCollection;
        private string[] _comparisonRowChartLabels;
        private Func<double, string> _percentageFormatter;
        private SeriesCollection _comparisonLShapeCollection;
        private string _comparisonItemControlHeight = "300";
        private string _columnChartYAxisTitle = "FPS";
        private ComparisonColorManager _comparisonColorManager = new ComparisonColorManager();
        private bool _useEventMessages;
        private string _remainingRecordingTime;
        private double _firstSeconds;
        private double _lastSeconds;
        private bool _isContextLegendActive = true;
        private double _maxRecordingTime;
        private bool _doUpdateCharts = true;
        private double _barChartHeight;
        private double _varianceChartHeight;
        private bool _hasComparisonItems;
        private TabItem _selectedChartItem;
        private bool _isSortModeAscending = false;
        private string _selectedSortMetric = "First Metric";
        private Func<double, string> _comparisonColumnChartFormatter;
        private bool _colorPickerVisibility;
        private EMetric _selectedFirstMetric = EMetric.Average;
        private EMetric _selectedSecondMetric = EMetric.P1;
        private EMetric _selectedThirdMetric = EMetric.P0dot2;
        private EComparisonContext _selectedComparisonContext = EComparisonContext.DateTime;
        private EComparisonContext _selectedSecondComparisonContext = EComparisonContext.None;
        private string _currentGameName;
        private bool _hasUniqueGameNames;
        private bool _useComparisonGrouping;
        private bool _useComparisonRelativeMode;
        private bool _isRangeSliderActive;
        private bool _messageDialogContentIsOpen;
        private MessageDialog _messageDialogContent;
        private string _messageText;
        private int _barMaxValue;
        private int _barMinValue;
        private double _legendFontSizeFactor = 1.0;
        private double _varianceBarMinValue;
        private bool _varianceAutoScaling = true;
        private bool _showCustomTitle;
        private string _selectedChartView = "Frametimes";
        private EFilterMode _selectedFilterMode;
        private string _lShapeYaxisLabel = "Frametimes (ms)" + Environment.NewLine + " ";
        private bool _showGpuActiveLineCharts;

        public Array FirstMetricItems => Enum.GetValues(typeof(EMetric))
            .Cast<EMetric>().Where(metric => metric != EMetric.None && metric != EMetric.GpuActiveAverage && metric != EMetric.GpuActiveOnePercentLowAverage && metric != EMetric.GpuActiveP1)
            .ToArray();

        public Array SecondMetricItems => Enum.GetValues(typeof(EMetric))
            .Cast<EMetric>().Where(metric => metric != EMetric.GpuActiveAverage && metric != EMetric.GpuActiveOnePercentLowAverage && metric != EMetric.GpuActiveP1)
            .ToArray();
        public Array ThirdMetricItems => Enum.GetValues(typeof(EMetric))
            .Cast<EMetric>().Where(metric => metric != EMetric.GpuActiveAverage && metric != EMetric.GpuActiveOnePercentLowAverage && metric != EMetric.GpuActiveP1)
            .ToArray();

        public Array ComparisonContextItems => Enum.GetValues(typeof(EComparisonContext))
            .Cast<EComparisonContext>()
            .ToArray();

        public Array FilterModes => Enum.GetValues(typeof(EFilterMode))
            .Cast<EFilterMode>()
            .Where(filter => filter != EFilterMode.RawPlusAverage)
            .ToArray();

        public ISubject<Unit> ResetLShapeChart = new Subject<Unit>();

        public Func<double, string> PercentageFormatter
        {
            get { return _percentageFormatter; }
            set
            {
                _percentageFormatter = value;
                RaisePropertyChanged();
            }
        }

        public ComparisonColorManager ComparisonColorManager
            => _comparisonColorManager;

        public IEventAggregator EventAggregator
            => _eventAggregator;

        public EMetric SelectedFirstMetric
        {
            get { return _selectedFirstMetric; }
            set
            {
                _appConfiguration.ComparisonFirstMetric =
                    value.ConvertToString();
                _selectedFirstMetric = value;
                RaisePropertyChanged();
                OnMetricChanged();
            }
        }

        public EMetric SelectedSecondMetric
        {
            get { return _selectedSecondMetric; }
            set
            {
                _appConfiguration.ComparisonSecondMetric =
                    value.ConvertToString();
                _selectedSecondMetric = value;
                RaisePropertyChanged();
                OnMetricChanged();
            }
        }
        public EMetric SelectedThirdMetric
        {
            get { return _selectedThirdMetric; }
            set
            {
                _appConfiguration.ComparisonThirdMetric =
                    value.ConvertToString();
                _selectedThirdMetric = value;
                RaisePropertyChanged();
                OnMetricChanged();
            }
        }

        public EComparisonContext SelectedComparisonContext
        {
            get { return _selectedComparisonContext; }
            set
            {
                _appConfiguration.ComparisonContext =
                    value.ConvertToString();
                _selectedComparisonContext = value;
                RaisePropertyChanged();
                OnComparisonContextChanged();
            }
        }

        public EComparisonContext SelectedSecondComparisonContext
        {
            get { return _selectedSecondComparisonContext; }
            set
            {
                _appConfiguration.SecondComparisonContext =
                    value.ConvertToString();
                _selectedSecondComparisonContext = value;
                RaisePropertyChanged();
                OnComparisonContextChanged();
            }
        }

        public Func<double, string> ComparisonColumnChartFormatter
        {
            get { return _comparisonColumnChartFormatter; }
            set
            {
                _comparisonColumnChartFormatter = value;
                RaisePropertyChanged();
            }
        }

        public PlotModel ComparisonFrametimesModel
        {
            get { return _comparisonFrametimesModel; }
            set
            {
                _comparisonFrametimesModel = value;
                RaisePropertyChanged();
            }
        }

        public PlotModel ComparisonFpsModel
        {
            get { return _comparisonFpsModel; }
            set
            {
                _comparisonFpsModel = value;
                RaisePropertyChanged();
            }
        }
        public PlotModel ComparisonDistributionModel
        {
            get { return _comparisonDistributionModel; }
            set
            {
                _comparisonDistributionModel = value;
                RaisePropertyChanged();
            }
        }
        

        public SeriesCollection ComparisonRowChartSeriesCollection
        {
            get { return _comparisonRowChartSeriesCollection; }
            set
            {
                _comparisonRowChartSeriesCollection = value;
                RaisePropertyChanged();
            }
        }

        public SeriesCollection ComparisonRowChartSeriesCollectionLegend
        {
            get { return _comparisonRowChartSeriesCollectionLegend; }
            set
            {
                _comparisonRowChartSeriesCollectionLegend = value;
                RaisePropertyChanged();
            }
        }

        public SeriesCollection ComparisonLShapeCollection
        {
            get { return _comparisonLShapeCollection; }
            set
            {
                _comparisonLShapeCollection = value;
                RaisePropertyChanged();
            }
        }
        public SeriesCollection VarianceStatisticCollection
        {
            get { return _varianceStatisticCollection; }
            set
            {
                _varianceStatisticCollection = value;
                RaisePropertyChanged();
            }
        }

        public string ComparisonLShapeYAxisLabel
        {
            get { return _lShapeYaxisLabel; }
            set
            {
                _lShapeYaxisLabel = value;
                RaisePropertyChanged();
            }

        }

        public string[] ComparisonRowChartLabels
        {
            get { return _comparisonRowChartLabels; }
            set
            {
                _comparisonRowChartLabels = value;
                RaisePropertyChanged();
            }
        }

        public string ComparisonItemControlHeight
        {
            get { return _comparisonItemControlHeight; }
            set
            {
                _comparisonItemControlHeight = value;
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

        public string ColumnChartYAxisTitle
        {
            get { return _columnChartYAxisTitle; }
            set
            {
                _columnChartYAxisTitle = value;
                RaisePropertyChanged();
            }
        }

        public double BarChartHeight
        {
            get { return _barChartHeight; }
            set
            {
                _barChartHeight = value;
                RaisePropertyChanged();
            }
        }

        public double VarianceChartHeight
        {
            get { return _varianceChartHeight; }
            set
            {
                _varianceChartHeight = value;
                RaisePropertyChanged();
            }
        }

        public bool HasComparisonItems
        {
            get { return _hasComparisonItems; }
            set
            {
                _hasComparisonItems = value;
                RaisePropertyChanged();
            }
        }

        public bool ColorPickerVisibility
        {
            get { return _colorPickerVisibility; }
            set
            {
                _colorPickerVisibility = value;
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
            get { return MaxRecordingTime * 0.05; }
        }

        public double FirstSeconds
        {
            get { return _firstSeconds; }
            set
            {
                _firstSeconds = value;
                RaisePropertyChanged();
                UpdateCharts();
                RemainingRecordingTime = "(" + Math.Round(LastSeconds - _firstSeconds, 2, MidpointRounding.AwayFromZero)
                    .ToString("0.00", CultureInfo.InvariantCulture) + " s)";
            }
        }

        public double LastSeconds
        {
            get { return _lastSeconds; }
            set
            {
                _lastSeconds = value;
                RaisePropertyChanged();
                UpdateCharts();
                RemainingRecordingTime = "(" + Math.Round(_lastSeconds - FirstSeconds, 2, MidpointRounding.AwayFromZero)
                    .ToString("0.00", CultureInfo.InvariantCulture) + " s)";
            }
        }

        public bool IsContextLegendActive
        {
            get { return _isContextLegendActive; }
            set
            {
                _isContextLegendActive = value;
                RaisePropertyChanged();
                OnShowContextLegendChanged();
            }
        }

        public TabItem SelectedChartItem
        {
            get { return _selectedChartItem; }
            set
            {
                _selectedChartItem = value;
                RaisePropertyChanged();
                RaisePropertyChanged(nameof(IsBarChartTabActive));
                RaisePropertyChanged(nameof(IsLineChartTabActive));
                RaisePropertyChanged(nameof(IsVarianceChartTabActive));
                OnChartItemChanged();
                UpdateCharts();

                if (!IsLineChartTabActive)
                {
                    SortComparisonItems();
                }
            }
        }

        public bool IsSortModeAscending
        {
            get { return _isSortModeAscending; }
            set
            {
                _isSortModeAscending = value;
                RaisePropertyChanged();
                OnSortModeChanged();
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

        public bool HasUniqueGameNames
        {
            get { return _hasUniqueGameNames; }
            set
            {
                _hasUniqueGameNames = value;
                RaisePropertyChanged();
                RaisePropertyChanged(nameof(ShowGameNameTitle));
            }
        }

        public bool ShowGameNameTitle
            => HasUniqueGameNames && !ShowCustomTitle;

        public bool UseComparisonGrouping
        {
            get { return _useComparisonGrouping; }
            set
            {
                _useComparisonGrouping = value;
                RaisePropertyChanged();
                OnComparisonGroupingChanged();
            }
        }

        public bool UseComparisonRelativeMode
        {
            get { return _useComparisonRelativeMode; }
            set
            {
                _useComparisonRelativeMode = value;
                RaisePropertyChanged();
            }
        }

        public bool IsRangeSliderActive
        {
            get { return _isRangeSliderActive; }
            set
            {
                _isRangeSliderActive = value;
                RaisePropertyChanged();
                OnRangeSliderChanged();
            }
        }

        public MessageDialog MessageDialogContent
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
                BarMinValue = (int)(-value * 0.05);
                RaisePropertyChanged();
            }
        }

        public int BarMinValue
        {
            get { return _barMinValue; }
            set
            {
                _barMinValue = value;
                RaisePropertyChanged();
            }
        }

        public double VarianceBarMinValue
        {
            get { return _varianceBarMinValue; }
            set
            {
                _varianceBarMinValue = value;
                RaisePropertyChanged();
                RaisePropertyChanged(nameof(VarianceChartSeparators));
            }
        }

        public bool VarianceAutoScaling
        {
            get { return _varianceAutoScaling; }
            set
            {
                _varianceAutoScaling = value;
                RaisePropertyChanged();
                SetVarianceBarMinValues();
            }
        }

        public bool ShowCustomTitle
        {
            get { return _showCustomTitle; }
            set
            {
                _showCustomTitle = value;
                RaisePropertyChanged();
                RaisePropertyChanged(nameof(ShowGameNameTitle));
            }
        }

        public string SelectedChartView
        {
            get { return _selectedChartView; }
            set
            {
                _selectedChartView = value;
                if (value == "Frametimes" || value == "GPU Frametimes")
                    ComparisonLShapeYAxisLabel = "Frametimes" + Environment.NewLine + " ";
                else
                    ComparisonLShapeYAxisLabel = "FPS" + Environment.NewLine + " ";
                RaisePropertyChanged();
                UpdateCharts();
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
        public string SelectedSortMetric
        {
            get { return _selectedSortMetric; }
            set
            {
                _selectedSortMetric = value;
                RaisePropertyChanged();
                OnSortModeChanged();
            }
        }

        public bool MetricAndLabelOptionsEnabled => ComparisonRecords.Count > 0;

        public double VarianceChartSeparators
        {
            get
            {
                double steps;

                if (_varianceBarMinValue >= 0.99)
                    steps = 0.001;
                else if (_varianceBarMinValue >= 0.982)
                    steps = 0.002;
                else if (_varianceBarMinValue >= 0.96)
                    steps = 0.005;
                else if (_varianceBarMinValue >= 0.92)
                    steps = 0.01;
                else if (_varianceBarMinValue >= 0.85)
                    steps = 0.02;
                else if (_varianceBarMinValue >= 0.6)
                    steps = 0.05;
                else if (_varianceBarMinValue >= 0.15)
                    steps = 0.1;
                else
                    steps = 0.2;

                return steps;
            }
        }

        public double LegendFontSizeFactor
        {
            get { return _legendFontSizeFactor; }
            set
            {
                _legendFontSizeFactor = value;
                RaisePropertyChanged();
                InitializePlotModels();
                UpdateCharts();
            }
        }

        public bool ShowGpuActiveLineCharts
        {
            get { return _showGpuActiveLineCharts; }
            set
            {
                _showGpuActiveLineCharts = value;
                RaisePropertyChanged();
                UpdateCharts();
            }
        }

        public Color FirstMetricBarColor
        {
            get { return (Color)ColorConverter.ConvertFromString(_appConfiguration.FirstMetricBarColor); }
            set
            {
                _appConfiguration.FirstMetricBarColor = value.ToString();
                OnRowSeriesColorChanged();
                RaisePropertyChanged();
            }
        }

        public Color SecondMetricBarColor
        {
            get { return (Color)ColorConverter.ConvertFromString(_appConfiguration.SecondMetricBarColor); }
            set
            {
                _appConfiguration.SecondMetricBarColor = value.ToString(); ;
                OnRowSeriesColorChanged();
                RaisePropertyChanged();
            }
        }

        public Color ThirdMetricBarColor
        {
            get { return (Color)ColorConverter.ConvertFromString(_appConfiguration.ThirdMetricBarColor); }
            set
            {
                _appConfiguration.ThirdMetricBarColor = value.ToString();
                OnRowSeriesColorChanged();
                RaisePropertyChanged();
            }
        }

        public bool IsBarChartTabActive
        {
            get { return SelectedChartItem?.Header.ToString().Contains("Bar charts") ?? false; }
        }

        public bool IsLineChartTabActive
        {
            get { return SelectedChartItem?.Header.ToString().Contains("Line") ?? false; }
        }

        public bool IsVarianceChartTabActive
        {
            get { return SelectedChartItem?.Header.ToString().Contains("Variances") ?? false; }
        }

        public ICommand RemoveAllComparisonsCommand { get; }

        public ICommand SaveFrametimePlotAsSVG { get; }

        public ICommand SaveFPSPlotAsSVG { get; }

        public ICommand SaveFrametimePlotAsPNG { get; }

        public ICommand SaveFPSPlotAsPNG { get; }

        public ComparisonCollection ComparisonRecords { get; private set; }
            = new ComparisonCollection();

        public double BarChartMaxRowHeight { get; private set; } = 25;

        public Array SortMetricItemsSource 
            => new[] { "First Metric", "Second Metric", "Third Metric", "Comment Label", "CPU Label", "GPU Label" };

        public Array LegendFontSizeItemsSource => new[] { 1, 1.5, 2 };

        public ComparisonViewModel(IStatisticProvider frametimeStatisticProvider,
            IFrametimeAnalyzer frametimeAnalyzer,
            IEventAggregator eventAggregator,
            IAppConfiguration appConfiguration,
            RecordManager recordManager,
            ILogger<ComparisonViewModel> logger)
        {
            _frametimeStatisticProvider = frametimeStatisticProvider;
            _frametimeAnalyzer = frametimeAnalyzer;
            _eventAggregator = eventAggregator;
            _appConfiguration = appConfiguration;
            _recordManager = recordManager;
            _logger = logger;

            RemoveAllComparisonsCommand = new DelegateCommand(OnRemoveAllComparisons);
            ComparisonLShapeCollection = new SeriesCollection();
            MessageDialogContent = new MessageDialog();
            SaveFrametimePlotAsSVG = new DelegateCommand(() => OnSavePlotAsImage("frametimes", "svg"));
            SaveFPSPlotAsSVG = new DelegateCommand(() => OnSavePlotAsImage("fps", "svg"));
            SaveFrametimePlotAsPNG = new DelegateCommand(() => OnSavePlotAsImage("frametimes", "png"));
            SaveFPSPlotAsPNG = new DelegateCommand(() => OnSavePlotAsImage("fps", "png"));

            ComparisonColumnChartFormatter = value => value.ToString(string.Format("F{0}",
            _appConfiguration.FpsValuesRoundingDigits), CultureInfo.InvariantCulture);
            PercentageFormatter = value => value.ToString("P");
            SelectedComparisonContext = _appConfiguration.ComparisonContext.ConvertToEnum<EComparisonContext>();
            SelectedSecondComparisonContext = _appConfiguration.SecondComparisonContext.ConvertToEnum<EComparisonContext>();

            InitializeMetricsFromConfig();
            SetRowSeries();
            SetVarianceSeries();
            SubscribeToSelectRecord();
            SubscribeToUpdateRecordInfos();
            SubscribeToThemeChanged();
        }

        private void InitializePlotModels()
        {
            // Frametimes
            ComparisonFrametimesModel = new PlotModel
            {
                PlotMargins = new OxyThickness(40, 10, 10, 70),
                PlotAreaBorderColor = _appConfiguration.UseDarkMode ? OxyColor.FromArgb(100, 204, 204, 204) : OxyColor.FromArgb(50, 30, 30, 30),
                TextColor = _appConfiguration.UseDarkMode ? OxyColors.White : OxyColors.Black,
                DefaultFontSize = 13 * _legendFontSizeFactor
            };

            ComparisonFrametimesModel.Legends.Add(new Legend()
            {
                LegendPosition = LegendPosition.TopCenter,
                LegendOrientation = LegendOrientation.Horizontal,
                SeriesInvisibleTextColor = _appConfiguration.UseDarkMode ? OxyColors.White : OxyColors.Black
            });

            //Axes
            //X
            ComparisonFrametimesModel.Axes.Add(new LinearAxis()
            {
                Key = "xAxis",
                Position = OxyPlot.Axes.AxisPosition.Bottom,
                Title = "Recording time [s]",
                AxisTitleDistance = 10,
                FontSize = 13,
                MajorGridlineStyle = LineStyle.Solid,
                MajorGridlineThickness = 1,
                MajorGridlineColor = _appConfiguration.UseDarkMode ? OxyColor.FromArgb(40, 204, 204, 204) : OxyColor.FromArgb(20, 30, 30, 30),
                MinorTickSize = 0,
                MajorTickSize = 0

            });

            //Y
            ComparisonFrametimesModel.Axes.Add(new LinearAxis()
            {
                Key = "yAxis",
                Position = OxyPlot.Axes.AxisPosition.Left,
                Title = "Frametime [ms]",
                AxisTitleDistance = 10,
                FontSize = 13,
                MajorGridlineStyle = LineStyle.Solid,
                MajorGridlineThickness = 1,
                MajorGridlineColor = _appConfiguration.UseDarkMode ? OxyColor.FromArgb(40, 204, 204, 204) : OxyColor.FromArgb(20, 30, 30, 30),
                AbsoluteMinimum = 0,
                MinorTickSize = 0,
                MajorTickSize = 0
            });

            // FPS
            ComparisonFpsModel = new PlotModel
            {
                PlotMargins = new OxyThickness(40, 10, 10, 70),
                PlotAreaBorderColor = _appConfiguration.UseDarkMode ? OxyColor.FromArgb(40, 204, 204, 204) : OxyColor.FromArgb(20, 30, 30, 30),
                TextColor = _appConfiguration.UseDarkMode ? OxyColors.White : OxyColors.Black,
                DefaultFontSize = 13 * _legendFontSizeFactor
            };

            ComparisonFpsModel.Legends.Add(new Legend()
            {
                LegendPosition = LegendPosition.TopCenter,
                LegendOrientation = LegendOrientation.Horizontal,
                SeriesInvisibleTextColor = _appConfiguration.UseDarkMode ? OxyColors.White : OxyColors.Black
            });

            //Axes
            //X
            ComparisonFpsModel.Axes.Add(new LinearAxis()
            {
                Key = "xAxis",
                Position = OxyPlot.Axes.AxisPosition.Bottom,
                Title = "Recording time [s]",
                AxisTitleDistance = 10,
                FontSize = 13,
                MajorGridlineStyle = LineStyle.Solid,
                MajorGridlineThickness = 1,
                MajorGridlineColor = _appConfiguration.UseDarkMode ? OxyColor.FromArgb(40, 204, 204, 204) : OxyColor.FromArgb(20, 30, 30, 30),
                MinorTickSize = 0,
                MajorTickSize = 0
            });

            //Y
            ComparisonFpsModel.Axes.Add(new LinearAxis()
            {
                Key = "yAxis",
                Position = OxyPlot.Axes.AxisPosition.Left,
                Title = "FPS [1/s]",
                AxisTitleDistance = 10,
                FontSize = 13,
                MajorGridlineStyle = LineStyle.Solid,
                MajorGridlineThickness = 1,
                MajorGridlineColor = _appConfiguration.UseDarkMode ? OxyColor.FromArgb(40, 204, 204, 204) : OxyColor.FromArgb(20, 30, 30, 30),
                AbsoluteMinimum = 0,
                MinorTickSize = 0,
                MajorTickSize = 0
            });


            // Distribution
            ComparisonDistributionModel = new PlotModel
            {
                PlotMargins = new OxyThickness(40, 10, 10, 70),
                PlotAreaBorderColor = _appConfiguration.UseDarkMode ? OxyColor.FromArgb(40, 204, 204, 204) : OxyColor.FromArgb(20, 30, 30, 30),
                TextColor = _appConfiguration.UseDarkMode ? OxyColors.White : OxyColors.Black,
                DefaultFontSize = 13 * _legendFontSizeFactor
            };

            ComparisonDistributionModel.Legends.Add(new Legend()
            {
                LegendPosition = LegendPosition.TopCenter,
                LegendOrientation = LegendOrientation.Horizontal,
                SeriesInvisibleTextColor = _appConfiguration.UseDarkMode ? OxyColors.White : OxyColors.Black
            });

            //Axes
            //X
            ComparisonFpsModel.Axes.Add(new LinearAxis()
            {
                Key = "xAxis",
                Position = OxyPlot.Axes.AxisPosition.Bottom,
                Title = "Frame time [ms]",
                AxisTitleDistance = 10,
                FontSize = 13,
                MajorGridlineStyle = LineStyle.Solid,
                MajorGridlineThickness = 1,
                MajorGridlineColor = _appConfiguration.UseDarkMode ? OxyColor.FromArgb(40, 204, 204, 204) : OxyColor.FromArgb(20, 30, 30, 30),
                MinorTickSize = 0,
                MajorTickSize = 0
            });

            //Y
            ComparisonFpsModel.Axes.Add(new LinearAxis()
            {
                Key = "yAxis",
                Position = OxyPlot.Axes.AxisPosition.Left,
                Title = "Frame Time Distribution [%]",
                AxisTitleDistance = 10,
                FontSize = 13,
                MajorGridlineStyle = LineStyle.Solid,
                MajorGridlineThickness = 1,
                MajorGridlineColor = _appConfiguration.UseDarkMode ? OxyColor.FromArgb(40, 204, 204, 204) : OxyColor.FromArgb(20, 30, 30, 30),
                AbsoluteMinimum = 0,
                MinorTickSize = 0,
                MajorTickSize = 0
            });

        }

        private void SetRowSeries()
        {
            double fontSizeLabels = 12;

            double firstRowHeight = BarChartMaxRowHeight;
            double secondRowHeight = BarChartMaxRowHeight;
            double thirdRowHeight = BarChartMaxRowHeight;

            if (SelectedSecondMetric == EMetric.None && SelectedThirdMetric == EMetric.None)
            {
                firstRowHeight = BarChartMaxRowHeight * BARCHART_FACTOR_ONE_METRIC;
                fontSizeLabels = 13;
            }
            else if (SelectedThirdMetric == EMetric.None || SelectedThirdMetric == EMetric.None)
            {
                firstRowHeight = BarChartMaxRowHeight * BARCHART_FACTOR_TWO_METRICS;
                secondRowHeight = firstRowHeight;
                thirdRowHeight = firstRowHeight;
            }

            ComparisonRowChartSeriesCollection = new SeriesCollection()
            {
                new RowSeries
                {
                    Values = new ChartValues<double>(),
                    Fill = new SolidColorBrush(FirstMetricBarColor),
                    HighlightFill = new SolidColorBrush(CreateHighlightColor(FirstMetricBarColor)),
                    //HighlightFill = new SolidColorBrush(Color.FromRgb(122, 192, 247)),
                    FontSize = fontSizeLabels,
                    Stroke= Brushes.Transparent,
                    StrokeThickness = 2,
                    DataLabels = true,
                    MaxRowHeigth = firstRowHeight,
                    RowPadding = 0,
                    UseRelativeMode = true
                }
            };

            if (SelectedSecondMetric != EMetric.None)
            {
                // second metric
                ComparisonRowChartSeriesCollection.Add(
                    new RowSeries
                    {
                        Values = new ChartValues<double>(),
                        Fill = new SolidColorBrush(SecondMetricBarColor),
                        HighlightFill = new SolidColorBrush(CreateHighlightColor(SecondMetricBarColor)),
                        //HighlightFill = new SolidColorBrush(Color.FromRgb(245, 164, 98)),
                        FontSize = fontSizeLabels,
                        Stroke = Brushes.Transparent,
                        StrokeThickness = 2,
                        DataLabels = true,
                        MaxRowHeigth = secondRowHeight,
                        RowPadding = 0,
                        UseRelativeMode = true
                    });
            }

            if (SelectedThirdMetric != EMetric.None)
            {
                // third metric
                ComparisonRowChartSeriesCollection.Add(
                    new RowSeries
                    {
                        Values = new ChartValues<double>(),
                        Fill = new SolidColorBrush(ThirdMetricBarColor),
                        HighlightFill = new SolidColorBrush(CreateHighlightColor(ThirdMetricBarColor)),
                        //HighlightFill = new SolidColorBrush(Color.FromRgb(245, 217, 128)),
                        FontSize = fontSizeLabels,
                        Stroke = Brushes.Transparent,
                        StrokeThickness = 2,
                        DataLabels = true,
                        MaxRowHeigth = thirdRowHeight,
                        RowPadding = 0,
                        UseRelativeMode = true
                    });
            }


            // Second SeriesCollection to show the legend for the main SeriesCollection
            ComparisonRowChartSeriesCollectionLegend = new SeriesCollection()
            {
                new RowSeries
                {
                    Title = GetDescriptionAndFpsUnit(SelectedFirstMetric),
                    Values = new ChartValues<double>(),
                    Fill = new SolidColorBrush(FirstMetricBarColor)
                    //Fill = new SolidColorBrush(Color.FromRgb(34, 151, 243)),
                }
            };

            if (SelectedSecondMetric != EMetric.None)
            {
                // second metric
                ComparisonRowChartSeriesCollectionLegend.Add(
                new RowSeries
                {
                    Title = GetDescriptionAndFpsUnit(SelectedSecondMetric),
                    Values = new ChartValues<double>(),
                    Fill = new SolidColorBrush(SecondMetricBarColor)
                    //Fill = new SolidColorBrush(Color.FromRgb(241, 125, 32)),
                });
            }

            if (SelectedThirdMetric != EMetric.None)
            {
                // third metric
                ComparisonRowChartSeriesCollectionLegend.Add(
                new RowSeries
                {
                    Title = GetDescriptionAndFpsUnit(SelectedThirdMetric),
                    Values = new ChartValues<double>(),
                    Fill = new SolidColorBrush(ThirdMetricBarColor)
                    //Fill = new SolidColorBrush(Color.FromRgb(255, 180, 0)),
                });
            }
        }

        private void SetVarianceSeries()
        {
            VarianceStatisticCollection = new SeriesCollection
                {
                    new StackedRowSeries
                    {
                        Title = $"< 2ms",
                        Values = new ChartValues<double>(),
                        DataLabels = false,
                        Fill = new SolidColorBrush(Color.FromRgb(34, 151, 243)), // blue
                        StrokeThickness = 0,
                        StackMode = StackMode.Percentage,
                        MaxRowHeight = 50
                    },

                    new StackedRowSeries
                    {
                        Title = $"< 4ms",
                        Values = new ChartValues<double>(),
                        DataLabels = false,
                        Fill = new SolidColorBrush(Color.FromRgb(15, 120, 180)), // dark blue
                        StrokeThickness = 0,
                        StackMode = StackMode.Percentage,
                        MaxRowHeight = 50
                    },

                    new StackedRowSeries
                    {
                        Title = $"< 8ms",
                        Values = new ChartValues<double>(),
                        DataLabels = false,
                        Fill = new SolidColorBrush(Color.FromRgb(255, 180, 0)), // yellow
                        StrokeThickness = 0,
                        StackMode = StackMode.Percentage,
                        MaxRowHeight = 50,
                    },

                    new StackedRowSeries
                    {
                        Title = $"< 12ms",
                        Values = new ChartValues<double>(),
                        DataLabels = false,
                        Fill = new SolidColorBrush(Color.FromRgb(241, 125, 32)), // orange
                        StrokeThickness = 0,
                        StackMode = StackMode.Percentage,
                        MaxRowHeight = 50
                    },

                    new StackedRowSeries
                    {
                        Title = $"> 12ms",
                        Values = new ChartValues<double>(),
                        DataLabels = false,
                        Fill = new SolidColorBrush(Color.FromRgb(200, 0, 0)), // red
                        StrokeThickness = 0,
                        StackMode = StackMode.Percentage,
                        MaxRowHeight = 50
                    }
                };
        }

        private void UpdateCharts()
        {
            if (!_doUpdateCharts)
                return;
            ResetBarChartSeriesTitles();
            ComparisonFrametimesModel.Series.Clear();
            ComparisonFpsModel.Series.Clear();
            ComparisonLShapeCollection.Clear();

            //Reset axes of all charts
            ResetLShapeChart.OnNext(default);
            ComparisonFrametimesModel.ResetAllAxes();
            ComparisonFpsModel.ResetAllAxes();

            if (SelectedChartItem?.Header.ToString().Contains("Bar charts") ?? false)
                SetColumnChart();
            else if (SelectedChartItem?.Header.ToString().Contains("Variances") ?? false)
                SetVarianceChart();
            else
            {
                SetFrametimeChart();
                SetFpsChart();
                SetLShapeChart();
            }
            OnComparisonContextChanged();

            RaisePropertyChanged(nameof(MetricAndLabelOptionsEnabled));
        }

        private void OnChartItemChanged()
            => ColorPickerVisibility = SelectedChartItem?.Header.ToString().Contains("Line") ?? false;

        private void OnSortModeChanged()
            => SortComparisonItems();

        private void OnComparisonGroupingChanged()
            => SortComparisonItems();

        private void OnMetricChanged()
        {
            SetRowSeries();

            double GetMetricValue(IList<double> sequence, EMetric metric) =>
                _frametimeStatisticProvider.GetFpsMetricValue(sequence, metric);

            for (int i = 0; i < ComparisonRecords.Count; i++)
            {
                var currentWrappedComparisonInfo = ComparisonRecords[i];

                double startTime = FirstSeconds;
                double endTime = LastSeconds;
                var frametimeTimeWindow = currentWrappedComparisonInfo.WrappedRecordInfo.Session
                    .GetFrametimeTimeWindow(startTime, endTime, _appConfiguration, ERemoveOutlierMethod.None);

                var displayChangeTimeWindow = currentWrappedComparisonInfo.WrappedRecordInfo.Session
                    .GetDisplayChangeTimeWindow(startTime, endTime, _appConfiguration, ERemoveOutlierMethod.None);

                var gpuBusyTimeWindow = currentWrappedComparisonInfo.WrappedRecordInfo.Session
                    .GetGpuActiveTimeTimeWindow(startTime, endTime, _appConfiguration, ERemoveOutlierMethod.None);

                for (int j = 0; j < ComparisonRowChartSeriesCollection.Count; j++)
                {
                    var metric = GetMetricByIndex(j);
                    double metricValue = 0;

                    if (metric == EMetric.CpuFpsPerWatt)
                    {
                        metricValue =
                        _frametimeStatisticProvider.GetPhysicalMetricValue(frametimeTimeWindow, EMetric.CpuFpsPerWatt,
                             SensorReport.GetAverageSensorValues(currentWrappedComparisonInfo.WrappedRecordInfo.Session.Runs.Select(run => run.SensorData2), EReportSensorName.CpuPower,
                             startTime, endTime));
                    }
                    else if (metric == EMetric.GpuFpsPerWatt)
                    {
                        metricValue =
                            _frametimeStatisticProvider.GetPhysicalMetricValue(frametimeTimeWindow, EMetric.GpuFpsPerWatt,
                                 SensorReport.GetAverageSensorValues(currentWrappedComparisonInfo.WrappedRecordInfo.Session.Runs.Select(run => run.SensorData2), EReportSensorName.GpuPower,
                                 startTime, endTime, _appConfiguration.UseTBPSim));
                    }
                    else if (metric == EMetric.GpuActiveAverage || metric == EMetric.GpuActiveP1 || metric == EMetric.GpuActiveOnePercentLowAverage)

                    {
                        metricValue = GetMetricValue(gpuBusyTimeWindow, metric);

                    }
                    else
                    {
                        // Always use frame times when comes average fps
                        if (metric == EMetric.Average)
                        {
                            metricValue = GetMetricValue(frametimeTimeWindow, metric);
                        }
                        else
                        {
                            var samples = _appConfiguration.UseDisplayChangeMetrics
                                ? displayChangeTimeWindow : frametimeTimeWindow;
                          
                            metricValue = GetMetricValue(displayChangeTimeWindow, metric);
                        }

                    }

                    (ComparisonRowChartSeriesCollection[j] as RowSeries).Title = GetDescriptionAndFpsUnit(metric);
                    ComparisonRowChartSeriesCollection[j].Values.Insert(0, metricValue);
                }
            }

            SetBarMinMaxValues();
            UpdateBarChartHeight();
        }

        private void OnRowSeriesColorChanged()
        {
            for (int i = 0; i < ComparisonRowChartSeriesCollection.Count; i++)
            {
                if (i == 0)
                {
                    (ComparisonRowChartSeriesCollection[i] as RowSeries).Fill = new SolidColorBrush(FirstMetricBarColor);
                    (ComparisonRowChartSeriesCollection[i] as RowSeries).HighlightFill = new SolidColorBrush(CreateHighlightColor(FirstMetricBarColor));
                    (ComparisonRowChartSeriesCollectionLegend[i] as RowSeries).Fill = new SolidColorBrush(FirstMetricBarColor);
                }

                else if (i == 1 && SelectedSecondMetric == EMetric.None)
                {
                    (ComparisonRowChartSeriesCollection[i] as RowSeries).Fill = new SolidColorBrush(ThirdMetricBarColor);
                    (ComparisonRowChartSeriesCollection[i] as RowSeries).HighlightFill = new SolidColorBrush(CreateHighlightColor(ThirdMetricBarColor));
                    (ComparisonRowChartSeriesCollectionLegend[i] as RowSeries).Fill = new SolidColorBrush(ThirdMetricBarColor);
                }

                else if (i == 1)
                {
                    (ComparisonRowChartSeriesCollection[i] as RowSeries).Fill = new SolidColorBrush(SecondMetricBarColor);
                    (ComparisonRowChartSeriesCollection[i] as RowSeries).HighlightFill = new SolidColorBrush(CreateHighlightColor(SecondMetricBarColor));
                    (ComparisonRowChartSeriesCollectionLegend[i] as RowSeries).Fill = new SolidColorBrush(SecondMetricBarColor);
                }

                else if (i == 2)
                {
                    (ComparisonRowChartSeriesCollection[i] as RowSeries).Fill = new SolidColorBrush(ThirdMetricBarColor);
                    (ComparisonRowChartSeriesCollection[i] as RowSeries).HighlightFill = new SolidColorBrush(CreateHighlightColor(ThirdMetricBarColor));
                    (ComparisonRowChartSeriesCollectionLegend[i] as RowSeries).Fill = new SolidColorBrush(ThirdMetricBarColor);
                }

            }
        }

        private void OnFilterModeChanged()
        {
            ComparisonFpsModel.Series.Clear();
            SetFpsChart();
            OnComparisonContextChanged();
        }

        private void OnRangeSliderChanged()
        {
            UpdateRangeSliderParameter();
            UpdateCharts();
        }

        public void OnRangeSliderValuesChanged()
        {
            if (FirstSeconds > LastSeconds || FirstSeconds < 0)
                FirstSeconds = 0;

            if (LastSeconds > MaxRecordingTime || LastSeconds <= 0)
                LastSeconds = MaxRecordingTime;
        }

        internal class ChartLabel
        {
            public string GameName;
            public string Context;
        };

        internal ChartLabel GetChartLabel(ComparisonRecordInfo record)
        {
            var firstContext = GetLabelForContext(record, SelectedComparisonContext);
            var secondContext = GetLabelForContext(record, SelectedSecondComparisonContext);

            var gameName = record.Game;
            var context = string.Join(Environment.NewLine, new string[][] { firstContext, secondContext }.Select(labelLines =>
            {
                return string.Join(Environment.NewLine, labelLines.Select(line => line.PadRight(PART_LENGTH)));
            }).Where(line => !string.IsNullOrWhiteSpace(line)));

            return new ChartLabel()
            {
                GameName = gameName,
                Context = context
            };
        }

        private void OnComparisonContextChanged()
        {
            ChartLabel[] GetLabels()
            {
                return ComparisonRecords.Select(record => GetChartLabel(record.WrappedRecordInfo)).ToArray();
            }

            void SetLabels(ChartLabel[] labels)
            {
                ComparisonRowChartLabels = labels.Select(label => GetHasUniqueGameNames() ? label.Context : $"{label.GameName}{Environment.NewLine}{label.Context}").Reverse().ToArray();

                if (IsContextLegendActive)
                {
                    if (ComparisonFrametimesModel.Series.Count == ComparisonRecords.Count)
                    {
                        for (int i = 0; i < ComparisonRecords.Count; i++)
                        {
                            if (!ComparisonRecords[i].IsHideModeSelected)
                                ComparisonFrametimesModel.Series[i].Title = labels[i].Context;
                        }
                    }

                    if (ComparisonFpsModel.Series.Count == ComparisonRecords.Count)
                    {
                        for (int i = 0; i < ComparisonRecords.Count; i++)
                        {
                            if (!ComparisonRecords[i].IsHideModeSelected)
                                ComparisonFpsModel.Series[i].Title = labels[i].Context;
                        }
                    }
                }
            }

            if (ComparisonFrametimesModel == null
                || ComparisonFpsModel == null)
            {
                InitializePlotModels();
            }
            SetLabels(GetLabels());
            ComparisonFrametimesModel.InvalidatePlot(true);
            ComparisonFpsModel.InvalidatePlot(true);
        }

        private void UpdateRangeSliderParameter()
        {
            if (ComparisonRecords == null || !ComparisonRecords.Any())
            {
                FirstSeconds = 0;
                LastSeconds = 0;
                MaxRecordingTime = 0;
                RemainingRecordingTime = "(0.00 s)";
                return;

            }

            double longestRecord = 0;

            foreach (var record in ComparisonRecords)
            {
                if (record.WrappedRecordInfo.Session.Runs.SelectMany(r => r.CaptureData.TimeInSeconds).Last() > longestRecord)
                    longestRecord = record.WrappedRecordInfo.Session.Runs.SelectMany(r => r.CaptureData.TimeInSeconds).Last();
            }

            MaxRecordingTime = longestRecord;

            _doUpdateCharts = false;
            FirstSeconds = 0;
            LastSeconds = MaxRecordingTime;
            _doUpdateCharts = true;

            RemainingRecordingTime = ComparisonRecords.Any() ?
                "(" + Math.Round(MaxRecordingTime, 2, MidpointRounding.AwayFromZero).ToString("0.00", CultureInfo.InvariantCulture) + " s)" : "(0.00 s)"; ;
        }

        private void UpdateAxesMinMaxFrametimeChart()
        {
            if (ComparisonRecords == null || !ComparisonRecords.Any())
                return;

            var xAxis = ComparisonFrametimesModel.GetAxisOrDefault("xAxis", null);
            var yAxis = ComparisonFrametimesModel.GetAxisOrDefault("yAxis", null);

            if (xAxis == null || yAxis == null)
                return;

            xAxis.Reset();

            double xMin = 0;
            double xMax = 0;
            double yMin = 0;
            double yMax = 0;

            double startTime = FirstSeconds;
            double endTime = LastSeconds;

            var sessionParallelQuery = ComparisonRecords.Select(record => record.WrappedRecordInfo.Session).AsParallel();

            xMin = sessionParallelQuery.Min(session =>
            {
                var window = session.GetFrametimePointsTimeWindow(startTime, endTime, _appConfiguration, ERemoveOutlierMethod.None);
                if (window.Any())
                    return window.First().X;
                else
                    return double.MaxValue;
            });

            xMax = sessionParallelQuery.Max(session =>
            {
                var window = session.GetFrametimePointsTimeWindow(startTime, endTime, _appConfiguration, ERemoveOutlierMethod.None);
                if (window.Any())
                    return window.Last().X;
                else
                    return double.MinValue;
            });

            yMin = sessionParallelQuery.Min(session =>
            {
                IList<Point> window = null;

                if (ShowGpuActiveLineCharts)
                    window = session.GetGpuActiveTimePointsTimeWindow(startTime, endTime, _appConfiguration, ERemoveOutlierMethod.None);
                else
                    window = session.GetFrametimePointsTimeWindow(startTime, endTime, _appConfiguration, ERemoveOutlierMethod.None);

                if (window.Any())
                    return window.Min(pnt => pnt.Y);
                else
                    return double.MaxValue;
            });

            yMax = sessionParallelQuery.Max(session =>
            {
                IList<Point> window = null;

                if (ShowGpuActiveLineCharts)
                    window = session.GetGpuActiveTimePointsTimeWindow(startTime, endTime, _appConfiguration, ERemoveOutlierMethod.None);
                else
                    window = session.GetFrametimePointsTimeWindow(startTime, endTime, _appConfiguration, ERemoveOutlierMethod.None);

                if (window.Any())
                    return window.Max(pnt => pnt.Y);
                else
                    return double.MinValue;
            });

            xAxis.Minimum = xMin;
            xAxis.Maximum = xMax;

            yAxis.Minimum = yMin - (yMax - yMin) / 6;
            yAxis.Maximum = yMax + (yMax - yMin) / 6;

            ComparisonFrametimesModel.InvalidatePlot(true);
        }

        // ToDo: Optimieren!
        private void UpdateAxesMinMaxFpsChart()
        {
            if (ComparisonRecords == null || !ComparisonRecords.Any())
                return;

            var xAxis = ComparisonFpsModel.GetAxisOrDefault("xAxis", null);
            var yAxis = ComparisonFpsModel.GetAxisOrDefault("yAxis", null);

            if (xAxis == null || yAxis == null)
                return;

            xAxis.Reset();

            double xMin = 0;
            double xMax = 0;
            double yMin = 0;
            double yMax = 0;

            double startTime = FirstSeconds;
            double endTime = LastSeconds;

            var sessionParallelQuery = ComparisonRecords.Select(record => record.WrappedRecordInfo.Session).AsParallel();

            xMin = sessionParallelQuery.Min(session =>
            {
                IList<Point> window = session.GetFpsPointsTimeWindow(startTime, endTime, _appConfiguration, ERemoveOutlierMethod.None);

                if (window.Any())
                    return window.First().X;
                else
                    return double.MaxValue;
            });

            xMax = sessionParallelQuery.Max(session =>
            {
                IList<Point> window = session.GetFpsPointsTimeWindow(startTime, endTime, _appConfiguration, ERemoveOutlierMethod.None);

                if (window.Any())
                    return window.Last().X;
                else
                    return double.MinValue;
            });

            yMin = sessionParallelQuery.Min(session =>
            {
                IList<Point> window = null;

                //if (ShowGpuActiveLineCharts)
                //	window = session.GetGpuActiveFpsPointsTimeWindow(startTime, endTime, _appConfiguration, ERemoveOutlierMethod.None, SelectedFilterMode);
                //else
                window = session.GetFpsPointsTimeWindow(startTime, endTime, _appConfiguration, ERemoveOutlierMethod.None, SelectedFilterMode);

                if (window.Any())
                    return window.Min(pnt => pnt.Y);
                else
                    return double.MaxValue;
            });

            yMax = sessionParallelQuery.Max(session =>
            {
                IList<Point> window = null;

                //if (ShowGpuActiveLineCharts)
                //	window = session.GetGpuActiveFpsPointsTimeWindow(startTime, endTime, _appConfiguration, ERemoveOutlierMethod.None, SelectedFilterMode);
                //else
                window = session.GetFpsPointsTimeWindow(startTime, endTime, _appConfiguration, ERemoveOutlierMethod.None, SelectedFilterMode);

                if (window.Any())
                    return window.Max(pnt => pnt.Y);
                else
                    return double.MinValue;
            });

            xAxis.Minimum = xMin;
            xAxis.Maximum = xMax;

            yAxis.Minimum = yMin - (yMax - yMin) / 6;
            yAxis.Maximum = yMax + (yMax - yMin) / 6;

            ComparisonFpsModel.InvalidatePlot(true);
        }

        private void UpdateBarChartHeight()
        {
            int recordCount = ComparisonRecords.Count;


            if (SelectedSecondMetric == EMetric.None && SelectedThirdMetric == EMetric.None)
            {
                BarChartHeight = 14 + (ComparisonRowChartSeriesCollection.Count * (BarChartMaxRowHeight * BARCHART_FACTOR_ONE_METRIC) + 20) * recordCount;
            }
            else if (SelectedSecondMetric == EMetric.None || SelectedThirdMetric == EMetric.None)
            {
                BarChartHeight = 14 + (ComparisonRowChartSeriesCollection.Count * (BarChartMaxRowHeight * BARCHART_FACTOR_TWO_METRICS) + 15) * recordCount;
            }
            else
            {
                BarChartHeight = 14 + (ComparisonRowChartSeriesCollection.Count * BarChartMaxRowHeight + 10) * recordCount;
            }
        }

        private void UpdateVarianceChartHeight()
            => VarianceChartHeight =
            50 + (ComparisonRecords.Count * 75);

        private void OnRemoveAllComparisons()
            => RemoveAllComparisonItems(true, true);

        private void ResetBarChartSeriesTitles()
        {
            for (int i = 0; i < ComparisonFrametimesModel.Series.Count; i++)
            {
                ComparisonFrametimesModel.Series[i].Title = string.Empty;
            }

            for (int i = 0; i < ComparisonFpsModel.Series.Count; i++)
            {
                ComparisonFpsModel.Series[i].Title = string.Empty;
            }

            ComparisonRowChartLabels = Array.Empty<string>();
        }


        private void SetBarMinMaxValues()
        {
            if (!ComparisonRowChartSeriesCollection.Any())
                return;

            double maxSecondMetricBarValue = 0;
            double maxThirdMetricBarValue = 0;

            // First metric
            if (!((ComparisonRowChartSeriesCollection[0].Values as IList<double>).Any()))
                return;

            double maxFirstMetricBarValue = (ComparisonRowChartSeriesCollection[0].Values as IList<double>).Max() * 1.15;

            // Second metric
            if (ComparisonRowChartSeriesCollection.Count > 1)
            {
                maxSecondMetricBarValue = (ComparisonRowChartSeriesCollection[1].Values as IList<double>).Max() * 1.15;
            }

            // Second metric
            if (ComparisonRowChartSeriesCollection.Count > 2)
            {
                maxThirdMetricBarValue = (ComparisonRowChartSeriesCollection[2].Values as IList<double>).Max() * 1.15;
            }

            BarMaxValue = (int)(new[] { maxFirstMetricBarValue, maxSecondMetricBarValue, maxThirdMetricBarValue }.Max());
        }

        private void SetVarianceBarMinValues()
        {
            if (!VarianceStatisticCollection.Any())
                return;

            double minValue = 0;

            if (VarianceAutoScaling)
            {
                double lowestFirstBar = (VarianceStatisticCollection[0].Values as IList<double>).Min();

                if (lowestFirstBar > 0.98)
                    minValue = lowestFirstBar * 0.995;
                else if (lowestFirstBar > 0.95)
                    minValue = lowestFirstBar * 0.99;
                else if (lowestFirstBar > 0.90)
                    minValue = lowestFirstBar * 0.98;
                else if (lowestFirstBar > 0.85)
                    minValue = lowestFirstBar * 0.97;
                else if (lowestFirstBar > 0.80)
                    minValue = lowestFirstBar * 0.96;
                else if (lowestFirstBar > 0.70)
                    minValue = lowestFirstBar * 0.95;
                else
                    minValue = lowestFirstBar * 0.75;
            }

            VarianceBarMinValue = minValue;
        }

        private void SetColumnChart()
        {
            foreach (var item in ComparisonRowChartSeriesCollection)
            {
                item.Values.Clear();
            }

            for (int i = 0; i < ComparisonRecords.Count; i++)
            {
                AddToColumnCharts(ComparisonRecords[i]);
            }
        }

        private void SetVarianceChart()
        {
            foreach (var item in VarianceStatisticCollection)
            {
                item.Values.Clear();
            }

            for (int i = 0; i < ComparisonRecords.Count; i++)
            {
                AddToVarianceCharts(ComparisonRecords[i]);
            }
        }

        private void SetFrametimeChart()
        {
            for (int i = 0; i < ComparisonRecords.Count; i++)
            {
                AddToFrametimeChart(ComparisonRecords[i]);
            }

            UpdateAxesMinMaxFrametimeChart();

        }

        private void SetFpsChart()
        {
            for (int i = 0; i < ComparisonRecords.Count; i++)
            {
                AddToFpsChart(ComparisonRecords[i]);
            }

            UpdateAxesMinMaxFpsChart();
        }

        private void SetLShapeChart()
        {
            for (int i = 0; i < ComparisonRecords.Count; i++)
            {
                AddToLShapeChart(ComparisonRecords[i]);
            }

            ResetLShapeChart.OnNext(default);
        }

        private void AddToFrametimeChart(ComparisonRecordInfoWrapper wrappedComparisonInfo)
        {
            double startTime = FirstSeconds;
            double endTime = LastSeconds;
            var session = wrappedComparisonInfo.WrappedRecordInfo.Session;

            IEnumerable<Point> frametimePoints = null;

            if (ShowGpuActiveLineCharts)
            {
                frametimePoints = session.GetGpuActiveTimePointsTimeWindow(startTime, endTime, _appConfiguration, ERemoveOutlierMethod.None)
                   .Select(pnt => new Point(pnt.X, pnt.Y));
            }
            else
            {
                frametimePoints = session.GetFrametimePointsTimeWindow(startTime, endTime, _appConfiguration, ERemoveOutlierMethod.None)
                   .Select(pnt => new Point(pnt.X, pnt.Y));
            }

            var chartTitle = string.Empty;

            var color = wrappedComparisonInfo.FrametimeGraphColor.Value;
            var frametimeSeries = new Statistics.PlotBuilder.LineSeries()
            {
                Tag = wrappedComparisonInfo.WrappedRecordInfo.FileRecordInfo.Id,
                Title = chartTitle,
                StrokeThickness = 1.5,
                LegendStrokeThickness = 4,
                Color = wrappedComparisonInfo.IsHideModeSelected ?
                OxyColors.Transparent : OxyColor.FromRgb(color.R, color.G, color.B),
                EdgeRenderingMode = EdgeRenderingMode.PreferSpeed

            };

            frametimeSeries.Points.AddRange(frametimePoints.Select(pnt => new DataPoint(pnt.X, pnt.Y)));
            ComparisonFrametimesModel.Series.Add(frametimeSeries);
        }

        private void AddToFpsChart(ComparisonRecordInfoWrapper wrappedComparisonInfo)
        {
            double startTime = FirstSeconds;
            double endTime = LastSeconds;
            var session = wrappedComparisonInfo.WrappedRecordInfo.Session;

            IEnumerable<Point> fpsPoints = null;

            //if (ShowGpuActiveLineCharts)
            //	fpsPoints = session.GetGpuActiveFpsPointsTimeWindow(startTime, endTime, _appConfiguration, ERemoveOutlierMethod.None, SelectedFilterMode)
            //	   .Select(pnt => new Point(pnt.X, pnt.Y));
            //else
            fpsPoints = session.GetFpsPointsTimeWindow(startTime, endTime, _appConfiguration, ERemoveOutlierMethod.None, SelectedFilterMode)
                .Select(pnt => new Point(pnt.X, pnt.Y));

            var chartTitle = string.Empty;

            var color = wrappedComparisonInfo.FrametimeGraphColor.Value;
            var fpsSeries = new Statistics.PlotBuilder.LineSeries()
            {
                Tag = wrappedComparisonInfo.WrappedRecordInfo.FileRecordInfo.Id,
                Title = chartTitle,
                StrokeThickness = SelectedFilterMode == EFilterMode.TimeIntervalAverage ? 3 : 1.5,
                LegendStrokeThickness = 4,
                Color = wrappedComparisonInfo.IsHideModeSelected ?
                OxyColors.Transparent : OxyColor.FromRgb(color.R, color.G, color.B),
                InterpolationAlgorithm = SelectedFilterMode == EFilterMode.TimeIntervalAverage ? InterpolationAlgorithms.CanonicalSpline : null,
                EdgeRenderingMode = SelectedFilterMode == EFilterMode.TimeIntervalAverage 
                    ? EdgeRenderingMode.PreferGeometricAccuracy : EdgeRenderingMode.PreferSpeed

            };

            fpsSeries.Points.AddRange(fpsPoints.Select(pnt => new DataPoint(pnt.X, pnt.Y)));
            ComparisonFpsModel.Series.Add(fpsSeries);
        }

        private void AddToLShapeChart(ComparisonRecordInfoWrapper wrappedComparisonInfo)
        {
            var lShapeMetric = SelectedChartView == "FPS" ? ELShapeMetrics.FPS : ELShapeMetrics.Frametimes;
            double startTime = FirstSeconds;
            double endTime = LastSeconds;

            IList<double> frametimeTimeWindow = null;

            if (ShowGpuActiveLineCharts && lShapeMetric == ELShapeMetrics.Frametimes)
            {
                frametimeTimeWindow = wrappedComparisonInfo.WrappedRecordInfo.Session.GetGpuActiveTimeTimeWindow(startTime, endTime, _appConfiguration, ERemoveOutlierMethod.None);
            }
            else
            {
                frametimeTimeWindow = wrappedComparisonInfo.WrappedRecordInfo.Session.GetFrametimeTimeWindow(startTime, endTime, _appConfiguration, ERemoveOutlierMethod.None);
            }

            var fpsTimeWindow = frametimeTimeWindow?.Select(ft => 1000 / ft).ToList();
            string unit = lShapeMetric == ELShapeMetrics.Frametimes ? "ms" : "fps";

            var lShapeQuantiles = _frametimeAnalyzer.GetLShapeQuantiles(lShapeMetric);
            double action(double q) => _frametimeStatisticProvider.GetPQuantileSequence(lShapeMetric == ELShapeMetrics.Frametimes ? frametimeTimeWindow : fpsTimeWindow, q / 100);
            var quantiles = lShapeQuantiles.Select(q => new ObservablePoint(q, action(q)));
            var quantileValues = new ChartValues<ObservablePoint>();
            quantileValues.AddRange(quantiles);

            ComparisonLShapeCollection.Add(
            new LineSeries()
            {
                Id = wrappedComparisonInfo.WrappedRecordInfo.FileRecordInfo.Id,
                Values = quantileValues,
                Stroke = wrappedComparisonInfo.IsHideModeSelected ? Brushes.Transparent : wrappedComparisonInfo.Color,
                Fill = Brushes.Transparent,
                StrokeThickness = 1.5,
                LineSmoothness = 1,
                PointGeometrySize = 5,
                PointGeometry = DefaultGeometries.Square,
                PointForeground = wrappedComparisonInfo.IsHideModeSelected ? Brushes.Transparent : wrappedComparisonInfo.Color,
                LabelPoint = chartPoint => string.Format(CultureInfo.InvariantCulture, "{0:0.##}", chartPoint.Y, unit)
            });
        }

        private void AddToColumnCharts(ComparisonRecordInfoWrapper wrappedComparisonInfo)
        {
            // Update metrics
            SetMetrics(wrappedComparisonInfo);

            // First metric
            ComparisonRowChartSeriesCollection[0].Values.Insert(0, wrappedComparisonInfo.WrappedRecordInfo.FirstMetric);

            // Second metric
            if (ComparisonRowChartSeriesCollection.Count > 1 && SelectedSecondMetric != EMetric.None)
            {
                ComparisonRowChartSeriesCollection[1].Values.Insert(0, wrappedComparisonInfo.WrappedRecordInfo.SecondMetric);
            }

            // Third metric
            if (ComparisonRowChartSeriesCollection.Count > 1 && SelectedSecondMetric == EMetric.None)
            {
                ComparisonRowChartSeriesCollection[1].Values.Insert(0, wrappedComparisonInfo.WrappedRecordInfo.ThirdMetric);
            }
            else if (ComparisonRowChartSeriesCollection.Count > 2)
            {
                ComparisonRowChartSeriesCollection[2].Values.Insert(0, wrappedComparisonInfo.WrappedRecordInfo.ThirdMetric);
            }

            SetBarMinMaxValues();
            OnComparisonContextChanged();
        }

        private void AddToVarianceCharts(ComparisonRecordInfoWrapper wrappedComparisonInfo)
        {
            var variances = _frametimeStatisticProvider.GetFrametimeVariancePercentages(wrappedComparisonInfo.WrappedRecordInfo.Session);

            VarianceStatisticCollection[0].Values.Insert(0, variances[0]);
            VarianceStatisticCollection[1].Values.Insert(0, variances[1]);
            VarianceStatisticCollection[2].Values.Insert(0, variances[2]);
            VarianceStatisticCollection[3].Values.Insert(0, variances[3]);
            VarianceStatisticCollection[4].Values.Insert(0, variances[4]);

            SetVarianceBarMinValues();

        }

        private void InitializeMetricsFromConfig()
        {
            try
            {
                SelectedFirstMetric = _appConfiguration.ComparisonFirstMetric.ConvertToEnum<EMetric>();
                SelectedSecondMetric = _appConfiguration.ComparisonSecondMetric.ConvertToEnum<EMetric>();
                SelectedThirdMetric = _appConfiguration.ComparisonThirdMetric.ConvertToEnum<EMetric>();
            }
            catch
            {
                SelectedFirstMetric = EMetric.Average;
                SelectedSecondMetric = EMetric.P1;
                SelectedThirdMetric = EMetric.P0dot2;
            }
        }

        private string GetDescriptionAndFpsUnit(EMetric metric)
        {
            string description;
            if (metric == EMetric.CpuFpsPerWatt || metric == EMetric.GpuFpsPerWatt)
            {
                description = metric.GetDescription();
            }
            else
                description = $"{metric.GetDescription()} FPS";

            return description;
        }

        private EMetric GetMetricByIndex(int index)
        {
            if (index == 0)
                return SelectedFirstMetric;
            else if (index == 1 && SelectedSecondMetric == EMetric.None)
                return SelectedThirdMetric;
            else if (index == 1)
                return SelectedSecondMetric;
            else if (index == 2)
                return SelectedThirdMetric;
            else
                return 0;
        }

        private ComparisonRecordInfo GetComparisonRecordInfoFromFileRecordInfo(IFileRecordInfo fileRecordInfo)
        {
            string infoText = string.Empty;
            var session = _recordManager.LoadData(fileRecordInfo.FullPath);
            var frameTimes = session.Runs.SelectMany(r => r.CaptureData.MsBetweenPresents).ToList();
            var recordTime = session.Runs.SelectMany(r => r.CaptureData.TimeInSeconds).Last();
            if (session != null)
            {
                var newLine = Environment.NewLine;
                infoText += $"{fileRecordInfo.CreationDate} {fileRecordInfo.CreationTime}" + newLine +
                    $"{frameTimes.Count()} frames in {Math.Round(recordTime, 2, MidpointRounding.AwayFromZero).ToString(CultureInfo.InvariantCulture)}s";
            }

            return new ComparisonRecordInfo
            {
                Game = fileRecordInfo.GameName,
                InfoText = infoText,
                DateTime = fileRecordInfo.FileInfo.LastWriteTime.ToString(),
                Session = session,
                FileRecordInfo = fileRecordInfo
            };
        }

        private ComparisonRecordInfoWrapper GetWrappedRecordInfo(ComparisonRecordInfo comparisonRecordInfo)
        {
            var wrappedComparisonRecordInfo = new ComparisonRecordInfoWrapper(comparisonRecordInfo, this);

            var color = _comparisonColorManager.GetNextFreeColor();
            wrappedComparisonRecordInfo.Color = color;
            wrappedComparisonRecordInfo.FrametimeGraphColor = color.Color;

            return wrappedComparisonRecordInfo;
        }


        private Color CreateHighlightColor(Color color)
        {
            var drawingcolor = System.Drawing.Color.FromArgb(color.A, color.R, color.G, color.B);
            var highlightColor = ControlPaint.LightLight(drawingcolor);

            return Color.FromArgb(highlightColor.A, highlightColor.R, highlightColor.G, highlightColor.B);
        }

        private void SubscribeToSelectRecord()
        {
            _eventAggregator.GetEvent<PubSubEvent<ViewMessages.SelectSession>>()
                .Subscribe(msg =>
                {
                    if (_useEventMessages)
                    {
                        AddComparisonItem(msg.RecordInfo);
                    }
                });
        }

        private void SubscribeToUpdateRecordInfos()
        {
            _eventAggregator.GetEvent<PubSubEvent<ViewMessages.UpdateRecordInfos>>()
                .Subscribe(msg =>
                {
                    if (_useEventMessages)
                    {
                        var recordInfoWrapper = ComparisonRecords
                            .FirstOrDefault(info => info.WrappedRecordInfo
                            .FileRecordInfo.Id == msg.RecordInfo.Id);

                        if (recordInfoWrapper != null)
                        {
                            RemoveComparisonItem(recordInfoWrapper);
                            AddComparisonItem(msg.RecordInfo);
                        }
                    }
                });
        }

        private void SubscribeToThemeChanged()
        {
            _eventAggregator.GetEvent<PubSubEvent<ViewMessages.ThemeChanged>>()
                .Subscribe(msg =>
                {
                    InitializePlotModels();
                    UpdateCharts();
                });
        }

        protected void OnSavePlotAsImage(string plotType, string fileFormat)
        {
            var filename = $"{CurrentGameName}_ComparisonChartExport_{plotType}";


            if (fileFormat == "svg")
            {
                ImageExport.SavePlotAsSVG(plotType == "frametimes" ? ComparisonFrametimesModel : ComparisonFpsModel, filename, _appConfiguration.HorizontalGraphExportRes, _appConfiguration.VerticalGraphExportRes);
            }
            else if (fileFormat == "png")
            {
                ImageExport.SavePlotAsPNG(plotType == "frametimes" ? ComparisonFrametimesModel : ComparisonFpsModel, filename, _appConfiguration.HorizontalGraphExportRes, _appConfiguration.VerticalGraphExportRes, _appConfiguration.UseDarkMode);
            }
        }

        void IDropTarget.Drop(IDropInfo dropInfo)
        {
            if (dropInfo != null)
            {
                if (dropInfo.VisualTarget is FrameworkElement frameworkElement)
                {
                    if (frameworkElement.Name == "ComparisonRecordItemControl" ||
                        frameworkElement.Name == "ComparisonImage")
                    {
                        if (dropInfo.Data is IFileRecordInfo recordInfo)
                        {
                            AddComparisonItem(recordInfo);
                        }
                        else if (dropInfo.Data is IEnumerable<IFileRecordInfo> recordInfos)
                        {
                            recordInfos.ForEach(info => AddComparisonItem(info));
                        }

                        if (dropInfo.Data is ComparisonRecordInfoWrapper wrappedRecordInfo)
                        {
                            // manage sorting
                            int currentIndex = ComparisonRecords.IndexOf(wrappedRecordInfo);

                            if (dropInfo.InsertIndex < ComparisonRecords.Count)
                            {
                                ComparisonRecords.Move(currentIndex, dropInfo.InsertIndex);

                                foreach (var rowSeries in ComparisonRowChartSeriesCollection)
                                {
                                    var chartValueList = (rowSeries.Values as IList<double>).Reverse().ToList();
                                    chartValueList.Move(currentIndex, dropInfo.InsertIndex);
                                    chartValueList.Reverse();
                                    rowSeries.Values.Clear();
                                    rowSeries.Values.AddRange(chartValueList.Select(chartValue => chartValue as object));
                                }

                                var labelList = ComparisonRowChartLabels.Reverse().ToList();
                                labelList.Move(currentIndex, dropInfo.InsertIndex);
                                labelList.Reverse();
                                ComparisonRowChartLabels = labelList.ToArray();
                            }
                        }
                    }
                }
            }
        }

        void IDropTarget.DragOver(IDropInfo dropInfo)
        {
            if (dropInfo != null)
            {
                dropInfo.DropTargetAdorner = DropTargetAdorners.Highlight;
                dropInfo.Effects = DragDropEffects.Move;
            }
        }

        public bool IsNavigationTarget(NavigationContext navigationContext)
        {
            return true;
        }

        public void OnNavigatedFrom(NavigationContext navigationContext)
        {
            _useEventMessages = false;
        }

        public void OnNavigatedTo(NavigationContext navigationContext)
        {
            _useEventMessages = true;
        }
    }
}
