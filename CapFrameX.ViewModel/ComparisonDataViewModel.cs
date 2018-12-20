using CapFrameX.OcatInterface;
using GongSolutions.Wpf.DragDrop;
using LiveCharts;
using LiveCharts.Defaults;
using LiveCharts.Geared;
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

		private bool _initialIconVisibility = true;
		private SeriesCollection _comparisonSeriesCollection;

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

		public ObservableCollection<ComparisonRecordInfo> ComparisonRecords { get; }
			= new ObservableCollection<ComparisonRecordInfo>();

		public ComparisonDataViewModel()
		{

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
				Session = session
			};
		}

		private void SetCharts()
		{
			ComparisonSeriesCollection = new SeriesCollection();

			for (int i = 0; i < ComparisonRecords.Count; i++)
			{
				var record = ComparisonRecords[i];
				var session = record.Session;
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

								//Draw charts and performance parameter
								SetCharts();
							}
						}
					}
					else if (frameworkElement.Name == "DelteRecordItemControl")
					{
						if (dropInfo.Data is ComparisonRecordInfo comparisonRecordInfo)
						{
							ComparisonRecords.Remove(comparisonRecordInfo);
							InitialIconVisibility = !ComparisonRecords.Any();

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
