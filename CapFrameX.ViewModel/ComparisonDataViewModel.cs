using CapFrameX.Contracts.Configuration;
using CapFrameX.EventAggregation.Messages;
using CapFrameX.Extensions;
using CapFrameX.OcatInterface;
using CapFrameX.Statistics;
using GongSolutions.Wpf.DragDrop;
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
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace CapFrameX.ViewModel
{
	public class ComparisonDataViewModel : BindableBase, INavigationAware, IDropTarget
	{
		private static readonly SolidColorBrush[] _comparisonBrushes =
			new SolidColorBrush[]
			{
				// kind of green
				new SolidColorBrush(Color.FromRgb(35, 139, 123)),
				// kind of blue
				new SolidColorBrush(Color.FromRgb(35, 50, 139)),
				// kind of red
				new SolidColorBrush(Color.FromRgb(139, 35, 50)),
                // kind of dark red
                new SolidColorBrush(Color.FromRgb(89, 22, 32)),
				// kind of yellow
				new SolidColorBrush(Color.FromRgb(139, 123, 35)),
				// kind of pink
				new SolidColorBrush(Color.FromRgb(139, 35, 102)),
				// kind of brown
				new SolidColorBrush(Color.FromRgb(139, 71, 35)),
			};

		private readonly IStatisticProvider _frametimeStatisticProvider;
		private readonly IFrametimeAnalyzer _frametimeAnalyzer;
		private readonly IEventAggregator _eventAggregator;
		private readonly IAppConfiguration _appConfiguration;

		private EComparisonContext _comparisonContext = EComparisonContext.DateTime;
		private bool _initialIconVisibility = true;
		private SeriesCollection _comparisonSeriesCollection;
		private SeriesCollection _comparisonColumnChartSeriesCollection;
		private string[] _comparisonColumnChartLabels;
		private SeriesCollection _comparisonLShapeCollection;
		private string _comparisonItemControlHeight = "300";
		private HashSet<SolidColorBrush> _freeColors = new HashSet<SolidColorBrush>(_comparisonBrushes);
		private ZoomingOptions _zoomingMode;
		private bool _useEventMessages;
		private string _remainingRecordingTime;
		private double _cutLeftSliderMaximum;
		private double _cutRightSliderMaximum;
		private double _firstSeconds;
		private double _lastSeconds;
		private bool _isCuttingModeActive;
		private double _maxRecordingTime;
		private bool _doUpdateCharts = true;
		private Func<double, string> _comparisonColumnChartFormatter;

		public bool InitialIconVisibility
		{
			get { return _initialIconVisibility; }
			set
			{
				_initialIconVisibility = value;
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

		public Func<double, string> ComparisonColumnChartFormatter
		{
			get { return _comparisonColumnChartFormatter; }
			set
			{
				_comparisonColumnChartFormatter = value;
				RaisePropertyChanged();
			}
		}

		public SeriesCollection ComparisonSeriesCollection
		{
			get { return _comparisonSeriesCollection; }
			set
			{
				_comparisonSeriesCollection = value;
				RaisePropertyChanged();
			}
		}

		public SeriesCollection ComparisonColumnChartSeriesCollection
		{
			get { return _comparisonColumnChartSeriesCollection; }
			set
			{
				_comparisonColumnChartSeriesCollection = value;
				RaisePropertyChanged();
			}
		}

		public SeriesCollection ComparisonLShapeCollection
		{
			get { return _comparisonLShapeCollection; }
			set { _comparisonLShapeCollection = value; RaisePropertyChanged(); }
		}

		public string[] ComparisonColumnChartLabels
		{
			get { return _comparisonColumnChartLabels; }
			set { _comparisonColumnChartLabels = value; RaisePropertyChanged(); }
		}

		public string ComparisonItemControlHeight
		{
			get { return _comparisonItemControlHeight; }
			set { _comparisonItemControlHeight = value; RaisePropertyChanged(); }
		}

		public string RemainingRecordingTime
		{
			get { return _remainingRecordingTime; }
			set { _remainingRecordingTime = value; RaisePropertyChanged(); }
		}

		public double CutLeftSliderMaximum
		{
			get { return _cutLeftSliderMaximum; }
			set { _cutLeftSliderMaximum = value; RaisePropertyChanged(); }
		}

		public double CutRightSliderMaximum
		{
			get { return _cutRightSliderMaximum; }
			set { _cutRightSliderMaximum = value; RaisePropertyChanged(); }
		}

		public double FirstSeconds
		{
			get { return _firstSeconds; }
			set
			{
				_firstSeconds = value;
				RaisePropertyChanged();
				UpdateCharts();
				RemainingRecordingTime = Math.Round(_maxRecordingTime - LastSeconds - _firstSeconds, 2)
					.ToString(CultureInfo.InvariantCulture) + " s";
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
				RemainingRecordingTime = Math.Round(_maxRecordingTime - _lastSeconds - FirstSeconds, 2)
					.ToString(CultureInfo.InvariantCulture) + " s";
			}
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

		public ICommand ToogleZoomingModeCommand { get; }

		public ICommand DateTimeContextCommand { get; }

		public ICommand CpuContextCommand { get; }

		public ICommand GpuContextCommand { get; }

		public ICommand CustomContextCommand { get; }

		public ICommand RemoveAllComparisonsCommand { get; }

		public ObservableCollection<ComparisonRecordInfoWrapper> ComparisonRecords { get; }
			= new ObservableCollection<ComparisonRecordInfoWrapper>();

		public ComparisonDataViewModel(IStatisticProvider frametimeStatisticProvider,
									   IFrametimeAnalyzer frametimeAnalyzer,
									   IEventAggregator eventAggregator,
									   IAppConfiguration appConfiguration)
		{
			_frametimeStatisticProvider = frametimeStatisticProvider;
			_frametimeAnalyzer = frametimeAnalyzer;
			_eventAggregator = eventAggregator;
			_appConfiguration = appConfiguration;

			ZoomingMode = ZoomingOptions.Y;
			ToogleZoomingModeCommand = new DelegateCommand(OnToogleZoomingMode);
			DateTimeContextCommand = new DelegateCommand(OnDateTimeContext);
			CpuContextCommand = new DelegateCommand(OnCpuContext);
			GpuContextCommand = new DelegateCommand(OnGpuContex);
			CustomContextCommand = new DelegateCommand(OnCustomContex);
			RemoveAllComparisonsCommand = new DelegateCommand(OnRemoveAllComparisons);

			ComparisonColumnChartFormatter = value => value.ToString(string.Format("F{0}", _appConfiguration.FpsValuesRoundingDigits));
			ComparisonSeriesCollection = new SeriesCollection();
			ComparisonLShapeCollection = new SeriesCollection();
			ComparisonColumnChartSeriesCollection = new SeriesCollection
			{

                // Add ColumnSeries per parameter
                // Average
                new ColumnSeries
				{
					Title = "Average",
					Values = new ChartValues<double>(),
                    // Kind of blue
                    Fill = _comparisonBrushes[1],
					DataLabels = true
				},

                 //1% quantile
                new ColumnSeries
				{
					Title = "1%",
					Values = new ChartValues<double>(),
                    // Kind of red
                    Fill = _comparisonBrushes[2],
					DataLabels = true
				},

                //0.1% quantile
                new ColumnSeries
				{
					Title = "0.1%",
					Values = new ChartValues<double>(),
                    // Kind of dark red
                    Fill = _comparisonBrushes[3],
					DataLabels = true
				}
			};
			ComparisonLShapeCollection = new SeriesCollection();

			SubscribeToSelectRecord();
		}

		private void OnCuttingModeChanged()
		{
			if (!ComparisonRecords.Any())
				return;

			if (IsCuttingModeActive)
			{
				UpdateCuttingParameter();
			}
			else
			{
				FirstSeconds = 0;
				LastSeconds = 0;
			}
		}

		private void UpdateCuttingParameter()
		{
			double minRecordingTime = double.MaxValue;
			_maxRecordingTime = double.MinValue;

			foreach (var record in ComparisonRecords)
			{
				if (record.WrappedRecordInfo.Session.FrameStart.Last() > _maxRecordingTime)
					_maxRecordingTime = record.WrappedRecordInfo.Session.FrameStart.Last();

				if (record.WrappedRecordInfo.Session.FrameStart.Last() < minRecordingTime)
					minRecordingTime = record.WrappedRecordInfo.Session.FrameStart.Last();
			}

			_doUpdateCharts = false;
			FirstSeconds = 0;
			LastSeconds = 0;
			_doUpdateCharts = true;

			CutLeftSliderMaximum = minRecordingTime / 2;
			CutRightSliderMaximum = minRecordingTime / 2 + _maxRecordingTime - minRecordingTime;
			RemainingRecordingTime = Math.Round(_maxRecordingTime, 2).ToString(CultureInfo.InvariantCulture) + " s";
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

		private void OnCustomContex()
		{
			_comparisonContext = EComparisonContext.Custom;
			SetLabelCustomContext();
		}

		private void OnGpuContex()
		{
			_comparisonContext = EComparisonContext.GPU;
			SetLabelGpuContext();
		}

		private void OnCpuContext()
		{
			_comparisonContext = EComparisonContext.CPU;
			SetLabelCpuContext();
		}

		private void OnDateTimeContext()
		{
			_comparisonContext = EComparisonContext.DateTime;
			SetLabelDateTimeContext();
		}

		private void OnRemoveAllComparisons()
		{
			foreach (var record in ComparisonRecords)
			{
				_freeColors.Add(record.Color);
			}

			ComparisonRecords.Clear();

			UpdateCharts();

			InitialIconVisibility = true;
			ComparisonItemControlHeight = "300";
		}

		private void SetLabelDateTimeContext()
		{
			ComparisonColumnChartLabels = ComparisonRecords.Select(record =>
			{
				int gameNameLength = record.WrappedRecordInfo.Game.Length;
				int dateTimeLength = record.WrappedRecordInfo.DateTime.Length;

				int maxAlignment = gameNameLength < dateTimeLength ? dateTimeLength : gameNameLength;

				var alignmentFormat = "{0," + maxAlignment.ToString() + "}";
				var gameName = string.Format(alignmentFormat, record.WrappedRecordInfo.Game);
				var dateTime = string.Format(alignmentFormat, record.WrappedRecordInfo.DateTime);
				return gameName + Environment.NewLine + dateTime;
			}).ToArray();
		}

		private void SetLabelCpuContext()
		{
			ComparisonColumnChartLabels = ComparisonRecords.Select(record =>
			{
				var processorName = record.WrappedRecordInfo.Session.ProcessorName ?? "-";

				int gameNameLength = record.WrappedRecordInfo.Game.Length;
				int cpuInfoLength = processorName.Length;

				int maxAlignment = gameNameLength < cpuInfoLength ? cpuInfoLength : gameNameLength;
				var alignmentFormat = "{0," + maxAlignment.ToString() + "}";
				var gameName = string.Format(alignmentFormat, record.WrappedRecordInfo.Game);
				var cpuInfo = string.Format(alignmentFormat, processorName);

				return gameName + Environment.NewLine + cpuInfo;
			}).ToArray();
		}

		private void SetLabelGpuContext()
		{
			ComparisonColumnChartLabels = ComparisonRecords.Select(record =>
			{
				var graphicCardName = record.WrappedRecordInfo.Session.GraphicCardName ?? "-";

				int gameNameLength = record.WrappedRecordInfo.Game.Length;
				int gpuInfoLength = graphicCardName.Length;

				int maxAlignment = gameNameLength < gpuInfoLength ? gpuInfoLength : gameNameLength;
				var alignmentFormat = "{0," + maxAlignment.ToString() + "}";
				var gameName = string.Format(alignmentFormat, record.WrappedRecordInfo.Game);
				var gpuInfo = string.Format(alignmentFormat, graphicCardName);
				return gameName + Environment.NewLine + gpuInfo;
			}).ToArray();
		}

		private void SetLabelCustomContext()
		{
			ComparisonColumnChartLabels = ComparisonRecords.Select(record =>
			{
				var comment = record.WrappedRecordInfo.Session.Comment ?? "-";

				int gameNameLength = record.WrappedRecordInfo.Game.Length;
				int commentLength = comment.Length;

				int maxAlignment = gameNameLength < commentLength ? commentLength : gameNameLength;
				var alignmentFormat = "{0," + maxAlignment.ToString() + "}";
				var gameName = string.Format(alignmentFormat, record.WrappedRecordInfo.Game);
				var gpuInfo = string.Format(alignmentFormat, comment);
				return gameName + Environment.NewLine + gpuInfo;
			}).ToArray();
		}

		private ComparisonRecordInfo GetComparisonRecordInfoFromOcatRecordInfo(OcatRecordInfo ocatRecordInfo)
		{
			string infoText = string.Empty;
			var session = RecordManager.LoadData(ocatRecordInfo.FullPath);

			if (session != null)
			{
				var newLine = Environment.NewLine;
				infoText += "creation date: " + ocatRecordInfo.FileInfo.LastWriteTime.ToShortDateString() + newLine +
							"creation time: " + ocatRecordInfo.FileInfo.LastWriteTime.ToString("HH:mm:ss") + newLine +
							"capture time: " + Math.Round(session.LastFrameTime, 2).ToString(CultureInfo.InvariantCulture) + " s" + newLine +
							"number of samples: " + session.FrameTimes.Count.ToString();
			}

			return new ComparisonRecordInfo
			{
				Game = ocatRecordInfo.GameName,
				InfoText = infoText,
				DateTime = ocatRecordInfo.FileInfo.LastWriteTime.ToString(),
				Session = session
			};
		}

		private void UpdateCharts()
		{
			if (!_doUpdateCharts)
				return;

			ComparisonSeriesCollection.Clear();
			ComparisonLShapeCollection.Clear();

			Task.Factory.StartNew(() => SetFrametimeChart());
			Task.Factory.StartNew(() => SetLShapeChart());
			SetColumnChart();
		}

		private void AddToCharts(ComparisonRecordInfoWrapper wrappedComparisonInfo)
		{
			AddToFrameTimeChart(wrappedComparisonInfo);
			AddToColumnCharts(wrappedComparisonInfo);
			AddToLShapeChart(wrappedComparisonInfo);
		}

		private void AddToColumnCharts(ComparisonRecordInfoWrapper wrappedComparisonInfo)
		{
			double startTime = FirstSeconds;
			double endTime = _maxRecordingTime - LastSeconds;
			var frametimeSampleWindow = wrappedComparisonInfo.WrappedRecordInfo.Session.GetFrametimeSamplesWindow(startTime, endTime);

			var roundingDigits = _appConfiguration.FpsValuesRoundingDigits;
			var fps = frametimeSampleWindow.Select(ft => 1000 / ft).ToList();
			var average = Math.Round(frametimeSampleWindow.Count * 1000 / frametimeSampleWindow.Sum(), roundingDigits);
			var p1_quantile = Math.Round(_frametimeStatisticProvider.GetPQuantileSequence(fps, 0.01), roundingDigits);
			var p0dot1_quantile = Math.Round(_frametimeStatisticProvider.GetPQuantileSequence(fps, 0.001), roundingDigits);

			// Average
			ComparisonColumnChartSeriesCollection[0].Values.Add(average);

			//1% quantile
			ComparisonColumnChartSeriesCollection[1].Values.Add(p1_quantile);

			//0.1% quantile
			ComparisonColumnChartSeriesCollection[2].Values.Add(p0dot1_quantile);

			switch (_comparisonContext)
			{
				case EComparisonContext.DateTime:
					SetLabelDateTimeContext();
					break;
				case EComparisonContext.CPU:
					SetLabelCpuContext();
					break;
				case EComparisonContext.GPU:
					SetLabelGpuContext();
					break;
				default:
					SetLabelDateTimeContext();
					break;
			}
		}

		private void AddToFrameTimeChart(ComparisonRecordInfoWrapper wrappedComparisonInfo)
		{
			double startTime = FirstSeconds;
			double endTime = _maxRecordingTime - LastSeconds;
			var session = wrappedComparisonInfo.WrappedRecordInfo.Session;
			var frametimePoints = session.GetFrametimePointsWindow(startTime, endTime)
										 .Select(pnt => new ObservablePoint(pnt.X, pnt.Y));

			var frametimeChartValues = new GearedValues<ObservablePoint>();
			frametimeChartValues.AddRange(frametimePoints);
			frametimeChartValues.WithQuality(_appConfiguration.ChartQualityLevel.ConverToEnum<Quality>());

			Application.Current.Dispatcher.Invoke(new Action(() =>
			{
				ComparisonSeriesCollection.Add(
				new GLineSeries
				{
					Values = frametimeChartValues,
					Fill = Brushes.Transparent,
					Stroke = wrappedComparisonInfo.Color,
					StrokeThickness = 1,
					LineSmoothness = 0,
					PointGeometrySize = 0
				});
			}));
		}

		private void AddToLShapeChart(ComparisonRecordInfoWrapper wrappedComparisonInfo)
		{
			double startTime = FirstSeconds;
			double endTime = _maxRecordingTime - LastSeconds;
			var frametimeSampleWindow = wrappedComparisonInfo.WrappedRecordInfo.Session.GetFrametimeSamplesWindow(startTime, endTime);

			var lShapeQuantiles = _frametimeAnalyzer.GetLShapeQuantiles();
			double action(double q) => _frametimeStatisticProvider.GetPQuantileSequence(frametimeSampleWindow, q / 100);
			var quantiles = lShapeQuantiles.Select(q => new ObservablePoint(q, action(q)));
			var quantileValues = new ChartValues<ObservablePoint>();
			quantileValues.AddRange(quantiles);

			Application.Current.Dispatcher.BeginInvoke(new Action(() =>
			{
				ComparisonLShapeCollection.Add(
				new GLineSeries
				{
					Values = quantileValues,
					Stroke = wrappedComparisonInfo.Color,
					Fill = Brushes.Transparent,
					StrokeThickness = 1,
					LineSmoothness = 1,
					PointGeometrySize = 10,
					PointGeometry = DefaultGeometries.Triangle,
				});
			}));
		}

		private void SetColumnChart()
		{
			// Average
			ComparisonColumnChartSeriesCollection[0].Values.Clear();

			//1% quantile
			ComparisonColumnChartSeriesCollection[1].Values.Clear();

			//0.1% quantile
			ComparisonColumnChartSeriesCollection[2].Values.Clear();

			for (int i = 0; i < ComparisonRecords.Count; i++)
			{
				AddToColumnCharts(ComparisonRecords[i]);
			}
		}

		private void SetFrametimeChart()
		{
			for (int i = 0; i < ComparisonRecords.Count; i++)
			{
				AddToFrameTimeChart(ComparisonRecords[i]);
			}
		}

		private void SetLShapeChart()
		{
			for (int i = 0; i < ComparisonRecords.Count; i++)
			{
				AddToLShapeChart(ComparisonRecords[i]);
			}
		}

		private void SubscribeToSelectRecord()
		{
			_eventAggregator.GetEvent<PubSubEvent<ViewMessages.SelectSession>>()
							.Subscribe(msg =>
							{
								if (_useEventMessages)
								{
									AddComparisonRecord(msg.RecordInfo);

									// Complete redraw
									if (IsCuttingModeActive)
									{
										UpdateCharts();
									}
								}
							});
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
						if (dropInfo.Data is OcatRecordInfo recordInfo)
						{
							AddComparisonRecord(recordInfo);

							// Complete redraw
							if (IsCuttingModeActive)
							{
								UpdateCharts();
							}
						}
					}
					else if (frameworkElement.Name == "RemoveRecordItemControl")
					{
						if (dropInfo.Data is ComparisonRecordInfoWrapper wrappedComparisonRecordInfo)
						{
							ComparisonRecords.Remove(wrappedComparisonRecordInfo);
							UpdateIndicesAfterRemove(ComparisonRecords);
							_freeColors.Add(wrappedComparisonRecordInfo.Color);
							InitialIconVisibility = !ComparisonRecords.Any();
							ComparisonItemControlHeight = ComparisonRecords.Any() ? "Auto" : "300";

							UpdateCuttingParameter();

							//Cleanup charts and performance parameter
							UpdateCharts();
						}
					}
				}
			}
		}

		private void UpdateIndicesAfterRemove(ObservableCollection<ComparisonRecordInfoWrapper> comparisonRecords)
		{
			for (int i = 0; i < comparisonRecords.Count; i++)
			{
				comparisonRecords[i].CollectionIndex = i;
			}
		}

		private void AddComparisonRecord(OcatRecordInfo recordInfo)
		{
			if (ComparisonRecords.Count < _comparisonBrushes.Count())
			{
				var comparisonRecordInfo = GetComparisonRecordInfoFromOcatRecordInfo(recordInfo);
				var wrappedComparisonRecordInfo = GetWrappedRecordInfo(comparisonRecordInfo);				

				//Update list and index
				ComparisonRecords.Add(wrappedComparisonRecordInfo);
				wrappedComparisonRecordInfo.CollectionIndex = ComparisonRecords.Count - 1;

				var color = _freeColors.First();
				_freeColors.Remove(color);
				wrappedComparisonRecordInfo.Color = color;
				wrappedComparisonRecordInfo.FrametimeGraphColor = color.Color;

				InitialIconVisibility = !ComparisonRecords.Any();
				ComparisonItemControlHeight = ComparisonRecords.Any() ? "Auto" : "300";

				UpdateCuttingParameter();

				//Draw charts and performance parameter
				AddToCharts(wrappedComparisonRecordInfo);
			}
		}

		private ComparisonRecordInfoWrapper GetWrappedRecordInfo(ComparisonRecordInfo comparisonRecordInfo)
		{
			var wrappedRecordInfo = new ComparisonRecordInfoWrapper(comparisonRecordInfo,
				ComparisonSeriesCollection, ComparisonLShapeCollection);

			return wrappedRecordInfo;
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

			OnRemoveAllComparisons();
		}

		public void OnNavigatedTo(NavigationContext navigationContext)
		{
			_useEventMessages = true;
		}
	}
}
