using CapFrameX.Contracts.Configuration;
using CapFrameX.EventAggregation.Messages;
using CapFrameX.Extensions;
using CapFrameX.OcatInterface;
using CapFrameX.Statistics;
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
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace CapFrameX.ViewModel
{
	public class DataViewModel : BindableBase, INavigationAware
	{
		private const int SCALE_RESOLUTION = 200;
		private readonly IStatisticProvider _frametimeStatisticProvider;
		private readonly IFrametimeAnalyzer _frametimeAnalyzer;
		private readonly IEventAggregator _eventAggregator;
		private readonly IAppConfiguration _appConfiguration;

		private bool _useUpdateSession = true;
		private Session _session;
		private OcatRecordInfo _recordInfo;
		private ZoomingOptions _zoomingMode;
		private SeriesCollection _seriesCollection;
		private SeriesCollection _statisticCollection;
		private SeriesCollection _lShapeCollection;
		private SeriesCollection _advancedStatisticCollection;
		private string[] _parameterLabels;
		private string[] _lShapeLabels;
		private string[] _advancedParameterLabels;
		private int _selectWindowSize;
		private int _firstNFrames;
		private int _lastNFrames;
		private bool _removeOutliers;
		private bool _useSlidingWindow = false;
		private string _selectedChartLengthValue;
		private double _frametimeSliderMaximum;
		private double _frametimeSliderValue;
		private List<SystemInfo> _systemInfos;
		private bool _isCuttingModeActive;
		private int _cutLeftSliderMaximum;
		private int _cutRightSliderMaximum;
		private string _cutGraphNumberSamples;
		private bool _doUpdateCharts = true;

		/// <summary>
		/// https://docs.microsoft.com/en-us/dotnet/standard/base-types/standard-numeric-format-strings
		/// </summary>
		public Func<double, string> ParameterFormatter { get; } = value => value.ToString("F0");

		public Func<double, string> AdvancedParameterFormatter { get; } = value => value.ToString("N");

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

		public SeriesCollection SeriesCollection
		{
			get { return _seriesCollection; }
			set
			{
				_seriesCollection = value;
				RaisePropertyChanged();
			}
		}

		public SeriesCollection AdvancedStatisticCollection
		{
			get { return _advancedStatisticCollection; }
			set
			{
				_advancedStatisticCollection = value;
				RaisePropertyChanged();
			}
		}

		public ZoomingOptions ZoomingMode
		{
			get { return _zoomingMode; }
			set
			{
				_zoomingMode = value;
				RaisePropertyChanged();
			}
		}

		public int FirstNFrames
		{
			get { return _firstNFrames; }
			set
			{
				_firstNFrames = value;
				RaisePropertyChanged();
				UpdateCharts();
			}
		}

		public int LastNFrames
		{
			get { return _lastNFrames; }
			set
			{
				_lastNFrames = value;
				RaisePropertyChanged();
				UpdateCharts();
			}
		}

		public int CutLeftSliderMaximum
		{
			get { return _cutLeftSliderMaximum; }
			set { _cutLeftSliderMaximum = value; RaisePropertyChanged(); }
		}

		public int CutRightSliderMaximum
		{
			get { return _cutRightSliderMaximum; }
			set { _cutRightSliderMaximum = value; RaisePropertyChanged(); }
		}

		public bool RemoveOutliers
		{
			get { return _removeOutliers; }
			set
			{
				_removeOutliers = value;
				RaisePropertyChanged();
				UpdateCharts();
			}
		}

		public int SelectWindowSize
		{
			get { return _selectWindowSize; }
			set
			{
				_selectWindowSize = value;
				RaisePropertyChanged();
				UpdateCharts();
			}
		}

		public string SelectedChartLengthValue
		{
			get { return _selectedChartLengthValue; }
			set
			{
				_selectedChartLengthValue = value;
				RaisePropertyChanged();
				OnSelectedChartLengthValueChanged();
			}
		}

		public double FrametimeSliderMaximum
		{
			get { return _frametimeSliderMaximum; }
			set
			{
				_frametimeSliderMaximum = value;
				RaisePropertyChanged();
			}
		}

		public double FrametimeSliderValue
		{
			get { return _frametimeSliderValue; }
			set
			{
				_frametimeSliderValue = value;
				RaisePropertyChanged();
				OnFrametimeSliderValueChanged();
			}
		}

		public bool UseSlidingWindow
		{
			get { return _useSlidingWindow; }
			set
			{
				_useSlidingWindow = value;
				RaisePropertyChanged();
				OnSelectedChartLengthValueChanged();
				OnFrametimeSliderValueChanged();
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

		public string CutGraphNumberSamples
		{
			get { return _cutGraphNumberSamples; }
			set { _cutGraphNumberSamples = value; RaisePropertyChanged(); }
		}

		public IList<int> WindowSizes { get; }

		public IList<string> ChartLengthValues { get; }

		public ICommand ToogleZoomingModeCommand { get; }

		public ICommand CopyFrametimeValuesCommand { get; }

		public ICommand CopyFrametimePointsCommand { get; }

		public ICommand CopyFpsValuesCommand { get; }

		public ICommand CopyStatisticalParameterCommand { get; }

		public ICommand CopyLShapeQuantilesCommand { get; }

		public ICommand CopySystemInfoCommand { get; }

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

			ToogleZoomingModeCommand = new DelegateCommand(OnToogleZoomingMode);
			CopyFrametimeValuesCommand = new DelegateCommand(OnCopyFrametimeValues);
			CopyFrametimePointsCommand = new DelegateCommand(OnCopyFrametimePoints);
			CopyFpsValuesCommand = new DelegateCommand(OnCopyFpsValues);
			CopyStatisticalParameterCommand = new DelegateCommand(OnCopyStatisticalParameter);
			CopyLShapeQuantilesCommand = new DelegateCommand(OnCopyQuantiles);
			CopySystemInfoCommand = new DelegateCommand(OnCopySystemInfoCommand);

			ZoomingMode = ZoomingOptions.Y;
			WindowSizes = new List<int>(Enumerable.Range(4, 100 - 4));
			SelectWindowSize = _appConfiguration.MovingAverageWindowSize;
			ChartLengthValues = new List<string> { "5", "10", "20", "30", "60", "120", "180", "240", "300", "600" };
			SelectedChartLengthValue = "10";
		}

		private void OnCuttingModeChanged()
		{
			if (_session == null)
				return;

			if (IsCuttingModeActive)
			{
				UpdateCuttingParameter();
			}
			else
			{
				FirstNFrames = 0;
				LastNFrames = 0;
			}
		}

		private void UpdateCuttingParameter()
		{
			_doUpdateCharts = false;
			FirstNFrames = 0;
			LastNFrames = 0;
			_doUpdateCharts = true;

			CutLeftSliderMaximum = _session.FrameTimes.Count / 2;
			CutRightSliderMaximum = _session.FrameTimes.Count / 2;
			CutGraphNumberSamples = _session.FrameTimes.Count.ToString();
		}

		private void OnCopyFrametimeValues()
		{
			if (_session == null)
				return;

			var frametimes = GetFrametimesSubset();
			StringBuilder builder = new StringBuilder();

			foreach (var frametime in frametimes)
			{
				builder.Append(frametime + Environment.NewLine);
			}

			Clipboard.SetDataObject(builder.ToString(), false);
		}

		private void OnCopyFrametimePoints()
		{
			if (_session == null)
				return;

			var frametimes = GetFrametimesSubset();
			StringBuilder builder = new StringBuilder();

			for (int i = 0; i < frametimes.Count; i++)
			{
				builder.Append(_session.FrameStart[i] + "\t" + frametimes[i] + Environment.NewLine);
			}

			Clipboard.SetDataObject(builder.ToString(), false);
		}

		private void OnCopyFpsValues()
		{
			if (_session == null)
				return;

			var frametimes = GetFrametimesSubset();
			StringBuilder builder = new StringBuilder();

			foreach (var frametime in frametimes)
			{
				builder.Append(Math.Round(1000 / frametime, 0) + Environment.NewLine);
			}

			Clipboard.SetDataObject(builder.ToString(), false);
		}

		private void OnCopyStatisticalParameter()
		{
			if (_session == null)
				return;

			var frametimes = GetFrametimesSubset();
			var fps = frametimes.Select(ft => 1000 / ft).ToList();
			var max = Math.Round(fps.Max(), 0);
			var p99_quantile = Math.Round(_frametimeStatisticProvider.GetPQuantileSequence(fps, 0.99), 0);
			var p95_quantile = Math.Round(_frametimeStatisticProvider.GetPQuantileSequence(fps, 0.95), 0);
			var average = Math.Round(frametimes.Count * 1000 / frametimes.Sum(), 0);
			var p0dot1_quantile = Math.Round(_frametimeStatisticProvider.GetPQuantileSequence(fps, 0.001), 0);
			var p1_quantile = Math.Round(_frametimeStatisticProvider.GetPQuantileSequence(fps, 0.01), 0);
			var p5_quantile = Math.Round(_frametimeStatisticProvider.GetPQuantileSequence(fps, 0.05), 0);
			var min = Math.Round(fps.Min(), 0);
			var adaptiveStandardDeviation = Math.Round(_frametimeStatisticProvider.GetAdaptiveStandardDeviation(fps, SelectWindowSize), 0);

			StringBuilder builder = new StringBuilder();

			// Vice versa!
			// "Adaptive STD" ,"Min" ,"0,1%" ,"1%" ,"5%" ,"Average" ,"95%" ,"99%" ,"Max"
			builder.Append("Max" + "\t" + max + Environment.NewLine);
			builder.Append("99%" + "\t" + p99_quantile + Environment.NewLine);
			builder.Append("95%" + "\t" + p95_quantile + Environment.NewLine);
			builder.Append("Average" + "\t" + average + Environment.NewLine);
			builder.Append("5%" + "\t" + p5_quantile + Environment.NewLine);
			builder.Append("1%" + "\t" + p1_quantile + Environment.NewLine);
			builder.Append("0,1%" + "\t" + p0dot1_quantile + Environment.NewLine);
			builder.Append("Min" + "\t" + min + Environment.NewLine);
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

		private void OnToogleZoomingMode()
		{
			switch (ZoomingMode)
			{
				case ZoomingOptions.None:
					ZoomingMode = ZoomingOptions.X;
					break;
				case ZoomingOptions.X:
					ZoomingMode = ZoomingOptions.Y;
					break;
				case ZoomingOptions.Y:
					ZoomingMode = ZoomingOptions.Xy;
					break;
				case ZoomingOptions.Xy:
					ZoomingMode = ZoomingOptions.None;
					break;
				default:
					throw new ArgumentOutOfRangeException();
			}
		}

		private void SubscribeToUpdateSession()
		{
			_eventAggregator.GetEvent<PubSubEvent<ViewMessages.UpdateSession>>()
							.Subscribe(msg =>
							{
								if (_useUpdateSession)
								{
									_session = msg.OcatSession;
									_recordInfo = msg.RecordInfo;
									SystemInfos = RecordManager.GetSystemInfos(msg.OcatSession);

									// Do update actions
									UpdateCuttingParameter();
									UpdateCharts();
								}
							});
		}

		private IList<double> GetFrametimes()
		{
			IList<double> frametimes = new List<double>();

			if (_session?.FrameTimes == null || !_session.FrameTimes.Any())
				return frametimes;

			if (RemoveOutliers)
			{
				if (UseSlidingWindow)
				{
					if (Double.TryParse(SelectedChartLengthValue, NumberStyles.Any, CultureInfo.InvariantCulture, out double length))
					{
						double startTime = (_session.LastFrameTime - length) * FrametimeSliderValue / SCALE_RESOLUTION;
						double endTime = startTime + length;
						var frametimeSampleWindow = _session.GetFrametimeSamplesWindow(startTime, endTime);

						// ToDo: Make method selectable
						frametimes = _frametimeStatisticProvider?.GetOutlierAdjustedSequence(frametimeSampleWindow,
							ERemoveOutlierMethod.DeciPercentile);
					}
				}
				else
				{
					// ToDo: Make method selectable
					frametimes = _frametimeStatisticProvider?.GetOutlierAdjustedSequence(_session.FrameTimes,
						ERemoveOutlierMethod.DeciPercentile);
				}
			}
			else
			{
				if (UseSlidingWindow)
				{
					if (Double.TryParse(SelectedChartLengthValue, NumberStyles.Any, CultureInfo.InvariantCulture, out double length))
					{
						double startTime = (_session.LastFrameTime - length) * FrametimeSliderValue / SCALE_RESOLUTION;
						double endTime = startTime + length;
						frametimes = _session.GetFrametimeSamplesWindow(startTime, endTime);
					}
				}
				else
				{
					frametimes = _session?.FrameTimes;
				}
			}

			return frametimes;
		}

		private void UpdateCharts()
		{
			if (!_doUpdateCharts)
				return;

			var subset = GetFrametimesSubset();
			CutGraphNumberSamples = subset.Count.ToString();

			if (subset != null)
			{
				SetFrametimeChart(subset);
				SetStaticChart(subset);
				SetAdvancedStaticChart(subset);
				SetLShapeChart(subset);
			}
		}

		private void OnSelectedChartLengthValueChanged()
		{
			if (UseSlidingWindow)
			{
				if (Double.TryParse(SelectedChartLengthValue, NumberStyles.Any, CultureInfo.InvariantCulture, out double length))
				{
					// Slider parameter
					FrametimeSliderMaximum = length < _session.LastFrameTime ? SCALE_RESOLUTION : 0;
					FrametimeSliderValue = 0;
				}
			}
			else
			{
				FrametimeSliderMaximum = 0;
				FrametimeSliderValue = 0;
			}
		}

		private void OnFrametimeSliderValueChanged()
		{
			var subset = GetFrametimesSubset();

			if (subset != null)
			{
				SetFrametimeChart(subset);
			}
		}

		private List<double> GetFrametimesSubset()
		{
			var subset = new List<double>();
			var frametimes = GetFrametimes();

			if (frametimes != null && frametimes.Any())
			{
				for (int i = FirstNFrames; i < frametimes.Count - LastNFrames; i++)
				{
					subset.Add(frametimes[i]);
				}
			}

			return subset;
		}

		private void SetFrametimeChart(IList<double> frametimes)
		{
			//var gradientBrush = new LinearGradientBrush
			//{
			//	StartPoint = new Point(0, 0),
			//	EndPoint = new Point(0, 1)
			//};

			// ToDo: Get color from ressources
			//gradientBrush.GradientStops.Add(new GradientStop(Color.FromRgb(139, 35, 35), 0));
			//gradientBrush.GradientStops.Add(new GradientStop(Colors.Transparent, 1));

			var frametimeValues = new GearedValues<double>();
			frametimeValues.AddRange(frametimes);
			frametimeValues.WithQuality(_appConfiguration.ChartQualityLevel.ConverToEnum<Quality>());

			var movingAverageValues = new GearedValues<double>();
			movingAverageValues.AddRange(_frametimeStatisticProvider.GetMovingAverage(frametimes, SelectWindowSize));
			movingAverageValues.WithQuality(_appConfiguration.ChartQualityLevel.ConverToEnum<Quality>());

			SeriesCollection = new SeriesCollection()
			{
				new GLineSeries
				{
					Title = "Frametimes",
					Values = frametimeValues,
					Fill = Brushes.Transparent,
					Stroke = new SolidColorBrush(Color.FromRgb(139,35,35)),
					StrokeThickness = 1,
					LineSmoothness= 0,
					PointGeometrySize = 0
				},
				new GLineSeries
				{
					Title = string.Format("Moving average (window size = {0})", _appConfiguration.MovingAverageWindowSize),
					Values = movingAverageValues,
					Fill = Brushes.Transparent,
					Stroke = new SolidColorBrush(Color.FromRgb(35, 139, 123)),
					StrokeThickness = 1,
					LineSmoothness= 0,
					PointGeometrySize = 0
				}
			};
		}

		private void SetStaticChart(IList<double> frametimes)
		{
			if (frametimes == null || !frametimes.Any())
				return;

			var fps = frametimes.Select(ft => 1000 / ft).ToList();
			var p99_quantile = Math.Round(_frametimeStatisticProvider.GetPQuantileSequence(fps, 0.99), 0);
			var p95_quantile = Math.Round(_frametimeStatisticProvider.GetPQuantileSequence(fps, 0.95), 0);
			var max = Math.Round(fps.Max(), 0);
			var average = Math.Round(frametimes.Count * 1000 / frametimes.Sum(), 0);
			var p0dot1_quantile = Math.Round(_frametimeStatisticProvider.GetPQuantileSequence(fps, 0.001), 0);
			var p1_quantile = Math.Round(_frametimeStatisticProvider.GetPQuantileSequence(fps, 0.01), 0);
			var p5_quantile = Math.Round(_frametimeStatisticProvider.GetPQuantileSequence(fps, 0.05), 0);
			var min = Math.Round(fps.Min(), 0);
			var adaptiveStandardDeviation = Math.Round(_frametimeStatisticProvider.GetAdaptiveStandardDeviation(fps, SelectWindowSize), 0);

			IChartValues values = new ChartValues<double>
			{
				adaptiveStandardDeviation, min, p0dot1_quantile, p1_quantile, p5_quantile, average, p95_quantile, p99_quantile, max
			};

			StatisticCollection = new SeriesCollection
			{
				new RowSeries
				{
					Title = _recordInfo.GameName,
					Fill = new SolidColorBrush(Color.FromRgb(83,104,114)),
					Values = values,
					DataLabels = true
				}
			};

			ParameterLabels = new[] { "Adaptive STD", "Min", "0,1%", "1%", "5%", "Average", "95%", "99%", "Max" };
		}

		private void SetAdvancedStaticChart(IList<double> frametimes)
		{
			if (frametimes == null || !frametimes.Any())
				return;

			var stutteringPercentage = _frametimeStatisticProvider.GetStutteringPercentage(frametimes, _appConfiguration.StutteringFactor);
			IChartValues values = new ChartValues<double> { stutteringPercentage };

			AdvancedStatisticCollection = new SeriesCollection
			{
				new RowSeries
				{
					Title = _recordInfo.GameName,
					Fill = new SolidColorBrush(Color.FromRgb(83,104,114)),
					Values = values,
					DataLabels = true
				}
			};

			AdvancedParameterLabels = new[] { "Stuttering %" };
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
					LabelPoint = point => point.X + "%, " + Math.Round(point.Y, 1).ToString(CultureInfo.InvariantCulture) + " ms"
				}
			};

			// LShapeLabels = lShapeQuantiles.Select(q => q.ToString(CultureInfo.InvariantCulture)).ToArray();
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

			//Reset data
			SeriesCollection?.Clear();
			LShapeCollection?.Clear();
			StatisticCollection?.Clear();
			AdvancedStatisticCollection?.Clear();
			SystemInfos?.Clear();

			// Reset Slider
			_doUpdateCharts = false;
			FirstNFrames = 0;
			LastNFrames = 0;
			_doUpdateCharts = true;
		}
	}
}
