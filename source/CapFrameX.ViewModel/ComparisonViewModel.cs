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
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace CapFrameX.ViewModel
{
	public class ComparisonViewModel : BindableBase, INavigationAware, IDropTarget
	{
		private static readonly int PART_LENGTH = 42;

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
		private SeriesCollection _comparisonRowChartSeriesCollection;
		private string[] _comparisonRowChartLabels;
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
		private double _barChartHeight;
		private bool _barChartVisibility;
		private TabItem _selectedChartItem;
		private Func<double, string> _comparisonColumnChartFormatter;
		private bool _colorPickerVisibility;

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

		public SeriesCollection ComparisonRowChartSeriesCollection
		{
			get { return _comparisonRowChartSeriesCollection; }
			set
			{
				_comparisonRowChartSeriesCollection = value;
				RaisePropertyChanged();
			}
		}

		public SeriesCollection ComparisonLShapeCollection
		{
			get { return _comparisonLShapeCollection; }
			set { _comparisonLShapeCollection = value; RaisePropertyChanged(); }
		}

		public string[] ComparisonRowChartLabels
		{
			get { return _comparisonRowChartLabels; }
			set { _comparisonRowChartLabels = value; RaisePropertyChanged(); }
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

		public double BarChartHeight
		{
			get { return _barChartHeight; }
			set
			{
				_barChartHeight = value;
				RaisePropertyChanged();
			}
		}

		public bool BarChartVisibility
		{
			get { return _barChartVisibility; }
			set
			{
				_barChartVisibility = value;
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

		public ICommand DateTimeContextCommand { get; }

		public ICommand CpuContextCommand { get; }

		public ICommand GpuContextCommand { get; }

		public ICommand CustomContextCommand { get; }

		public ICommand RemoveAllComparisonsCommand { get; }

		public ICommand AbsoluteModeCommand { get; }

		public ICommand RelativeModeCommand { get; }

		public ObservableCollection<ComparisonRecordInfoWrapper> ComparisonRecords { get; }
			= new ObservableCollection<ComparisonRecordInfoWrapper>();

		public double BarChartMaxRowHeight { get; private set; } = 20;

		public ComparisonViewModel(IStatisticProvider frametimeStatisticProvider,
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
			ComparisonRowChartSeriesCollection = new SeriesCollection
			{
                // Add ColumnSeries per parameter
                // Average
                new RowSeries
				{
					Title = "Average",
					Values = new ChartValues<double>(),
                    // Kind of blue
                    Fill = _comparisonBrushes[1],
					DataLabels = true,
					MaxRowHeigth = BarChartMaxRowHeight,
					UseRelativeMode = true
				},

                 //1% quantile
                new RowSeries
				{
					Title = "P1",
					Values = new ChartValues<double>(),
                    // Kind of red
                    Fill = _comparisonBrushes[2],
					DataLabels = true,
					MaxRowHeigth = BarChartMaxRowHeight,
					UseRelativeMode = true
				},

                ////0.1% quantile
                //new LiveCharts.Wpf.ColumnSeries
                //{
                //    Title = "P0.1",
                //    Values = new ChartValues<double>(),
                //    // Kind of dark red
                //    Fill = _comparisonBrushes[3],
                //    DataLabels = true
                //}
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
								ComparisonModel.Series[i].Title = 
									GetLabelDateTimeContext(ComparisonRecords[i], GetMaxDateTimeAlignment());
							}
						}
						break;
					case EComparisonContext.CPU:
						if (ComparisonModel.Series.Count == ComparisonRecords.Count)
						{
							for (int i = 0; i < ComparisonRecords.Count; i++)
							{
								ComparisonModel.Series[i].Title = 
									GetLabelCpuContext(ComparisonRecords[i], GetMaxCpuAlignment());
							}
						}
						break;
					case EComparisonContext.GPU:
						if (ComparisonModel.Series.Count == ComparisonRecords.Count)
						{
							for (int i = 0; i < ComparisonRecords.Count; i++)
							{
								ComparisonModel.Series[i].Title = 
									GetLabelGpuContext(ComparisonRecords[i], GetMaxGpuAlignment());
							}
						}
						break;
					case EComparisonContext.Custom:
						if (ComparisonModel.Series.Count == ComparisonRecords.Count)
						{
							for (int i = 0; i < ComparisonRecords.Count; i++)
							{
								ComparisonModel.Series[i].Title = 
									GetLabelCustomContext(ComparisonRecords[i], GetMaxCommentAlignment());
							}
						}
						break;
					default:
						if (ComparisonModel.Series.Count == ComparisonRecords.Count)
						{
							for (int i = 0; i < ComparisonRecords.Count; i++)
							{
								ComparisonModel.Series[i].Title = 
									GetLabelDateTimeContext(ComparisonRecords[i], GetMaxDateTimeAlignment());
							}
						}
						break;
				}
			}

			ComparisonModel.InvalidatePlot(false);
		}

		private void OnChartItemChanged()
			=> ColorPickerVisibility = SelectedChartItem.Header.ToString() != "Bar charts";

		private int GetMaxDateTimeAlignment()
		{
			var maxGameNameLength = ComparisonRecords.Max(record => record.WrappedRecordInfo.Game.Length);
			var maxDateTimeLength = ComparisonRecords.Max(record => record.WrappedRecordInfo.DateTime.Length);

			return Math.Max(maxGameNameLength, maxDateTimeLength);
		}

		private int GetMaxCommentAlignment()
		{
			var maxGameNameLength = ComparisonRecords.Max(record => record.WrappedRecordInfo.Game.Length);
			var maxContextLength = ComparisonRecords.Max(record
				=> record.WrappedRecordInfo.FileRecordInfo.Comment.SplitWordWise(PART_LENGTH).Max(part => part.Length));

			return Math.Max(maxGameNameLength, maxContextLength);
		}

		private int GetMaxGpuAlignment()
		{
			var maxGameNameLength = ComparisonRecords.Max(record => record.WrappedRecordInfo.Game.Length);
			var maxGpuLength = ComparisonRecords.Max(record
				=> record.WrappedRecordInfo.FileRecordInfo.GraphicCardName.SplitWordWise(PART_LENGTH).Max(part => part.Length));

			return Math.Max(maxGameNameLength, maxGpuLength);
		}

		private int GetMaxCpuAlignment()
		{
			var maxGameNameLength = ComparisonRecords.Max(record => record.WrappedRecordInfo.Game.Length);
			var maxCpuLength = ComparisonRecords.Max(record
				=> record.WrappedRecordInfo.FileRecordInfo.ProcessorName.SplitWordWise(PART_LENGTH).Max(part => part.Length));

			return Math.Max(maxGameNameLength, maxCpuLength);
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
			BarChartVisibility = false;
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
			ComparisonRowChartLabels = ComparisonRecords.Select(record =>
			{
				return GetLabelDateTimeContext(record, GetMaxDateTimeAlignment());
			}).Reverse().ToArray();

			if (ComparisonModel.Series.Count == ComparisonRecords.Count)
			{
				for (int i = 0; i < ComparisonRecords.Count; i++)
				{
					ComparisonModel.Series[i].Title = 
						GetLabelDateTimeContext(ComparisonRecords[i], GetMaxDateTimeAlignment());
				}
			}
		}

		private string GetLabelDateTimeContext(ComparisonRecordInfoWrapper record, int maxAlignment)
		{
			var alignmentFormat = "{0," + maxAlignment.ToString() + "}";
			var gameName = string.Format(CultureInfo.InvariantCulture, alignmentFormat, record.WrappedRecordInfo.Game);
			var dateTime = string.Format(CultureInfo.InvariantCulture, alignmentFormat, record.WrappedRecordInfo.DateTime);
			return gameName + Environment.NewLine + dateTime;
		}

		private void SetLabelCpuContext()
		{
			ComparisonRowChartLabels = ComparisonRecords.Select(record =>
			{
				return GetLabelCpuContext(record, GetMaxCpuAlignment());
			}).Reverse().ToArray();

			if (ComparisonModel.Series.Count == ComparisonRecords.Count)
			{
				for (int i = 0; i < ComparisonRecords.Count; i++)
				{
					ComparisonModel.Series[i].Title = GetLabelCpuContext(ComparisonRecords[i], GetMaxCpuAlignment());
				}
			}
		}

		private string GetLabelCpuContext(ComparisonRecordInfoWrapper record, int maxAlignment)
		{
			var processorName = record.WrappedRecordInfo.FileRecordInfo.ProcessorName ?? "";
			var cpuInfoParts = processorName.SplitWordWise(PART_LENGTH);
			var alignmentFormat = "{0," + maxAlignment.ToString() + "}";

			var infoPartsFormatted = string.Empty;
			foreach (var part in cpuInfoParts)
			{
				if (part == string.Empty)
					continue;

				infoPartsFormatted += Environment.NewLine + string.Format(CultureInfo.InvariantCulture, alignmentFormat, part);
			}

			var gameName = string.Format(CultureInfo.InvariantCulture, alignmentFormat, record.WrappedRecordInfo.Game);
			return gameName + infoPartsFormatted;
		}

		private void SetLabelGpuContext()
		{
			ComparisonRowChartLabels = ComparisonRecords.Select(record =>
			{
				return GetLabelGpuContext(record, GetMaxGpuAlignment());
			}).Reverse().ToArray();

			if (ComparisonModel.Series.Count == ComparisonRecords.Count)
			{
				for (int i = 0; i < ComparisonRecords.Count; i++)
				{
					ComparisonModel.Series[i].Title = GetLabelGpuContext(ComparisonRecords[i], GetMaxGpuAlignment());
				}
			}
		}

		private string GetLabelGpuContext(ComparisonRecordInfoWrapper record, int maxAlignment)
		{
			var graphicCardName = record.WrappedRecordInfo.FileRecordInfo.GraphicCardName ?? "";
			var gpuInfoParts = graphicCardName.SplitWordWise(PART_LENGTH);
			var alignmentFormat = "{0," + maxAlignment.ToString() + "}";

			var infoPartsFormatted = string.Empty;
			foreach (var part in gpuInfoParts)
			{
				if (part == string.Empty)
					continue;

				infoPartsFormatted += Environment.NewLine + string.Format(CultureInfo.InvariantCulture, alignmentFormat, part);
			}

			var gameName = string.Format(CultureInfo.InvariantCulture, alignmentFormat, record.WrappedRecordInfo.Game);
			return gameName + Environment.NewLine + infoPartsFormatted;
		}

		private void SetLabelCustomContext()
		{
			ComparisonRowChartLabels = ComparisonRecords.Select(record =>
			{
				return GetLabelCustomContext(record, GetMaxCommentAlignment());
			}).Reverse().ToArray();

			if (ComparisonModel.Series.Count == ComparisonRecords.Count)
			{
				for (int i = 0; i < ComparisonRecords.Count; i++)
				{
					ComparisonModel.Series[i].Title = GetLabelCustomContext(ComparisonRecords[i], GetMaxCommentAlignment());
				}
			}
		}

		private string GetLabelCustomContext(ComparisonRecordInfoWrapper record, int maxAlignment)
		{
			var comment = record.WrappedRecordInfo.FileRecordInfo.Comment ?? "";
			var commentParts = comment.SplitWordWise(PART_LENGTH);
			var alignmentFormat = "{0," + maxAlignment.ToString() + "}";

			var infoPartsFormatted = string.Empty;
			foreach (var part in commentParts)
			{
				if (part == string.Empty)
					continue;

				infoPartsFormatted += Environment.NewLine + string.Format(CultureInfo.InvariantCulture, alignmentFormat, part);
			}

			var gameName = string.Format(CultureInfo.InvariantCulture, alignmentFormat, record.WrappedRecordInfo.Game);
			return gameName + infoPartsFormatted;
		}

		private void ResetBarChartSeriesTitles()
		{
			for (int i = 0; i < ComparisonRecords.Count; i++)
			{
				ComparisonModel.Series[i].Title = string.Empty;
			}

			ComparisonRowChartLabels = new string[0];
		}

		private ComparisonRecordInfo GetComparisonRecordInfoFromFileRecordInfo(IFileRecordInfo fileRecordInfo)
		{
			string infoText = string.Empty;
			var session = RecordManager.LoadData(fileRecordInfo.FullPath);

			if (session != null)
			{
				var newLine = Environment.NewLine;
				infoText += $"{fileRecordInfo.FileInfo.LastWriteTime.ToShortDateString()} { fileRecordInfo.FileInfo.LastWriteTime.ToString("HH:mm:ss")}" + newLine +
							$"{session.FrameTimes.Count} frames in {Math.Round(session.LastFrameTime, 2).ToString(CultureInfo.InvariantCulture)}s" + newLine +
							$"context type '{_comparisonContext.ToString()}'";
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

			ResetBarChartSeriesTitles();
			ComparisonModel.Series.Clear();
			// ComparisonLShapeCollection.Clear();

			SetFrametimeChart();
			//Task.Factory.StartNew(() => SetLShapeChart());
			SetColumnChart();
		}

		private void AddToCharts(ComparisonRecordInfoWrapper wrappedComparisonInfo)
		{
			AddToFrameTimeChart(wrappedComparisonInfo);
			AddToColumnCharts(wrappedComparisonInfo);
			// AddToLShapeChart(wrappedComparisonInfo);

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
			ComparisonRowChartSeriesCollection[0].Values.Insert(0, average);

			//1% quantile
			ComparisonRowChartSeriesCollection[1].Values.Insert(0, p1_quantile);

			//0.1% quantile
			//ComparisonColumnChartSeriesCollection[2].Values.Add(p0dot1_quantile);

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
					chartTitle = GetLabelDateTimeContext(wrappedComparisonInfo, GetMaxDateTimeAlignment());
					break;
				case EComparisonContext.CPU:
					chartTitle = GetLabelCpuContext(wrappedComparisonInfo, GetMaxCpuAlignment());
					break;
				case EComparisonContext.GPU:
					chartTitle = GetLabelGpuContext(wrappedComparisonInfo, GetMaxGpuAlignment());
					break;
				case EComparisonContext.Custom:
					chartTitle = GetLabelCustomContext(wrappedComparisonInfo, GetMaxCommentAlignment());
					break;
				default:
					chartTitle = GetLabelDateTimeContext(wrappedComparisonInfo, GetMaxDateTimeAlignment());
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
					PointGeometry = DefaultGeometries.Triangle
				});
			}));
		}

		private void SetColumnChart()
		{
			// Average
			ComparisonRowChartSeriesCollection[0].Values.Clear();

			// 1% quantile
			ComparisonRowChartSeriesCollection[1].Values.Clear();

			//// 0.1% quantile
			//ComparisonRowChartSeriesCollection[2].Values.Clear();

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

		public void RemoveComparisonItem(ComparisonRecordInfoWrapper wrappedComparisonRecordInfo)
		{
			_freeColors.Add(wrappedComparisonRecordInfo.Color);
			ComparisonRecords.Remove(wrappedComparisonRecordInfo);
			UpdateIndicesAfterRemove(ComparisonRecords);
			UpdateCuttingParameter();
			UpdateCharts();

			// Do with delay
			var context = TaskScheduler.FromCurrentSynchronizationContext();
			Task.Run(async () =>
			{
				await SetTaskDelay().ContinueWith(_ =>
				{
					Application.Current.Dispatcher.Invoke(new Action(() =>
					{
						BarChartHeight = 40 + (2 * BarChartMaxRowHeight + 12) * ComparisonRecords.Count;
					}));
				}, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, context);
			});
		}

		private async Task SetTaskDelay()
		{
			await Task.Delay(TimeSpan.FromMilliseconds(500));
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
				BarChartVisibility = ComparisonRecords.Any();
				ComparisonItemControlHeight = ComparisonRecords.Any() ? "Auto" : "300";

				//ToDo: Update height of bar chart control here
				BarChartHeight = 40 + (2 * BarChartMaxRowHeight + 12) * ComparisonRecords.Count;

				UpdateCuttingParameter();

				//Draw charts and performance parameter
				AddToCharts(wrappedComparisonRecordInfo);
			}
		}

		private ComparisonRecordInfoWrapper GetWrappedRecordInfo(ComparisonRecordInfo comparisonRecordInfo)
			=> new ComparisonRecordInfoWrapper(comparisonRecordInfo, this);

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

						if (dropInfo.Data is ComparisonRecordInfoWrapper wrappedRecordInfo)
						{
							// manage sorting
							int currentIndex = ComparisonRecords.IndexOf(wrappedRecordInfo);

							if (dropInfo.InsertIndex < ComparisonRecords.Count)
							{
								var currentWrappedRecordInfo = wrappedRecordInfo;
								ComparisonRecords.Remove(currentWrappedRecordInfo);
								ComparisonRecords.Insert(dropInfo.InsertIndex, currentWrappedRecordInfo);

								foreach (var rowSeries in ComparisonRowChartSeriesCollection)
								{
									var currentChartValue = rowSeries.Values[currentIndex];
									rowSeries.Values.RemoveAt(currentIndex);
									rowSeries.Values.Insert(dropInfo.InsertIndex, currentChartValue);
								}

								var currentLabel = ComparisonRowChartLabels[currentIndex];
								var labelList = ComparisonRowChartLabels.ToList();
								labelList.RemoveAt(currentIndex);
								labelList.Insert(dropInfo.InsertIndex, currentLabel);
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
