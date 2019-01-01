using CapFrameX.EventAggregation.Messages;
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
using System.Diagnostics;
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

		public Func<double, string> ParameterFormatter { get; set; } = value => value.ToString("N");

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
			IFrametimeAnalyzer frametimeAnalyzer, IEventAggregator eventAggregator)
		{
			_frametimeStatisticProvider = frametimeStatisticProvider;
			_frametimeAnalyzer = frametimeAnalyzer;
			_eventAggregator = eventAggregator;

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
			SelectWindowSize = 10;
			ChartLengthValues = new List<string> { "5", "10", "20", "30", "60", "120", "180", "240", "300", "600" };
			SelectedChartLengthValue = "10";
		}

		private void OnCopyFrametimeValues()
		{
			if (_session == null)
				return;

			StringBuilder builder = new StringBuilder();

			foreach (var frametime in _session.FrameTimes)
			{
				builder.Append(frametime + Environment.NewLine);
			}

			Clipboard.SetDataObject(builder.ToString(), false);
		}

		private void OnCopyFrametimePoints()
		{
			if (_session == null)
				return;

			StringBuilder builder = new StringBuilder();

			for (int i = 0; i < _session.FrameTimes.Count; i++)
			{
				builder.Append(_session.FrameStart[i] + "\t" + _session.FrameTimes[i] + Environment.NewLine);
			}

			Clipboard.SetDataObject(builder.ToString(), false);
		}

		private void OnCopyFpsValues()
		{
			if (_session == null)
				return;

			StringBuilder builder = new StringBuilder();

			foreach (var frametime in _session.FrameTimes)
			{
				builder.Append(Math.Round(1000 / frametime, 0) + Environment.NewLine);
			}

			Clipboard.SetDataObject(builder.ToString(), false);
		}

		private void OnCopyStatisticalParameter()
		{
			if (_session == null)
				return;

			var fpsSequence = _session.FrameTimes.Select(ft => 1000 / ft).ToList();
			var max = Math.Round(fpsSequence.Max(), 0);
			var average = Math.Round(fpsSequence.Average(), 0);
			var p0dot1_quantile = Math.Round(_frametimeStatisticProvider.GetPQuantileSequence(fpsSequence, 0.001), 0);
			var p1_quantile = Math.Round(_frametimeStatisticProvider.GetPQuantileSequence(fpsSequence, 0.01), 0);
			var p5_quantile = Math.Round(_frametimeStatisticProvider.GetPQuantileSequence(fpsSequence, 0.05), 0);
			var min = Math.Round(fpsSequence.Min(), 0);
			var adaptiveStandardDeviation = Math.Round(_frametimeStatisticProvider.GetAdaptiveStandardDeviation(fpsSequence, SelectWindowSize), 0);

			StringBuilder builder = new StringBuilder();

			// Vice versa!
			// "Adaptive STD", "Min", "0,1%", "1%", "5%", "Average", "Max"
			builder.Append("Max" + "\t" + max + Environment.NewLine);
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
			double action(double q) => Math.Round(_frametimeStatisticProvider.GetPQuantileSequence(_session.FrameTimes, q / 100), 2);

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

						var frametimesSubset = RecordManager.GetFrametimesWindow(_session, startTime, endTime);

						// ToDo: Make method selectable
						frametimes = _frametimeStatisticProvider?.GetOutlierAdjustedSequence(frametimesSubset,
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

						frametimes = RecordManager.GetFrametimesWindow(_session, startTime, endTime);
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
			var subset = GetFrametimesSubset();

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
			var gradientBrush = new LinearGradientBrush
			{
				StartPoint = new Point(0, 0),
				EndPoint = new Point(0, 1)
			};

			// ToDo: Get color from ressources
			gradientBrush.GradientStops.Add(new GradientStop(Color.FromRgb(139, 35, 35), 0));
			gradientBrush.GradientStops.Add(new GradientStop(Colors.Transparent, 1));

			var frametimeValues = new GearedValues<double>();
			frametimeValues.AddRange(frametimes);
			frametimeValues.WithQuality(Quality.High);

			var movingAverageValues = new GearedValues<double>();
			movingAverageValues.AddRange(_frametimeStatisticProvider.GetMovingAverage(frametimes, SelectWindowSize));
			movingAverageValues.WithQuality(Quality.High);

			SeriesCollection = new SeriesCollection()
			{
				new GLineSeries
				{
					Values = frametimeValues,
					Fill = gradientBrush,
					Stroke = new SolidColorBrush(Color.FromRgb(139,35,35)),
					StrokeThickness = 1,
					LineSmoothness= 0,
					PointGeometrySize = 0
				},
				new GLineSeries
				{
					Values = movingAverageValues,
					Fill = gradientBrush,
					Stroke = new SolidColorBrush(Color.FromRgb(35, 139, 123)),
					StrokeThickness = 1,
					LineSmoothness= 0,
					PointGeometrySize = 0
				}
			};
		}

		private void SetStaticChart(IList<double> frameTimes)
		{
			if (frameTimes == null || !frameTimes.Any())
				return;

			var fpsSequence = frameTimes.Select(ft => 1000 / ft).ToList();
			var max = Math.Round(fpsSequence.Max(), 0);
			var average = Math.Round(fpsSequence.Average(), 0);
			var p0dot1_quantile = Math.Round(_frametimeStatisticProvider.GetPQuantileSequence(fpsSequence, 0.001), 0);
			var p1_quantile = Math.Round(_frametimeStatisticProvider.GetPQuantileSequence(fpsSequence, 0.01), 0);
			var p5_quantile = Math.Round(_frametimeStatisticProvider.GetPQuantileSequence(fpsSequence, 0.05), 0);
			var min = Math.Round(fpsSequence.Min(), 0);
			var adaptiveStandardDeviation = Math.Round(_frametimeStatisticProvider.GetAdaptiveStandardDeviation(fpsSequence, SelectWindowSize), 0);

			IChartValues values = new ChartValues<double> { adaptiveStandardDeviation, min, p0dot1_quantile, p1_quantile, p5_quantile, average, max };

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

			ParameterLabels = new[] { "Adaptive STD", "Min", "0,1%", "1%", "5%", "Average", "Max" };
		}

		private void SetAdvancedStaticChart(IList<double> frameTimes)
		{
			if (frameTimes == null || !frameTimes.Any())
				return;

			var fpsSequence = frameTimes.Select(ft => 1000 / ft).ToList();
			var stutteringPercentage = _frametimeStatisticProvider.GetStutteringPercentage(frameTimes);

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
		}
	}
}
