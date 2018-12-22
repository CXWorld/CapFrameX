using CapFrameX.OcatInterface;
using CapFrameX.Statistics;
using GongSolutions.Wpf.DragDrop;
using LiveCharts;
using LiveCharts.Defaults;
using LiveCharts.Geared;
using LiveCharts.Wpf;
using Prism.Mvvm;
using Prism.Regions;
using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Windows;
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
            };

        private readonly IStatisticProvider _frametimeStatisticProvider;

        private bool _initialIconVisibility = true;
        private SeriesCollection _comparisonSeriesCollection;
        private SeriesCollection _comparisonColumnChartSeriesCollection;
        private string[] _comparisonColumnChartLabels;
        private string _comparisonItemControlHeight = "300";

        public bool InitialIconVisibility
        {
            get { return _initialIconVisibility; }
            set
            {
                _initialIconVisibility = value;
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

        public Func<double, string> ComparisonColumnChartFormatter { get; private set; } = value => value.ToString("N");

        public ObservableCollection<ComparisonRecordInfo> ComparisonRecords { get; }
            = new ObservableCollection<ComparisonRecordInfo>();

        public ComparisonDataViewModel(IStatisticProvider frametimeStatisticProvider)
        {
            _frametimeStatisticProvider = frametimeStatisticProvider;
        }

        public bool IsNavigationTarget(NavigationContext navigationContext)
        {
            return true;
        }

        public void OnNavigatedFrom(NavigationContext navigationContext)
        {

        }

        public void OnNavigatedTo(NavigationContext navigationContext)
        {

        }

        private ComparisonRecordInfo GetComparisonRecordInfoFromOcatRecordInfo(OcatRecordInfo ocatRecordInfo)
        {
            string infoText = string.Empty;
            var session = RecordManager.LoadData(ocatRecordInfo.FullPath);

            if (session != null)
            {
                var newLine = Environment.NewLine;
                infoText += "creation datetime: " + ocatRecordInfo.FileInfo.CreationTime.ToString() + newLine +
                            "capture time: " + Math.Round(session.LastFrameTime, 2).ToString(CultureInfo.InvariantCulture) + " sec" + newLine +
                            "number of samples: " + session.FrameTimes.Count.ToString();
            }

            return new ComparisonRecordInfo
            {
                Game = ocatRecordInfo.GameName,
                InfoText = infoText,
                DateTime = ocatRecordInfo.FileInfo.CreationTime.ToString(),
                Session = session
            };
        }

        private void SetCharts()
        {
            SetFrametimeChart();
            SetColumnChart();
        }

        private void SetColumnChart()
        {
            // ToDo: do not always refill whole collection -> performance optimization
            ComparisonColumnChartSeriesCollection = new SeriesCollection();

            var averages = ComparisonRecords.Select(record => Math.Round(record.Session.FrameTimes.Average(ft => 1000 / ft), 0));
            var p0dot1_quantiles = ComparisonRecords.Select(record => record.Session.FrameTimes.Select(ft => 1000 / ft))
                                                    .Select(fps => Math.Round(_frametimeStatisticProvider.GetPQuantileSequence(fps.ToList(), 0.001), 0));

            // Add ColumnSeries per parameter

            // Average
            ComparisonColumnChartSeriesCollection.Add(
                new ColumnSeries
                {
                    Title = "Average",
                    Values = new ChartValues<double>(averages),
                    // Kind of blue
                    Fill = _comparisonBrushes[1]
                });

            //0.1% quantile
            ComparisonColumnChartSeriesCollection.Add(
                new ColumnSeries
                {
                    Title = "0.1%",
                    Values = new ChartValues<double>(p0dot1_quantiles),
                    // Kind of red
                    Fill = _comparisonBrushes[2]
                });

            ComparisonColumnChartLabels = ComparisonRecords.Select(record => record.Game + Environment.NewLine + record.DateTime).ToArray();
        }

        private void SetFrametimeChart()
        {
            // ToDo: do not always refill whole collection -> performance optimization
            ComparisonSeriesCollection = new SeriesCollection();

            for (int i = 0; i < ComparisonRecords.Count; i++)
            {
                var session = ComparisonRecords[i].Session;
                var frametimePoints = session.FrameTimes.Select((val, index) => new ObservablePoint(session.FrameStart[index], val));
                var frametimeChartValues = new ChartValues<ObservablePoint>();
                frametimeChartValues.AddRange(frametimePoints);

                ComparisonSeriesCollection.Add(
                    new GLineSeries
                    {
                        Values = frametimeChartValues,
                        Fill = Brushes.Transparent,
                        Stroke = _comparisonBrushes[i],
                        StrokeThickness = 1,
                        LineSmoothness = 0,
                        PointGeometrySize = 0
                    });
            }
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
                            if (ComparisonRecords.Count <= _comparisonBrushes.Count())
                            {
                                var comparisonInfo = GetComparisonRecordInfoFromOcatRecordInfo(recordInfo);
                                comparisonInfo.Color = _comparisonBrushes[ComparisonRecords.Count];
                                ComparisonRecords.Add(comparisonInfo);
                                InitialIconVisibility = !ComparisonRecords.Any();
                                ComparisonItemControlHeight = ComparisonRecords.Any() ? "Auto" : "300";

                                //Draw charts and performance parameter
                                SetCharts();
                            }
                        }
                    }
                    else if (frameworkElement.Name == "RemoveRecordItemControl")
                    {
                        if (dropInfo.Data is ComparisonRecordInfo comparisonRecordInfo)
                        {
                            ComparisonRecords.Remove(comparisonRecordInfo);
                            InitialIconVisibility = !ComparisonRecords.Any();
                            ComparisonItemControlHeight = ComparisonRecords.Any() ? "Auto" : "300";

                            //Cleanup charts and performance parameter
                            SetCharts();
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
    }
}
