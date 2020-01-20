using CapFrameX.Contracts.Configuration;
using CapFrameX.Contracts.Data;
using CapFrameX.EventAggregation.Messages;
using CapFrameX.Data;
using CapFrameX.Statistics;
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

namespace CapFrameX.ViewModel
{
	public partial class DataViewModel : BindableBase, INavigationAware
	{
		private readonly IStatisticProvider _frametimeStatisticProvider;
		private readonly IFrametimeAnalyzer _frametimeAnalyzer;
		private readonly IEventAggregator _eventAggregator;
		private readonly IAppConfiguration _appConfiguration;
		private readonly IRecordDataServer _localRecordDataServer;

		private bool _useUpdateSession;
		private Session _session;
		private SeriesCollection _statisticCollection;
		private SeriesCollection _lShapeCollection;
		private SeriesCollection _stutteringStatisticCollection;
		private string[] _parameterLabels;
		private string[] _lShapeLabels;
		private string[] _advancedParameterLabels;
		private bool _removeOutliers;
		private List<SystemInfoEntry> _systemInfos;
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
		private int _barMaxValue;

		public IFileRecordInfo RecordInfo { get; private set; }

		public FrametimeGraphDataContext FrametimeGraphDataContext { get; }

		public FpsGraphDataContext FpsGraphDataContext { get; }

		public IAppConfiguration AppConfiguration => _appConfiguration;

		public Array ChartYAxisSettings => Enum.GetValues(typeof(EChartYAxisSetting))
										.Cast<EChartYAxisSetting>()
										.ToArray();

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

		public List<SystemInfoEntry> SystemInfos
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
			get { return MaxRecordingTime * 0.05; }
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
			}
		}

		public ICommand CopyStatisticalParameterCommand { get; }

		public ICommand CopyLShapeQuantilesCommand { get; }

		public ICommand CopySystemInfoCommand { get; }

		public ICommand AcceptParameterSettingsCommand { get; }


		public DataViewModel(IStatisticProvider frametimeStatisticProvider,
							 IFrametimeAnalyzer frametimeAnalyzer,
							 IEventAggregator eventAggregator,
							 IAppConfiguration appConfiguration)
		{
			_frametimeStatisticProvider = frametimeStatisticProvider;
			_frametimeAnalyzer = frametimeAnalyzer;
			_eventAggregator = eventAggregator;
			_appConfiguration = appConfiguration;

			SubscribeToUpdateSession();

			CopyStatisticalParameterCommand = new DelegateCommand(OnCopyStatisticalParameter);
			CopyLShapeQuantilesCommand = new DelegateCommand(OnCopyQuantiles);
			CopySystemInfoCommand = new DelegateCommand(OnCopySystemInfoCommand);
			AcceptParameterSettingsCommand = new DelegateCommand(OnAcceptParameterSettings);

			ParameterFormatter = value => value.ToString(string.Format("F{0}",
				_appConfiguration.FpsValuesRoundingDigits), CultureInfo.InvariantCulture);
			_localRecordDataServer = new LocalRecordDataServer();
			FrametimeGraphDataContext = new FrametimeGraphDataContext(_localRecordDataServer,
				_appConfiguration, _frametimeStatisticProvider);
			FpsGraphDataContext = new FpsGraphDataContext(_localRecordDataServer,
				_appConfiguration, _frametimeStatisticProvider);

			MessageDialogContent = new ContitionalMessageDialog();

			InitializeStatisticParameter();
			SetThresholdLabels();
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
				_frametimeStatisticProvider.GetFpsMetricValue(sequence, metric);

			var max = GeMetricValue(frametimes, EMetric.Max);
			var p99_quantile = GeMetricValue(frametimes, EMetric.P99);
			var p95_quantile = GeMetricValue(frametimes, EMetric.P95);
			var average = GeMetricValue(frametimes, EMetric.Average);
			var p0dot1_quantile = GeMetricValue(frametimes, EMetric.P0dot1);
			var p0dot2_quantile = GeMetricValue(frametimes, EMetric.P0dot2);
			var p1_quantile = GeMetricValue(frametimes, EMetric.P1);
			var p5_quantile = GeMetricValue(frametimes, EMetric.P5);
			var p1_averageLow = GeMetricValue(frametimes, EMetric.OnePercentLow);
			var p0dot1_averageLow = GeMetricValue(frametimes, EMetric.ZerodotOnePercentLow);
			var min = GeMetricValue(frametimes, EMetric.Min);
			var adaptiveStandardDeviation = GeMetricValue(frametimes, EMetric.AdaptiveStd);

			StringBuilder builder = new StringBuilder();

			// Vice versa!
			// "Adaptive STDEV" ,"Min","0.1% Low" ,"0.1%","0.2%" ,"1% Low", "1%" ,"5%" ,"Average" ,"95%" ,"99%" ,"Max"
			if (_appConfiguration.UseSingleRecordMaxStatisticParameter)
				builder.Append("Max" + "\t" + max.ToString(CultureInfo.InvariantCulture) + Environment.NewLine);
			if (_appConfiguration.UseSingleRecord99QuantileStatisticParameter)
				builder.Append("P99" + "\t" + p99_quantile.ToString(CultureInfo.InvariantCulture) + Environment.NewLine);
			if (_appConfiguration.UseSingleRecordP95QuantileStatisticParameter)
				builder.Append("P95" + "\t" + p95_quantile.ToString(CultureInfo.InvariantCulture) + Environment.NewLine);
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

			Clipboard.SetDataObject(builder.ToString(), false);
		}

		private void OnCopyQuantiles()
		{
			if (RecordInfo == null)
				return;

			var lShapeQuantiles = _frametimeAnalyzer.GetLShapeQuantiles();
			var frametimes = GetFrametimesSubset();
			double action(double q) => Math.Round(_frametimeStatisticProvider.GetPQuantileSequence(frametimes, q / 100), 2);

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

			var systemInfos = RecordManager.GetSystemInfos(RecordInfo);

			StringBuilder builder = new StringBuilder();

			foreach (var systemInfo in systemInfos)
			{
				builder.Append(systemInfo.Key + "\t" + systemInfo.Value + Environment.NewLine);
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

		private void UpdateRangeSliderParameter()
		{
			if (_session == null)
				return;

			MaxRecordingTime = _session.FrameStart.Last();

			_doUpdateCharts = false;
			FirstSeconds = 0;
			LastSeconds = MaxRecordingTime;
			_doUpdateCharts = true;
			RemainingRecordingTime = Math.Round(MaxRecordingTime, 2)
				.ToString(CultureInfo.InvariantCulture) + " s";
		}

		private void SubscribeToUpdateSession()
		{
			_eventAggregator.GetEvent<PubSubEvent<ViewMessages.UpdateSession>>()
							.Subscribe(msg =>
							{
								_session = msg.CurrentSession;
								RecordInfo = msg.RecordInfo;

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
				SystemInfos = RecordManager.GetSystemInfos(RecordInfo);

				// Do update actions
				FrametimeGraphDataContext.RecordSession = _session;
				FpsGraphDataContext.RecordSession = _session;

				UpdateRangeSliderParameter();
				UpdateMainCharts();
				UpdateSecondaryCharts();
			}
			else
			{
				ResetData();
			}
		}

		private void UpdateMainCharts()
		{
			if (!_doUpdateCharts)
				return;

			var subset = GetFrametimesSubset();

			if (subset != null)
			{
				Task.Factory.StartNew(() =>
				{
					FrametimeGraphDataContext
					   .SetFrametimeChart(_localRecordDataServer?
					   .GetFrametimePointTimeWindow());
					SetFrametimeChartYAxisSetting(
						GetYAxisSettingFromSelection(SelecetedChartYAxisSetting));
				});

				Task.Factory.StartNew(() => SetStaticChart(subset));
				Task.Factory.StartNew(() => SetStutteringChart(subset));
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
				Task.Factory.StartNew(() =>
				{
					FrametimeGraphDataContext
					   .SetFrametimeChart(_localRecordDataServer?
					   .GetFrametimePointTimeWindow());
					var xAxis = FrametimeGraphDataContext
						.FrametimeModel.Axes.FirstOrDefault(axis => axis.Key == "xAxis");

					if (xAxis != null)
						xAxis.Reset();
				});
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
				Task.Factory.StartNew(() => SetFpsThresholdChart(subset));
			}
		}

		private void UpdateSecondaryCharts()
		{
			if (SelectedChartItem == null)
				return;

			var headerName = SelectedChartItem.Header.ToString();
			var subset = GetFrametimesSubset();

			if (subset == null)
				return;

			if (headerName == "L-shape")
			{
				Task.Factory.StartNew(() => SetLShapeChart(subset));
			}
			else if (headerName == "FPS")
			{
				Task.Factory.StartNew(() =>
					FpsGraphDataContext.SetFpsChart(_localRecordDataServer?.GetFpsPointTimeWindow()));
			}
		}

		private IList<double> GetFrametimesSubset()
			=> _localRecordDataServer?.GetFrametimeTimeWindow();

		private void SetStaticChart(IList<double> frametimes)
		{
			if (frametimes == null || !frametimes.Any())
				return;

			double GeMetricValue(IList<double> sequence, EMetric metric) =>
				_frametimeStatisticProvider.GetFpsMetricValue(sequence, metric);

			var max = GeMetricValue(frametimes, EMetric.Max);
			var p99_quantile = GeMetricValue(frametimes, EMetric.P99);
			var p95_quantile = GeMetricValue(frametimes, EMetric.P95);
			var average = GeMetricValue(frametimes, EMetric.Average);
			var p0dot1_quantile = GeMetricValue(frametimes, EMetric.P0dot1);
			var p0dot2_quantile = GeMetricValue(frametimes, EMetric.P0dot2);
			var p1_quantile = GeMetricValue(frametimes, EMetric.P1);
			var p5_quantile = GeMetricValue(frametimes, EMetric.P5);
			var p1_averageLow = GeMetricValue(frametimes, EMetric.OnePercentLow);
			var p0dot1_averageLow = GeMetricValue(frametimes, EMetric.ZerodotOnePercentLow);
			var min = GeMetricValue(frametimes, EMetric.Min);
			var adaptiveStandardDeviation = GeMetricValue(frametimes, EMetric.AdaptiveStd);

			Application.Current.Dispatcher.Invoke(new Action(() =>
			{
				IChartValues values = new ChartValues<double>();

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
						MaxRowHeigth = 30
					}
				};

				double maxOffset = (values as IList<double>).Max() * 0.15;
				BarMaxValue = (int)((values as IList<double>).Max() + maxOffset);

				var parameterLabelList = new List<string>();

				//{ "Adaptive STDEV", "Min", "0.1% Low", "0.1%", "0.2%", "1% Low", "1%", "5%", "Average", "95%", "99%", "Max" }
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

			Application.Current.Dispatcher.BeginInvoke(new Action(() =>
			{
				StutteringStatisticCollection = new SeriesCollection
				{
					new PieSeries
					{
						Title = "Smooth time (s)",
						Values = new ChartValues<double>(){ Math.Round((1 - stutteringTimePercentage / 100) * frametimes.Skip(1).Sum() / 1000, 2) },
						DataLabels = true,
						Fill = ColorRessource.PieChartSmmoothFill,
						Foreground = Brushes.Black,
						LabelPoint = PieChartPointLabel,
						FontSize = 12
					},
					new PieSeries
					{
						Title = "Stuttering time (s)",
						Values = new ChartValues<double>(){ Math.Round(stutteringTimePercentage / 100 * frametimes.Skip(1).Sum() / 1000, 2) },
						DataLabels = true,
						Fill = ColorRessource.PieChartStutterFill,
						Foreground = Brushes.Black,
						LabelPoint = PieChartPointLabel,
						FontSize = 12
					}
				};
			}));
		}

		private void SetLShapeChart(IList<double> frametimes)
		{
			if (frametimes == null || !frametimes.Any())
				return;

			var lShapeQuantiles = _frametimeAnalyzer.GetLShapeQuantiles();
			double action(double q) => _frametimeStatisticProvider.GetPQuantileSequence(frametimes, q / 100);
			var observablePoints = lShapeQuantiles.Select(q => new ObservablePoint(q, action(q)));
			var chartValues = new ChartValues<ObservablePoint>();
			chartValues.AddRange(observablePoints);

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
							Math.Round(point.Y, 1).ToString(CultureInfo.InvariantCulture) + " ms"
					}
				};

#pragma warning disable IDE0034 // simplify "default" expression
				ResetLShapeChart.OnNext(default(Unit));
#pragma warning restore IDE0034 // simplify "default" expression
			}));
		}

		private void ResetData()
		{
			FrametimeGraphDataContext.FrametimeModel.Series.Clear();
			FrametimeGraphDataContext.FrametimeModel.InvalidatePlot(true);
			FpsGraphDataContext.FpsModel.Series.Clear();
			FpsGraphDataContext.FpsModel.InvalidatePlot(true);
			_localRecordDataServer.CurrentSession = null;
			LShapeCollection?.Clear();
			StatisticCollection?.Clear();
			StutteringStatisticCollection?.Clear();
			SystemInfos?.Clear();
		}

		private void SetFrametimeChartYAxisSetting(Tuple<double, double> setting)
		{
			var yAxis = FrametimeGraphDataContext
				.FrametimeModel.Axes.FirstOrDefault(axis => axis.Key == "yAxis");

			if (yAxis != null)
			{
				yAxis.Reset();
				yAxis.Minimum = setting.Item1;
				yAxis.Maximum = setting.Item2;
				FrametimeGraphDataContext.FrametimeModel.InvalidatePlot(true);
			}
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

		public void OnRangeSliderDragCompleted()
		{
			DemandUpdateCharts();
		}

		public void OnNavigatedTo(NavigationContext navigationContext)
		{
			_useUpdateSession = true;
			try
			{
				UpdateAnalysisPage();
			}
			catch
			{ return; }
		}

		public bool IsNavigationTarget(NavigationContext navigationContext)
		{
			return true;
		}

		public void OnNavigatedFrom(NavigationContext navigationContext)
		{
			_useUpdateSession = false;
		}
	}
}
