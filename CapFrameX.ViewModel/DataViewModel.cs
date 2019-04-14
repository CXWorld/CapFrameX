using CapFrameX.Contracts.Configuration;
using CapFrameX.EventAggregation.Messages;
using CapFrameX.OcatInterface;
using CapFrameX.Statistics;
using CapFrameX.ViewModel.DataContext;
using LiveCharts;
using LiveCharts.Defaults;
using LiveCharts.Geared;
using LiveCharts.Wpf;
using Prism.Commands;
using Prism.Events;
using Prism.Mvvm;
using Prism.Regions;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
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
		private bool _useUpdateSession = true;
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

		public IAppConfiguration AppConfiguration { get; }

		public OcatRecordInfo RecordInfo { get; private set; }

		public FrametimeGraphDataContext FrametimeGraphDataContext { get; }

		public FpsGraphDataContext FpsGraphDataContext { get; }

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

		public ICommand CopyFpsValuesCommand { get; }

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
			AppConfiguration = appConfiguration;

			SubscribeToUpdateSession();

			CopyFpsValuesCommand = new DelegateCommand(OnCopyFpsValues);
			CopyStatisticalParameterCommand = new DelegateCommand(OnCopyStatisticalParameter);
			CopyLShapeQuantilesCommand = new DelegateCommand(OnCopyQuantiles);
			CopySystemInfoCommand = new DelegateCommand(OnCopySystemInfoCommand);
			AcceptParameterSettingsCommand = new DelegateCommand(OnAcceptParameterSettings);

			ParameterFormatter = value => value.ToString(string.Format("F{0}",
				AppConfiguration.FpsValuesRoundingDigits), CultureInfo.InvariantCulture);
			_localRecordDataServer = new LocalRecordDataServer();
			FrametimeGraphDataContext = new FrametimeGraphDataContext(_localRecordDataServer,
				AppConfiguration, _frametimeStatisticProvider);
			FpsGraphDataContext = new FpsGraphDataContext(_localRecordDataServer,
				AppConfiguration, _frametimeStatisticProvider);

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

		private void OnCopyFpsValues()
		{
			if (_session == null)
				return;

			var frametimes = GetFrametimesSubset();
			StringBuilder builder = new StringBuilder();

			foreach (var frametime in frametimes)
			{
				builder.Append(Math.Round(1000 / frametime, AppConfiguration.FpsValuesRoundingDigits) + Environment.NewLine);
			}

			Clipboard.SetDataObject(builder.ToString(), false);
		}

		private void OnCopyStatisticalParameter()
		{
			if (_session == null)
				return;

			var frametimes = GetFrametimesSubset();
			var roundingDigits = AppConfiguration.FpsValuesRoundingDigits;
			var fps = frametimes.Select(ft => 1000 / ft).ToList();
			var max = Math.Round(fps.Max(), roundingDigits);
			var p99_quantile = Math.Round(_frametimeStatisticProvider.GetPQuantileSequence(fps, 0.99), roundingDigits);
			var p95_quantile = Math.Round(_frametimeStatisticProvider.GetPQuantileSequence(fps, 0.95), roundingDigits);
			var average = Math.Round(frametimes.Count * 1000 / frametimes.Sum(), roundingDigits);
			var p0dot1_quantile = Math.Round(_frametimeStatisticProvider.GetPQuantileSequence(fps, 0.001), roundingDigits);
			var p1_quantile = Math.Round(_frametimeStatisticProvider.GetPQuantileSequence(fps, 0.01), roundingDigits);
			var p5_quantile = Math.Round(_frametimeStatisticProvider.GetPQuantileSequence(fps, 0.05), roundingDigits);
			var p1_averageLow = Math.Round(1000 / _frametimeStatisticProvider.GetPAverageHighSequence(frametimes, 1 - 0.01), roundingDigits);
			var p0dot1_averageLow = Math.Round(1000 / _frametimeStatisticProvider.GetPAverageHighSequence(frametimes, 1 - 0.001), roundingDigits);
			var min = Math.Round(fps.Min(), roundingDigits);
			var adaptiveStandardDeviation = Math.Round(_frametimeStatisticProvider.GetAdaptiveStandardDeviation(fps, AppConfiguration.MovingAverageWindowSize), roundingDigits);

			StringBuilder builder = new StringBuilder();

			// Vice versa!
			// "Adaptive STD" ,"Min","0.1% Low" ,"0.1%" ,"1% Low", "1%" ,"5%" ,"Average" ,"95%" ,"99%" ,"Max"
			if(AppConfiguration.UseSingleRecordMaxStatisticParameter)
				builder.Append("Max" + "\t" + max + Environment.NewLine);
			if (AppConfiguration.UseSingleRecord99QuantileStatisticParameter)
				builder.Append("P99" + "\t" + p99_quantile + Environment.NewLine);
			if (AppConfiguration.UseSingleRecordP95QuantileStatisticParameter)
				builder.Append("P95" + "\t" + p95_quantile + Environment.NewLine);
			if (AppConfiguration.UseSingleRecordAverageStatisticParameter)
				builder.Append("Average" + "\t" + average + Environment.NewLine);
			if (AppConfiguration.UseSingleRecordP5QuantileStatisticParameter)
				builder.Append("P5" + "\t" + p5_quantile + Environment.NewLine);
			if (AppConfiguration.UseSingleRecordP1QuantileStatisticParameter)
				builder.Append("P1" + "\t" + p1_quantile + Environment.NewLine);
			if (AppConfiguration.UseSingleRecordP1LowAverageStatisticParameter)
				builder.Append("1% Low" + "\t" + p1_averageLow + Environment.NewLine);
			if (AppConfiguration.UseSingleRecordP0Dot1QuantileStatisticParameter)
				builder.Append("P0.1" + "\t" + p0dot1_quantile + Environment.NewLine);
			if (AppConfiguration.UseSingleRecordP0Dot1LowAverageStatisticParameter)
				builder.Append("0.1% Low" + "\t" + p0dot1_averageLow + Environment.NewLine);
			if (AppConfiguration.UseSingleRecordMinStatisticParameter)
				builder.Append("Min" + "\t" + min + Environment.NewLine);
			if (AppConfiguration.UseSingleRecordAdaptiveSTDStatisticParameter)
				builder.Append("Adaptive STD" + "\t" + adaptiveStandardDeviation + Environment.NewLine);

			Clipboard.SetDataObject(builder.ToString(), false);
		}

		private void OnCopyQuantiles()
		{
			if (_session == null)
				return;

			var lShapeQuantiles = _frametimeAnalyzer.GetLShapeQuantiles();
			var frametimes = GetFrametimesSubset();
			double action(double q) => Math.Round(_frametimeStatisticProvider.GetPQuantileSequence(frametimes, q / 100), 2);

			StringBuilder builder = new StringBuilder();

			foreach (var quantile in lShapeQuantiles)
			{
				builder.Append(quantile + "%" + "\t" + action(quantile) + Environment.NewLine);
			}

			Clipboard.SetDataObject(builder.ToString(), false);
		}

		private void OnCopySystemInfoCommand()
		{
			if (_session == null)
				return;

			var systemInfos = RecordManager.GetSystemInfos(_session);

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
			_localRecordDataServer.RemoveOutlierMethod = RemoveOutliers ? ERemoveOutlierMethod.DeciPercentile : ERemoveOutlierMethod.None;
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

									if (_session != null)
									{
										SystemInfos = RecordManager.GetSystemInfos(_session);

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
				Task.Factory.StartNew(() => FrametimeGraphDataContext.SetFrametimeChart(subset));
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
					FpsGraphDataContext.SetFpsChart(_localRecordDataServer.GetFpsSampleWindow()));
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

			var roundingDigits = AppConfiguration.FpsValuesRoundingDigits;
			var fps = frametimes.Select(ft => 1000 / ft).ToList();
			var p99_quantile = Math.Round(_frametimeStatisticProvider.GetPQuantileSequence(fps, 0.99), roundingDigits);
			var p95_quantile = Math.Round(_frametimeStatisticProvider.GetPQuantileSequence(fps, 0.95), roundingDigits);
			var max = Math.Round(fps.Max(), roundingDigits);
			var average = Math.Round(frametimes.Count * 1000 / frametimes.Sum(), roundingDigits);
			var p0dot1_quantile = Math.Round(_frametimeStatisticProvider.GetPQuantileSequence(fps, 0.001), roundingDigits);
			var p1_quantile = Math.Round(_frametimeStatisticProvider.GetPQuantileSequence(fps, 0.01), roundingDigits);
			var p5_quantile = Math.Round(_frametimeStatisticProvider.GetPQuantileSequence(fps, 0.05), roundingDigits);
			var p1_averageLow = Math.Round(1000 / _frametimeStatisticProvider.GetPAverageHighSequence(frametimes, 1 - 0.01), roundingDigits);
			var p0dot1_averageLow = Math.Round(1000 / _frametimeStatisticProvider.GetPAverageHighSequence(frametimes, 1 - 0.001), roundingDigits);
			var min = Math.Round(fps.Min(), roundingDigits);
			var adaptiveStandardDeviation = Math.Round(_frametimeStatisticProvider.GetAdaptiveStandardDeviation(fps, AppConfiguration.MovingAverageWindowSize), roundingDigits);

			IChartValues values = new ChartValues<double>();

			if (AppConfiguration.UseSingleRecordAdaptiveSTDStatisticParameter)
				values.Add(adaptiveStandardDeviation);
			if (AppConfiguration.UseSingleRecordMinStatisticParameter)
				values.Add(min);
			if (AppConfiguration.UseSingleRecordP0Dot1LowAverageStatisticParameter)
				values.Add(p0dot1_averageLow);
			if (AppConfiguration.UseSingleRecordP0Dot1QuantileStatisticParameter)
				values.Add(p0dot1_quantile);
			if (AppConfiguration.UseSingleRecordP1LowAverageStatisticParameter)
				values.Add(p1_averageLow);
			if (AppConfiguration.UseSingleRecordP1QuantileStatisticParameter)
				values.Add(p1_quantile);
			if (AppConfiguration.UseSingleRecordP5QuantileStatisticParameter)
				values.Add(p5_quantile);
			if (AppConfiguration.UseSingleRecordAverageStatisticParameter)
				values.Add(average);
			if (AppConfiguration.UseSingleRecordP95QuantileStatisticParameter)
				values.Add(p95_quantile);
			if (AppConfiguration.UseSingleRecord99QuantileStatisticParameter)
				values.Add(p99_quantile);
			if (AppConfiguration.UseSingleRecordMaxStatisticParameter)
				values.Add(max);

			Application.Current.Dispatcher.BeginInvoke(new Action(() =>
			{
				StatisticCollection = new SeriesCollection
				{
					new RowSeries
					{
						Title = RecordInfo.GameName,
						Fill = new SolidColorBrush(Color.FromRgb(83,104,114)),
						Values = values,
						DataLabels = true,
						FontSize = 11,
					}
				};

				var parameterLabelList = new List<string>();

				//{ "Adaptive STD", "Min", "0.1% Low", "0.1%", "1% Low", "1%", "5%", "Average", "95%", "99%", "Max" }
				if (AppConfiguration.UseSingleRecordAdaptiveSTDStatisticParameter)
					parameterLabelList.Add("Adaptive STD");
				if (AppConfiguration.UseSingleRecordMinStatisticParameter)
					parameterLabelList.Add("Min");
				if (AppConfiguration.UseSingleRecordP0Dot1LowAverageStatisticParameter)
					parameterLabelList.Add("0.1% Low");
				if (AppConfiguration.UseSingleRecordP0Dot1QuantileStatisticParameter)
					parameterLabelList.Add("P0.1");
				if (AppConfiguration.UseSingleRecordP1LowAverageStatisticParameter)
					parameterLabelList.Add("1% Low");
				if (AppConfiguration.UseSingleRecordP1QuantileStatisticParameter)
					parameterLabelList.Add("P1");
				if (AppConfiguration.UseSingleRecordP5QuantileStatisticParameter)
					parameterLabelList.Add("P5");
				if (AppConfiguration.UseSingleRecordAverageStatisticParameter)
					parameterLabelList.Add("Average");
				if (AppConfiguration.UseSingleRecordP95QuantileStatisticParameter)
					parameterLabelList.Add("P95");
				if (AppConfiguration.UseSingleRecord99QuantileStatisticParameter)
					parameterLabelList.Add("P99");
				if (AppConfiguration.UseSingleRecordMaxStatisticParameter)
					parameterLabelList.Add("Max");

				ParameterLabels = parameterLabelList.ToArray();
			}));
		}

		private void SetAdvancedStaticChart(IList<double> frametimes)
		{
			if (frametimes == null || !frametimes.Any())
				return;

			var stutteringTimePercentage = _frametimeStatisticProvider.GetStutteringTimePercentage(frametimes, AppConfiguration.StutteringFactor);

			Application.Current.Dispatcher.BeginInvoke(new Action(() =>
			{
				StutteringStatisticCollection = new SeriesCollection
				{
					new PieSeries
					{
						Title = "Smooth time (s)",
						Values = new ChartValues<double>(){ Math.Round((1 - stutteringTimePercentage / 100) * frametimes.Sum(), 0)/1000 },
						DataLabels = true,
						Foreground = Brushes.Black,
						//LabelPosition = PieLabelPosition.InsideSlice,
						LabelPoint = PieChartPointLabel,
						FontSize = 12,
					},
					new PieSeries
					{
						Title = "Stuttering time (s)",
						Values = new ChartValues<double>(){ Math.Round(stutteringTimePercentage / 100 * frametimes.Sum()) / 1000 },
						DataLabels = true,
						Foreground = Brushes.Black,
						//LabelPosition = PieLabelPosition.InsideSlice,
						LabelPoint = PieChartPointLabel,
						FontSize = 12,
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
					new GLineSeries
					{
						Values = chartValues,
						Stroke = new SolidColorBrush(Color.FromRgb(139,35,35)),
						Fill = Brushes.Transparent,
						StrokeThickness = 1,
						LineSmoothness= 1,
						PointGeometrySize = 10,
						PointGeometry = DefaultGeometries.Triangle,
						DataLabels = true,
						LabelPoint = point => point.X.ToString(CultureInfo.InvariantCulture) + "%, " +
							Math.Round(point.Y, 1).ToString(CultureInfo.InvariantCulture) + " ms"
					}
				};
			}));
		}

		private void ResetData()
		{
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
