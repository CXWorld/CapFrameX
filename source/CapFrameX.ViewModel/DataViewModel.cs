using CapFrameX.Contracts.Configuration;
using CapFrameX.Contracts.Data;
using CapFrameX.EventAggregation.Messages;
using CapFrameX.OcatInterface;
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

namespace CapFrameX.ViewModel
{
	public partial class DataViewModel : BindableBase, INavigationAware
	{
		private readonly IStatisticProvider _frametimeStatisticProvider;
		private readonly IFrametimeAnalyzer _frametimeAnalyzer;
		private readonly IEventAggregator _eventAggregator;
		private readonly IAppConfiguration _appConfiguration;

		private bool _useUpdateSession;
		private Session _session;
		private SeriesCollection _statisticCollection;
		private SeriesCollection _lShapeCollection;
		private SeriesCollection _stutteringStatisticCollection;
		private string[] _parameterLabels;
		private string[] _lShapeLabels;
		private string[] _advancedParameterLabels;
		private bool _removeOutliers;
		private List<SystemInfo> _systemInfos;
		private bool _isCuttingModeActive;
		private bool _doUpdateCharts = true;
		private Func<double, string> _parameterFormatter;
		private TabItem _selectedChartItem;
		private IRecordDataServer _localRecordDataServer;
		private IDisposable _frametimeWindowObservable;
		private string _currentGameName;

		public IFileRecordInfo RecordInfo { get; private set; }

		public FrametimeGraphDataContext FrametimeGraphDataContext { get; }

		public FpsGraphDataContext FpsGraphDataContext { get; }

		public IAppConfiguration AppConfiguration => _appConfiguration;

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

		public List<SystemInfo> SystemInfos
		{
			get { return _systemInfos; }
			set { _systemInfos = value; RaisePropertyChanged(); }
		}

		public bool IsCuttingModeActive
		{
			get { return _isCuttingModeActive; }
			set
			{
				_isCuttingModeActive = value;
				RaisePropertyChanged();
				OnCuttingModeChanged();
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

			InitializeStatisticParameter();
		}

		partial void InitializeStatisticParameter();

		private void OnAcceptParameterSettings()
		{
			Task.Factory.StartNew(() => SetStaticChart(GetFrametimesSubset()));
		}

		private void OnCuttingModeChanged()
		{
			if (_session == null)
				return;

			var tabItemHeader = SelectedChartItem.Header.ToString();

			if (string.IsNullOrWhiteSpace(tabItemHeader))
				return;

			if (tabItemHeader == "Frametimes")
			{
				FrametimeGraphDataContext.IsCuttingModeActive = IsCuttingModeActive;
			}
			else if (tabItemHeader == "FPS")
			{
				FpsGraphDataContext.IsCuttingModeActive = IsCuttingModeActive;
			}

			if (IsCuttingModeActive)
			{
				_frametimeWindowObservable = _localRecordDataServer.FrametimeDataStream.Subscribe(sequence =>
				{
					Task.Factory.StartNew(() => SetStaticChart(sequence));
					Task.Factory.StartNew(() => SetAdvancedStaticChart(sequence));
				});
			}
			else
			{
				_frametimeWindowObservable?.Dispose();
				UpdateMainCharts();
				UpdateSecondaryCharts();
			}
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
			// "Adaptive STD" ,"Min","0.1% Low" ,"0.1%","0.2%" ,"1% Low", "1%" ,"5%" ,"Average" ,"95%" ,"99%" ,"Max"
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
				builder.Append("Adaptive STD" + "\t" + adaptiveStandardDeviation.ToString(CultureInfo.InvariantCulture) + Environment.NewLine);

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
			_localRecordDataServer.RemoveOutlierMethod = RemoveOutliers ?
				ERemoveOutlierMethod.DeciPercentile : ERemoveOutlierMethod.None;
			UpdateMainCharts();
			UpdateSecondaryCharts();
		}

		private void SubscribeToUpdateSession()
		{
			_eventAggregator.GetEvent<PubSubEvent<ViewMessages.UpdateSession>>()
							.Subscribe(msg =>
							{
								if (_useUpdateSession)
								{
									_session = msg.OcatSession;
									RecordInfo = msg.RecordInfo;

									if (_session != null && RecordInfo != null)
									{
										CurrentGameName = RecordInfo.GameName;
										SystemInfos = RecordManager.GetSystemInfos(RecordInfo);

										// Do update actions
										FrametimeGraphDataContext.RecordSession = _session;
										FrametimeGraphDataContext.InitializeCuttingParameter();
										FpsGraphDataContext.RecordSession = _session;
										FpsGraphDataContext.InitializeCuttingParameter();
										UpdateMainCharts();
										UpdateSecondaryCharts();
									}
									else
									{
										ResetData();
									}
								}
							});
		}

		private void UpdateMainCharts()
		{
			if (!_doUpdateCharts)
				return;

			var subset = GetFrametimesSubset();

			if (subset != null)
			{
				Task.Factory.StartNew(() => FrametimeGraphDataContext
					.SetFrametimeChart(_localRecordDataServer?.GetFrametimePointTimeWindow()));
				Task.Factory.StartNew(() => SetStaticChart(subset));
				Task.Factory.StartNew(() => SetAdvancedStaticChart(subset));
			}
		}

		private void UpdateSecondaryCharts()
		{
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
		{
			return _localRecordDataServer?.GetFrametimeSampleWindow();
		}

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

			Application.Current.Dispatcher.BeginInvoke(new Action(() =>
			{
				StatisticCollection = new SeriesCollection
				{
					new RowSeries
					{
						Title = RecordInfo.GameName,
						Fill = ColorRessource.BarChartFill,
						Values = values,
						DataLabels = true,
						FontSize = 11
					}
				};

				var parameterLabelList = new List<string>();

				//{ "Adaptive STD", "Min", "0.1% Low", "0.1%", "0.2%", "1% Low", "1%", "5%", "Average", "95%", "99%", "Max" }
				if (_appConfiguration.UseSingleRecordAdaptiveSTDStatisticParameter && !double.IsNaN(adaptiveStandardDeviation))
					parameterLabelList.Add("Adaptive STD");
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

		private void SetAdvancedStaticChart(IList<double> frametimes)
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
						Values = new ChartValues<double>(){ Math.Round((1 - stutteringTimePercentage / 100) * frametimes.Sum(), 0)/1000 },
						DataLabels = true,
						Fill = ColorRessource.PieChartSmmoothFill,
						Foreground = Brushes.Black,
						LabelPoint = PieChartPointLabel,
						FontSize = 12
					},
					new PieSeries
					{
						Title = "Stuttering time (s)",
						Values = new ChartValues<double>(){ Math.Round(stutteringTimePercentage / 100 * frametimes.Sum()) / 1000 },
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
						StrokeThickness = 1,
						LineSmoothness= 1,
						PointGeometrySize = 5,
						PointGeometry = DefaultGeometries.Square,
						DataLabels = true,
						LabelPoint = point => point.X.ToString(CultureInfo.InvariantCulture) + "%, " +
							Math.Round(point.Y, 1).ToString(CultureInfo.InvariantCulture) + " ms"
					}
				};

				ResetLShapeChart.OnNext(default(Unit));
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

		public void OnNavigatedTo(NavigationContext navigationContext)
		{
			_useUpdateSession = true;
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
