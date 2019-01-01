using CapFrameX.EventAggregation.Messages;
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

        public ICommand ToogleZoomingModeCommand { get; }

        public ICommand DateTimeContextCommand { get; }

        public ICommand CpuContextCommand { get; }

        public ICommand GpuContextCommand { get; }

        public Func<double, string> ComparisonColumnChartFormatter { get; private set; } = value => value.ToString("N");

        public ObservableCollection<ComparisonRecordInfo> ComparisonRecords { get; }
            = new ObservableCollection<ComparisonRecordInfo>();

        public ComparisonDataViewModel(IStatisticProvider frametimeStatisticProvider, IFrametimeAnalyzer frametimeAnalyzer,
									   IEventAggregator eventAggregator)
        {
            _frametimeStatisticProvider = frametimeStatisticProvider;
            _frametimeAnalyzer = frametimeAnalyzer;
			_eventAggregator = eventAggregator;

            ZoomingMode = ZoomingOptions.Y;
            ToogleZoomingModeCommand = new DelegateCommand(OnToogleZoomingMode);
            DateTimeContextCommand = new DelegateCommand(OnDateTimeContext);
            CpuContextCommand = new DelegateCommand(OnCpuContext);
            GpuContextCommand = new DelegateCommand(OnGpuContex);

            ComparisonSeriesCollection = new SeriesCollection();
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

        private void SetLabelDateTimeContext()
        {
            ComparisonColumnChartLabels = ComparisonRecords.Select(record =>
            {
                int gameNameLength = record.Game.Length;
                int dateTimeLength = record.DateTime.Length;

                int maxAlignment = gameNameLength < dateTimeLength ? dateTimeLength : gameNameLength;

                var alignmentFormat = "{0," + maxAlignment.ToString() + "}";
                var gameName = string.Format(alignmentFormat, record.Game);
                var dateTime = string.Format(alignmentFormat, record.DateTime);
                return gameName + Environment.NewLine + dateTime;
            }).ToArray();
        }

        private void SetLabelCpuContext()
        {
            ComparisonColumnChartLabels = ComparisonRecords.Select(record =>
            {
                int gameNameLength = record.Game.Length;
                int cpuInfoLength = record.Session.ProcessorName.Trim(new Char[] { ' ', '"' }).Length;

                int maxAlignment = gameNameLength < cpuInfoLength ? cpuInfoLength : gameNameLength;
                var alignmentFormat = "{0," + maxAlignment.ToString() + "}";
                var gameName = string.Format(alignmentFormat, record.Game);
                var cpuInfo = string.Format(alignmentFormat, record.Session.ProcessorName.Trim(new Char[] { ' ', '"' }));

                return gameName + Environment.NewLine + cpuInfo;
            }).ToArray();
        }

        private void SetLabelGpuContext()
        {
            ComparisonColumnChartLabels = ComparisonRecords.Select(record =>
            {
                int gameNameLength = record.Game.Length;
                int gpuInfoLength = record.Session.GraphicCardName.Trim(new Char[] { ' ', '"' }).Length;

                int maxAlignment = gameNameLength < gpuInfoLength ? gpuInfoLength : gameNameLength;
                var alignmentFormat = "{0," + maxAlignment.ToString() + "}";
                var gameName = string.Format(alignmentFormat, record.Game);
                var gpuInfo = string.Format(alignmentFormat, record.Session.GraphicCardName.Trim(new Char[] { ' ', '"' }));
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
                infoText += "creation datetime: " + ocatRecordInfo.FileInfo.LastWriteTime.ToString() + newLine +
                            "capture time: " + Math.Round(session.LastFrameTime, 2).ToString(CultureInfo.InvariantCulture) + " sec" + newLine +
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

        private void RemoveFromCharts(ComparisonRecordInfo comparisonInfo)
        {
            SetColumnChart();
            SetFrametimeChart();
            SetLShapeChart();
        }

        private void AddToCharts(ComparisonRecordInfo comparisonInfo)
        {
            AddToFrameTimeChart(comparisonInfo);
            AddToColumnCharts(comparisonInfo);
            AddToLShapeChart(comparisonInfo);
        }

        private void AddToColumnCharts(ComparisonRecordInfo comparisonInfo)
        {
            var fps = comparisonInfo.Session.FrameTimes.Select(ft => 1000 / ft).ToList();
            var average = Math.Round(fps.Average(), 0);
            var p1_quantile = Math.Round(_frametimeStatisticProvider.GetPQuantileSequence(fps, 0.01));
            var p0dot1_quantile = Math.Round(_frametimeStatisticProvider.GetPQuantileSequence(fps, 0.001));

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

        private void AddToFrameTimeChart(ComparisonRecordInfo comparisonInfo)
        {
            var session = comparisonInfo.Session;
            var frametimePoints = session.FrameTimes.Select((val, index) => new ObservablePoint(session.FrameStart[index], val));
            var frametimeChartValues = new GearedValues<ObservablePoint>();
            frametimeChartValues.AddRange(frametimePoints);
            frametimeChartValues.WithQuality(Quality.High);

            ComparisonSeriesCollection.Add(
                new GLineSeries
                {
                    Values = frametimeChartValues,
                    Fill = Brushes.Transparent,
                    Stroke = comparisonInfo.Color,
                    StrokeThickness = 1,
                    LineSmoothness = 0,
                    PointGeometrySize = 0
                });
        }

        private void AddToLShapeChart(ComparisonRecordInfo comparisonInfo)
        {
            var lShapeQuantiles = _frametimeAnalyzer.GetLShapeQuantiles();
            double action(double q) => _frametimeStatisticProvider.GetPQuantileSequence(comparisonInfo.Session.FrameTimes, q / 100);
            var quantiles = lShapeQuantiles.Select(q => new ObservablePoint(q, action(q)));
            var quantileValues = new ChartValues<ObservablePoint>();
            quantileValues.AddRange(quantiles);

            ComparisonLShapeCollection.Add(
                new GLineSeries
                {
                    Values = quantileValues,
                    Stroke = comparisonInfo.Color,
                    Fill = Brushes.Transparent,
                    StrokeThickness = 1,
                    LineSmoothness = 1,
                    PointGeometrySize = 10,
                    PointGeometry = DefaultGeometries.Triangle,
                });
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
            ComparisonSeriesCollection = new SeriesCollection();

            for (int i = 0; i < ComparisonRecords.Count; i++)
            {
                AddToFrameTimeChart(ComparisonRecords[i]);
            }
        }

        private void SetLShapeChart()
        {
            ComparisonLShapeCollection = new SeriesCollection();

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
						}
					}
                    else if (frameworkElement.Name == "RemoveRecordItemControl")
                    {
                        if (dropInfo.Data is ComparisonRecordInfo comparisonRecordInfo)
                        {
                            ComparisonRecords.Remove(comparisonRecordInfo);
                            _freeColors.Add(comparisonRecordInfo.Color);
                            InitialIconVisibility = !ComparisonRecords.Any();
                            ComparisonItemControlHeight = ComparisonRecords.Any() ? "Auto" : "300";

                            //Cleanup charts and performance parameter
                            RemoveFromCharts(comparisonRecordInfo);
                        }
                    }
                }
            }
        }

		private void AddComparisonRecord(OcatRecordInfo recordInfo)
		{
			if (ComparisonRecords.Count <= _comparisonBrushes.Count())
			{
				var comparisonRecordInfo = GetComparisonRecordInfoFromOcatRecordInfo(recordInfo);
				var color = _freeColors.First();
				comparisonRecordInfo.Color = color;
				_freeColors.Remove(color);
				ComparisonRecords.Add(comparisonRecordInfo);
				InitialIconVisibility = !ComparisonRecords.Any();
				ComparisonItemControlHeight = ComparisonRecords.Any() ? "Auto" : "300";

				//Draw charts and performance parameter
				AddToCharts(comparisonRecordInfo);
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
