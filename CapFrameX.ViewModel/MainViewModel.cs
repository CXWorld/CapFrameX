using CapFrameX.Contracts.OcatInterface;
using CapFrameX.OcatInterface;
using Prism.Mvvm;
using System;
using System.IO;
using System.Collections.ObjectModel;
using System.Threading;
using System.Reactive.Linq;
using System.Linq;
using LiveCharts;
using System.Windows.Input;
using Prism.Commands;
using System.Windows;
using System.Windows.Media;
using System.Collections.Generic;
using LiveCharts.Geared;
using LiveCharts.Wpf;
using CapFrameX.Statistics;
using System.Globalization;
using LiveCharts.Defaults;

namespace CapFrameX.ViewModel
{
	public class MainViewModel : BindableBase
	{
		private readonly IRecordDirectoryObserver _recordObserver;
		private readonly IStatisticProvider _frametimeStatisticProvider;
		private readonly IFrametimeAnalyzer _frametimeAnalyzer;

		private OcatRecordInfo _selectedRecordInfo;
		private ZoomingOptions _zoomingMode;
		private SeriesCollection _seriesCollection;
		private SeriesCollection _statisticCollection;
		private SeriesCollection _lShapeCollection;
		private string[] _parameterLabels;
		private string[] _lShapeLabels;
		private int _selectWindowSize;
		private int _firstNFrames;
		private int _lastNFrames;
		private Session _session;
		private bool _removeOutliers;
		private bool _useAdaptiveStandardDeviation = true;

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

		public OcatRecordInfo SelectedRecordInfo
		{
			get { return _selectedRecordInfo; }
			set
			{
				_selectedRecordInfo = value;
				RaisePropertyChanged();
				OnSelectedRecordInfoChanged();
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

		public bool UseAdaptiveStandardDeviation
		{
			get { return _useAdaptiveStandardDeviation; }
			set
			{
				_useAdaptiveStandardDeviation = value;
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

		public IList<int> WindowSizes { get; }

		public ObservableCollection<OcatRecordInfo> RecordInfoList { get; }
			= new ObservableCollection<OcatRecordInfo>();

		public ICommand ToogleZoomingModeCommand { get; }

		public MainViewModel(IRecordDirectoryObserver recordObserver,
			IStatisticProvider frametimeStatisticProvider, IFrametimeAnalyzer frametimeAnalyzer)
		{
			_recordObserver = recordObserver;
			_frametimeStatisticProvider = frametimeStatisticProvider;
			_frametimeAnalyzer = frametimeAnalyzer;

			// ToDo: check wether to do this async
			var initialRecordList = _recordObserver.GetAllRecordFileInfo();

			foreach (var fileInfo in initialRecordList)
			{
				AddToRecordInfoList(fileInfo);
			}

			var context = SynchronizationContext.Current;
			_recordObserver.RecordCreatedStream.ObserveOn(context).SubscribeOn(context)
							.Subscribe(OnRecordCreated);
			_recordObserver.RecordDeletedStream.ObserveOn(context).SubscribeOn(context)
							.Subscribe(OnRecordDeleted);

			// Turn streams now on
			_recordObserver.IsActive = true;

			ToogleZoomingModeCommand = new DelegateCommand(OnToogleZoomingMode);

			ZoomingMode = ZoomingOptions.Y;
			WindowSizes = new List<int>(Enumerable.Range(4, 100 - 4));
			SelectWindowSize = 10;
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

		private void AddToRecordInfoList(FileInfo fileInfo)
		{
			var recordInfo = OcatRecordInfo.Create(fileInfo);
			if (recordInfo != null)
			{
				RecordInfoList.Add(recordInfo);
			}
		}

		private void OnRecordCreated(FileInfo fileInfo) => AddToRecordInfoList(fileInfo);

		private void OnRecordDeleted(FileInfo fileInfo)
		{
			var recordInfo = OcatRecordInfo.Create(fileInfo);
			if (recordInfo != null)
			{
				var match = RecordInfoList.FirstOrDefault(info => info.FullPath == fileInfo.FullName);

				if (match != null)
				{
					RecordInfoList.Remove(match);
				}
			}
		}

		private void OnSelectedRecordInfoChanged()
		{
			_session = RecordManager.LoadData(SelectedRecordInfo.FullPath);

			if (_session.FrameTimes != null && _session.FrameTimes.Any())
			{
				if (FirstNFrames != 0 || LastNFrames != 0)
				{
					var subset = GetFrametimesSubset();
					SetFrametimeChart(subset);
					SetStaticChart(subset);
					SetLShapeChart(subset);
				}
				else
				{
					SetFrametimeChart(GetFrametimes());
					SetStaticChart(GetFrametimes());
					SetLShapeChart(GetFrametimes());
				}
			}
		}

		private IList<double> GetFrametimes()
		{
			if (RemoveOutliers)
			{
				// ToDo: Make method selectable
				return _frametimeStatisticProvider?.GetOutlierAdjustedSequence(_session.FrameTimes, ERemoveOutlierMethod.DeciPercentile);
			}
			else
			{
				return _session?.FrameTimes;
			}
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

			if (UseAdaptiveStandardDeviation)
			{
				movingAverageValues.AddRange(_frametimeStatisticProvider.GetMovingAverage(frametimes, SelectWindowSize));
				movingAverageValues.WithQuality(Quality.High);
			}

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
			var average = Math.Round(fpsSequence.Average(), 0);
			var p1_quantile = Math.Round(_frametimeStatisticProvider.GetPQuantileSequence(fpsSequence, 0.01), 0);
			var p5_quantile = Math.Round(_frametimeStatisticProvider.GetPQuantileSequence(fpsSequence, 0.05), 0);
			var min = Math.Round(fpsSequence.Min(), 0);

			IChartValues values = null;

			if (!UseAdaptiveStandardDeviation)
				values = new ChartValues<double> { min, p1_quantile, p5_quantile, average };
			else
			{
				var adaptiveStandardDeviation = Math.Round(_frametimeStatisticProvider.GetAdaptiveStandardDeviation(fpsSequence, SelectWindowSize), 0);
				values = new ChartValues<double> { adaptiveStandardDeviation, min, p1_quantile, p5_quantile, average };
			}

			StatisticCollection = new SeriesCollection
			{
				new RowSeries
				{
					Title = SelectedRecordInfo.GameName,
					Fill = new SolidColorBrush(Color.FromRgb(83,104,114)),
					Values = values,
					DataLabels = true
				}
			};

			if (!UseAdaptiveStandardDeviation)
				ParameterLabels = new[] { "Min", "1%", "5%", "Average" };
			else
				ParameterLabels = new[] { "Adaptive STD", "Min", "1%", "5%", "Average" };

			var stutteringPercentage = _frametimeStatisticProvider.GetStutteringPercentage(fpsSequence);
		}


		private void SetLShapeChart(IList<double> frametimes)
		{
			if (frametimes == null || !frametimes.Any())
				return;

			var lShapeQuantiles = _frametimeAnalyzer.GetLShapeQuantiles();
			double action(double q) => _frametimeStatisticProvider.GetPQuantileSequence(frametimes, q / 100);
			var quantiles = lShapeQuantiles.Select(q => new ObservablePoint(q, action(q)));
			var quantileValues = new ChartValues<ObservablePoint>();
			quantileValues.AddRange(quantiles);

			LShapeCollection = new SeriesCollection()
			{
				new LineSeries
				{
					Values = quantileValues,
					Stroke = new SolidColorBrush(Color.FromRgb(139,35,35)),
					Fill = Brushes.Transparent,
					StrokeThickness = 1,
					LineSmoothness= 1,
					PointGeometrySize = 10,
					PointGeometry = DefaultGeometries.Diamond,
					DataLabels = true,
					LabelPoint = point => point.X + "%," + Math.Round(point.Y, 0) + " ms"
				}
			};

			LShapeLabels = lShapeQuantiles.Select(q => q.ToString(CultureInfo.InvariantCulture)).ToArray();
		}

		private void UpdateCharts()
		{
			var subset = GetFrametimesSubset();

			if (subset != null)
			{
				SetFrametimeChart(subset);
				SetStaticChart(subset);
				SetLShapeChart(subset);
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
	}
}
