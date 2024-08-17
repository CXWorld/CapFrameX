using CapFrameX.Contracts.Configuration;
using CapFrameX.Contracts.Data;
using CapFrameX.EventAggregation.Messages;
using CapFrameX.Extensions;
using CapFrameX.OcatInterface;
using CapFrameX.Statistics;
using GongSolutions.Wpf.DragDrop;
using LiveCharts;
using LiveCharts.Defaults;
using LiveCharts.Wpf;
using OxyPlot;
using OxyPlot.Axes;
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
				// kind of yellow
				new SolidColorBrush(Color.FromRgb(139, 123, 35)),
				// kind of pink
				new SolidColorBrush(Color.FromRgb(139, 35, 102)),
				// kind of brown
				new SolidColorBrush(Color.FromRgb(139, 71, 35)),
				// kind of dark red
                new SolidColorBrush(Color.FromRgb(89, 22, 32))
			};

		private readonly IStatisticProvider _frametimeStatisticProvider;
		private readonly IFrametimeAnalyzer _frametimeAnalyzer;
		private readonly IEventAggregator _eventAggregator;
		private readonly IAppConfiguration _appConfiguration;

		private EComparisonContext _comparisonContext = EComparisonContext.DateTime;
		private EComparisonNumericMode _comparisonNumericMode = EComparisonNumericMode.Absolute;
		private bool _initialIconVisibility = true;
		private PlotModel _comparisonModel;
		private SeriesCollection _comparisonColumnChartSeriesCollection;
		private string[] _comparisonColumnChartLabels;
		private SeriesCollection _comparisonLShapeCollection;
		private string _comparisonItemControlHeight = "300";
		private string _columnChartYAxisTitle = "FPS";
		private HashSet<SolidColorBrush> _freeColors = new HashSet<SolidColorBrush>(_comparisonBrushes);
		private bool _useEventMessages;
		private string _remainingRecordingTime;
		private double _cutLeftSliderMaximum;
		private double _cutRightSliderMaximum;
		private double _firstSeconds;
		private double _lastSeconds;
		private bool _isCuttingModeActive;
		private bool _isContextLegendActive = true;
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

		public Func<double, string> ComparisonColumnChartFormatter
		{
			get { return _comparisonColumnChartFormatter; }
			set
			{
				_comparisonColumnChartFormatter = value;
				RaisePropertyChanged();
			}
		}

		public PlotModel ComparisonModel
		{
			get { return _comparisonModel; }
			set
			{
				_comparisonModel = value;
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

		public string ColumnChartYAxisTitle
		{
			get { return _columnChartYAxisTitle; }
			set
			{
				_columnChartYAxisTitle = value;
				RaisePropertyChanged();
			}
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

		public ICommand DateTimeContextCommand { get; }

		public ICommand CpuContextCommand { get; }

		public ICommand GpuContextCommand { get; }

		public ICommand CustomContextCommand { get; }

		public ICommand RemoveAllComparisonsCommand { get; }

		public ICommand AbsoluteModeCommand { get; }

		public ICommand RelativeModeCommand { get; }

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

			DateTimeContextCommand = new DelegateCommand(OnDateTimeContext);
			CpuContextCommand = new DelegateCommand(OnCpuContext);
			GpuContextCommand = new DelegateCommand(OnGpuContex);
			CustomContextCommand = new DelegateCommand(OnCustomContex);
			RemoveAllComparisonsCommand = new DelegateCommand(OnRemoveAllComparisons);
			AbsoluteModeCommand = new DelegateCommand(OnAbsoluteMode);
			RelativeModeCommand = new DelegateCommand(OnRelativeMode);

			ComparisonColumnChartFormatter = value => value.ToString(string.Format("F{0}",
				_appConfiguration.FpsValuesRoundingDigits), CultureInfo.InvariantCulture);
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
					Title = "P1",
					Values = new ChartValues<double>(),
                    // Kind of red
                    Fill = _comparisonBrushes[2],
					DataLabels = true
				},

                //0.1% quantile
                new LiveCharts.Wpf.ColumnSeries
				{
					Title = "P0.1",
					Values = new ChartValues<double>(),
                    // Kind of dark red
                    Fill = _comparisonBrushes[3],
					DataLabels = true
				}
			};

			InitializePlotModel();
			SubscribeToSelectRecord();
		}

		private void InitializePlotModel()
		{
			ComparisonModel = new PlotModel
			{
				PlotMargins = new OxyThickness(40, 10, 0, 40),
				PlotAreaBorderColor = OxyColor.FromArgb(64, 204, 204, 204),
				LegendPosition = LegendPosition.TopCenter,
				LegendOrientation = LegendOrientation.Horizontal
			};

			//Axes
			//X
			ComparisonModel.Axes.Add(new LinearAxis()
			{
				Key = "xAxis",
				Position = OxyPlot.Axes.AxisPosition.Bottom,
				Title = "Recording time [s]",
				MajorGridlineStyle = LineStyle.Solid,
				MajorGridlineThickness = 1,
				MajorGridlineColor = OxyColor.FromArgb(64, 204, 204, 204),
				MinorTickSize = 0,
				MajorTickSize = 0
			});

			//Y
			ComparisonModel.Axes.Add(new LinearAxis()
			{
				Key = "yAxis",
				Position = OxyPlot.Axes.AxisPosition.Left,
				Title = "Frametime [ms]",
				MajorGridlineStyle = LineStyle.Solid,
				MajorGridlineThickness = 1,
				MajorGridlineColor = OxyColor.FromArgb(64, 204, 204, 204),
				MinorTickSize = 0,
				MajorTickSize = 0
			});
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


		private void OnShowContextLegendChanged()
		{
			if (!ComparisonRecords.Any())
				return;

			if (!IsContextLegendActive)
			{
				ComparisonModel.Series.ForEach(series => series.Title = null);
			}
			else
			{
				switch (_comparisonContext)
				{
					case EComparisonContext.DateTime:
						if (ComparisonModel.Series.Count == ComparisonRecords.Count)
						{
							for (int i = 0; i < ComparisonRecords.Count; i++)
							{
								ComparisonModel.Series[i].Title = GetLabelDateTimeContext(ComparisonRecords[i]);
							}
						}
						break;
					case EComparisonContext.CPU:
						if (ComparisonModel.Series.Count == ComparisonRecords.Count)
						{
							for (int i = 0; i < ComparisonRecords.Count; i++)
							{
								ComparisonModel.Series[i].Title = GetLabelCpuContext(ComparisonRecords[i]);
							}
						}
						break;
					case EComparisonContext.GPU:
						if (ComparisonModel.Series.Count == ComparisonRecords.Count)
						{
							for (int i = 0; i < ComparisonRecords.Count; i++)
							{
								ComparisonModel.Series[i].Title = GetLabelGpuContext(ComparisonRecords[i]);
							}
						}
						break;
					case EComparisonContext.Custom:
						if (ComparisonModel.Series.Count == ComparisonRecords.Count)
						{
							for (int i = 0; i < ComparisonRecords.Count; i++)
							{
								ComparisonModel.Series[i].Title = GetLabelCustomContext(ComparisonRecords[i]);
							}
						}
						break;
					default:
						if (ComparisonModel.Series.Count == ComparisonRecords.Count)
						{
							for (int i = 0; i < ComparisonRecords.Count; i++)
							{
								ComparisonModel.Series[i].Title = GetLabelDateTimeContext(ComparisonRecords[i]);
							}
						}
						break;
				}
			}

			ComparisonModel.InvalidatePlot(false);
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

			CutLeftSliderMaximum = minRecordingTime / 2 - 0.5;
			CutRightSliderMaximum = minRecordingTime / 2 + _maxRecordingTime - minRecordingTime - 0.5;
			RemainingRecordingTime = Math.Round(_maxRecordingTime, 2).ToString(CultureInfo.InvariantCulture) + " s";
		}

		private void UpdateAxesMinMax(bool invalidatePlot)
		{
			if (ComparisonRecords == null || !ComparisonRecords.Any())
				return;

			var xAxis = ComparisonModel.GetAxisOrDefault("xAxis", null);
			var yAxis = ComparisonModel.GetAxisOrDefault("yAxis", null);

			if (xAxis == null || yAxis == null)
				return;

			double xMin = 0;
			double xMax = 0;
			double yMin = 0;
			double yMax = 0;


			if (!IsCuttingModeActive)
			{
				xMin = ComparisonRecords.Min(record => record.WrappedRecordInfo.Session.FrameStart.First());
				xMax = ComparisonRecords.Max(record => record.WrappedRecordInfo.Session.FrameStart.Last());

				yMin = ComparisonRecords.Min(record => record.WrappedRecordInfo.Session.FrameTimes.Min());
				yMax = ComparisonRecords.Max(record => record.WrappedRecordInfo.Session.FrameTimes.Max());
			}
			else
			{
				double startTime = FirstSeconds;
				double endTime = _maxRecordingTime - LastSeconds;

				var sessionParallelQuery = ComparisonRecords.Select(record => record.WrappedRecordInfo.Session).AsParallel();

				xMin = sessionParallelQuery.Min(session => 
				{
					return session.GetFrametimePointsTimeWindow(startTime, endTime).First().X;
				});

				xMax = sessionParallelQuery.Max(session =>
				{
					return session.GetFrametimePointsTimeWindow(startTime, endTime).Last().X;
				});

				yMin = sessionParallelQuery.Min(session =>
				{
					return session.GetFrametimePointsTimeWindow(startTime, endTime).Min(pnt => pnt.Y); ;
				});

				yMax = sessionParallelQuery.Max(session =>
				{
					return session.GetFrametimePointsTimeWindow(startTime, endTime).Max(pnt => pnt.Y);
				});
			}

			xAxis.Minimum = xMin;
			xAxis.Maximum = xMax;

			yAxis.Minimum = yMin - (yMax - yMin) / 6;
			yAxis.Maximum = yMax + (yMax - yMin) / 6;

			if (invalidatePlot)
				ComparisonModel.InvalidatePlot(true);
		}

		private void OnCustomContex()
		{
			_comparisonContext = EComparisonContext.Custom;
			SetLabelCustomContext();
			ComparisonModel.InvalidatePlot(true);
		}

		private void OnGpuContex()
		{
			_comparisonContext = EComparisonContext.GPU;
			SetLabelGpuContext();
			ComparisonModel.InvalidatePlot(true);
		}

		private void OnCpuContext()
		{
			_comparisonContext = EComparisonContext.CPU;
			SetLabelCpuContext();
			ComparisonModel.InvalidatePlot(true);
		}

		private void OnDateTimeContext()
		{
			_comparisonContext = EComparisonContext.DateTime;
			SetLabelDateTimeContext();
			ComparisonModel.InvalidatePlot(true);
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

		private void OnAbsoluteMode()
		{
			_comparisonNumericMode = EComparisonNumericMode.Absolute;
			SetColumnChart();
			ColumnChartYAxisTitle = "FPS";
		}

		private void OnRelativeMode()
		{
			_comparisonNumericMode = EComparisonNumericMode.Relative;
			SetColumnChart();
			ColumnChartYAxisTitle = "%";
		}

		private void SetLabelDateTimeContext()
		{
			ComparisonColumnChartLabels = ComparisonRecords.Select(record =>
			{
				return GetLabelDateTimeContext(record);
			}).ToArray();

			if (ComparisonModel.Series.Count == ComparisonRecords.Count)
			{
				for (int i = 0; i < ComparisonRecords.Count; i++)
				{
					ComparisonModel.Series[i].Title = GetLabelDateTimeContext(ComparisonRecords[i]);
				}
			}
		}

		private string GetLabelDateTimeContext(ComparisonRecordInfoWrapper record)
		{
			int gameNameLength = record.WrappedRecordInfo.Game.Length;
			int dateTimeLength = record.WrappedRecordInfo.DateTime.Length;

			int maxAlignment = gameNameLength < dateTimeLength ? dateTimeLength : gameNameLength;

			var alignmentFormat = "{0," + maxAlignment.ToString() + "}";
			var gameName = string.Format(CultureInfo.InvariantCulture, alignmentFormat, record.WrappedRecordInfo.Game);
			var dateTime = string.Format(CultureInfo.InvariantCulture, alignmentFormat, record.WrappedRecordInfo.DateTime);
			return gameName + Environment.NewLine + dateTime;
		}

		private void SetLabelCpuContext()
		{
			ComparisonColumnChartLabels = ComparisonRecords.Select(record =>
			{
				return GetLabelCpuContext(record);
			}).ToArray();

			if (ComparisonModel.Series.Count == ComparisonRecords.Count)
			{
				for (int i = 0; i < ComparisonRecords.Count; i++)
				{
					ComparisonModel.Series[i].Title = GetLabelCpuContext(ComparisonRecords[i]);
				}
			}
		}

		private string GetLabelCpuContext(ComparisonRecordInfoWrapper record)
		{
			var processorName = record.WrappedRecordInfo.FileRecordInfo.ProcessorName ?? "";

			int gameNameLength = record.WrappedRecordInfo.Game.Length;
			int cpuInfoLength = processorName.Length;

			int maxAlignment = gameNameLength < cpuInfoLength ? cpuInfoLength : gameNameLength;
			var alignmentFormat = "{0," + maxAlignment.ToString() + "}";
			var gameName = string.Format(CultureInfo.InvariantCulture, alignmentFormat, record.WrappedRecordInfo.Game);
			var cpuInfo = string.Format(CultureInfo.InvariantCulture, alignmentFormat, processorName);

			return gameName + Environment.NewLine + cpuInfo;
		}

		private void SetLabelGpuContext()
		{
			ComparisonColumnChartLabels = ComparisonRecords.Select(record =>
			{
				return GetLabelGpuContext(record);
			}).ToArray();

			if (ComparisonModel.Series.Count == ComparisonRecords.Count)
			{
				for (int i = 0; i < ComparisonRecords.Count; i++)
				{
					ComparisonModel.Series[i].Title = GetLabelGpuContext(ComparisonRecords[i]);
				}
			}
		}

		private string GetLabelGpuContext(ComparisonRecordInfoWrapper record)
		{
			var graphicCardName = record.WrappedRecordInfo.FileRecordInfo.GraphicCardName ?? "";

			int gameNameLength = record.WrappedRecordInfo.Game.Length;
			int gpuInfoLength = graphicCardName.Length;

			int maxAlignment = gameNameLength < gpuInfoLength ? gpuInfoLength : gameNameLength;
			var alignmentFormat = "{0," + maxAlignment.ToString() + "}";
			var gameName = string.Format(CultureInfo.InvariantCulture, alignmentFormat, record.WrappedRecordInfo.Game);
			var gpuInfo = string.Format(CultureInfo.InvariantCulture, alignmentFormat, graphicCardName);
			return gameName + Environment.NewLine + gpuInfo;
		}

		private void SetLabelCustomContext()
		{
			ComparisonColumnChartLabels = ComparisonRecords.Select(record =>
			{
				return GetLabelCustomContext(record);
			}).ToArray();

			if (ComparisonModel.Series.Count == ComparisonRecords.Count)
			{
				for (int i = 0; i < ComparisonRecords.Count; i++)
				{
					ComparisonModel.Series[i].Title = GetLabelCustomContext(ComparisonRecords[i]);
				}
			}
		}

		private string GetLabelCustomContext(ComparisonRecordInfoWrapper record)
		{
			var comment = record.WrappedRecordInfo.FileRecordInfo.Comment ?? "";

			int gameNameLength = record.WrappedRecordInfo.Game.Length;
			int commentLength = comment.Length;

			int maxAlignment = gameNameLength < commentLength ? commentLength : gameNameLength;
			var alignmentFormat = "{0," + maxAlignment.ToString() + "}";
			var gameName = string.Format(CultureInfo.InvariantCulture, alignmentFormat, record.WrappedRecordInfo.Game);
			var gpuInfo = string.Format(CultureInfo.InvariantCulture, alignmentFormat, comment);
			return gameName + Environment.NewLine + gpuInfo;
		}

		private ComparisonRecordInfo GetComparisonRecordInfoFromFileRecordInfo(IFileRecordInfo fileRecordInfo)
		{
			string infoText = string.Empty;
			var session = RecordManager.LoadData(fileRecordInfo.FullPath);

			if (session != null)
			{
				var newLine = Environment.NewLine;
				infoText += "creation date: " + fileRecordInfo.FileInfo.LastWriteTime.ToShortDateString() + newLine +
							"creation time: " + fileRecordInfo.FileInfo.LastWriteTime.ToString("HH:mm:ss") + newLine +
							"capture time: " + Math.Round(session.LastFrameTime, 2).ToString(CultureInfo.InvariantCulture) + " s" + newLine +
							"number of samples: " + session.FrameTimes.Count.ToString();
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

		private void UpdateCharts()
		{
			if (!_doUpdateCharts)
				return;

			ComparisonModel.Series.Clear();
			ComparisonLShapeCollection.Clear();

			SetFrametimeChart();
			Task.Factory.StartNew(() => SetLShapeChart());
			SetColumnChart();
		}

		private void AddToCharts(ComparisonRecordInfoWrapper wrappedComparisonInfo)
		{
			AddToFrameTimeChart(wrappedComparisonInfo);
			AddToColumnCharts(wrappedComparisonInfo);
			AddToLShapeChart(wrappedComparisonInfo);

			UpdateAxesMinMax(true);
		}

		private void AddToColumnCharts(ComparisonRecordInfoWrapper wrappedComparisonInfo)
		{
			double startTime = FirstSeconds;
			double endTime = _maxRecordingTime - LastSeconds;
			var frametimeTimeWindow = wrappedComparisonInfo.WrappedRecordInfo.Session.GetFrametimeTimeWindow(startTime, endTime, ERemoveOutlierMethod.None);

			var roundingDigits = _appConfiguration.FpsValuesRoundingDigits;
			var fps = frametimeTimeWindow.Select(ft => 1000 / ft).ToList();
			var average = Math.Round(frametimeTimeWindow.Count * 1000 / frametimeTimeWindow.Sum(), roundingDigits);
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
				case EComparisonContext.Custom:
					SetLabelCustomContext();
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
			var frametimePoints = session.GetFrametimePointsTimeWindow(startTime, endTime)
										 .Select(pnt => new Point(pnt.X, pnt.Y));

			var chartTitle = string.Empty;

			switch (_comparisonContext)
			{
				case EComparisonContext.DateTime:
					chartTitle = GetLabelDateTimeContext(wrappedComparisonInfo);
					break;
				case EComparisonContext.CPU:
					chartTitle = GetLabelCpuContext(wrappedComparisonInfo);
					break;
				case EComparisonContext.GPU:
					chartTitle = GetLabelGpuContext(wrappedComparisonInfo);
					break;
				case EComparisonContext.Custom:
					chartTitle = GetLabelCustomContext(wrappedComparisonInfo);
					break;
				default:
					chartTitle = GetLabelDateTimeContext(wrappedComparisonInfo);
					break;
			}

			var color = wrappedComparisonInfo.FrametimeGraphColor.Value;
			var frametimeSeries = new OxyPlot.Series.LineSeries { Title = chartTitle, StrokeThickness = 1, Color = OxyColor.FromRgb(color.R, color.G, color.B) };
			frametimeSeries.Points.AddRange(frametimePoints.Select(pnt => new DataPoint(pnt.X, pnt.Y)));

			ComparisonModel.Series.Add(frametimeSeries);
		}

		private void AddToLShapeChart(ComparisonRecordInfoWrapper wrappedComparisonInfo)
		{
			double startTime = FirstSeconds;
			double endTime = _maxRecordingTime - LastSeconds;
			var frametimeTimeWindow = wrappedComparisonInfo.WrappedRecordInfo.Session.GetFrametimeTimeWindow(startTime, endTime, ERemoveOutlierMethod.None);

			var lShapeQuantiles = _frametimeAnalyzer.GetLShapeQuantiles();
			double action(double q) => _frametimeStatisticProvider.GetPQuantileSequence(frametimeTimeWindow, q / 100);
			var quantiles = lShapeQuantiles.Select(q => new ObservablePoint(q, action(q)));
			var quantileValues = new ChartValues<ObservablePoint>();
			quantileValues.AddRange(quantiles);

			Application.Current.Dispatcher.BeginInvoke(new Action(() =>
			{
				ComparisonLShapeCollection.Add(
				new LineSeries
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

			// 1% quantile
			ComparisonColumnChartSeriesCollection[1].Values.Clear();

			// 0.1% quantile
			ComparisonColumnChartSeriesCollection[2].Values.Clear();

			for (int i = 0; i < ComparisonRecords.Count; i++)
			{
				AddToColumnCharts(ComparisonRecords[i]);
			}

			if (_comparisonNumericMode == EComparisonNumericMode.Relative)
			{
				// Average
				var averages = new List<double>(ComparisonColumnChartSeriesCollection[0].Values as IList<double>);

				// 1% quantile
				var p1_quantiles = new List<double>(ComparisonColumnChartSeriesCollection[1].Values as IList<double>);

				// 0.1% quantile
				var p0dot1_quantiles = new List<double>(ComparisonColumnChartSeriesCollection[2].Values as IList<double>);

				if (averages.Any() && p1_quantiles.Any() && p0dot1_quantiles.Any())
				{
					ComparisonColumnChartSeriesCollection[0].Values.Clear();
					ComparisonColumnChartSeriesCollection[1].Values.Clear();
					ComparisonColumnChartSeriesCollection[2].Values.Clear();

					var maxAverage = averages.Max();
					var maxP1_quantile = p1_quantiles.Max();
					var maxP0dot1_quantiles = p0dot1_quantiles.Max();

					var averagesPercent = averages.Select(x => 100d * x / maxAverage).ToList();
					var p1_quantilesPercent = p1_quantiles.Select(x => 100d * x / maxP1_quantile).ToList();
					var p0dot1_quantilesPercent = p0dot1_quantiles.Select(x => 100d * x / maxP0dot1_quantiles).ToList();

					averagesPercent.ForEach(x => ComparisonColumnChartSeriesCollection[0].Values.Add(x));
					p1_quantilesPercent.ForEach(x => ComparisonColumnChartSeriesCollection[1].Values.Add(x));
					p0dot1_quantilesPercent.ForEach(x => ComparisonColumnChartSeriesCollection[2].Values.Add(x));
				}
			}
		}

		private void SetFrametimeChart()
		{
			for (int i = 0; i < ComparisonRecords.Count; i++)
			{
				AddToFrameTimeChart(ComparisonRecords[i]);
			}

			UpdateAxesMinMax(true);
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
						if (dropInfo.Data is IFileRecordInfo recordInfo)
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
							_freeColors.Add(wrappedComparisonRecordInfo.Color);
							ComparisonRecords.Remove(wrappedComparisonRecordInfo);
							UpdateIndicesAfterRemove(ComparisonRecords);
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

		private void AddComparisonRecord(IFileRecordInfo recordInfo)
		{
			if (ComparisonRecords.Count < _comparisonBrushes.Count())
			{
				var comparisonRecordInfo = GetComparisonRecordInfoFromFileRecordInfo(recordInfo);
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
				ComparisonModel, ComparisonLShapeCollection);

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
		}

		public void OnNavigatedTo(NavigationContext navigationContext)
		{
			_useEventMessages = true;
		}
	}
}
