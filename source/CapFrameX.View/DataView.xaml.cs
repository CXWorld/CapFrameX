using CapFrameX.Configuration;
using CapFrameX.Data;
using CapFrameX.Statistics;
using CapFrameX.ViewModel;
using LiveCharts;
using LiveCharts.Wpf;
using Microsoft.Extensions.Logging;
using Prism.Events;
using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Reactive.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace CapFrameX.View
{
	/// <summary>
	/// Interaction logic for DataView.xaml
	/// </summary>
	public partial class DataView : UserControl
	{
		public DataView()
		{
			InitializeComponent();

			// Design time!
			if (DesignerProperties.GetIsInDesignMode(this))
			{
				var appConfiguration = new CapFrameXConfiguration();
				var loggerFactory = new LoggerFactory();
				DataContext = new DataViewModel(new FrametimeStatisticProvider(appConfiguration),
					new FrametimeAnalyzer(), new EventAggregator(), appConfiguration, new RecordManager(loggerFactory.CreateLogger<RecordManager>(), appConfiguration, new RecordDirectoryObserver(appConfiguration, loggerFactory.CreateLogger<RecordDirectoryObserver>()), new AppVersionProvider()));
			}

			var context = SynchronizationContext.Current;
			(DataContext as DataViewModel)?.ResetLShapeChart
				.ObserveOn(context)
				.SubscribeOn(context)
				.Subscribe(dummy => ResetLShapeChart());
		}

		private void Chart_OnDataClick(object sender, ChartPoint chartpoint)
		{
			var chart = (PieChart)chartpoint.ChartView;

			//clear selected slice
			foreach (PieSeries series in chart.Series)
				series.PushOut = 0;

			var selectedSeries = (PieSeries)chartpoint.SeriesView;
			selectedSeries.PushOut = 8;
		}

		private void ResetChart_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
			=> ResetLShapeChart();

		private void ResetLShapeChart()
		{
			//Use the axis MinValue/MaxValue properties to specify the values to display.
			//use double.Nan to clear it.

			LShapeX.MinValue = double.NaN;
			LShapeX.MaxValue = double.NaN;
			LShapeY.MinValue = double.NaN;
			LShapeY.MaxValue = double.NaN;
		}

		private void RangeSlider_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
		{
			(DataContext as DataViewModel).OnRangeSliderDragCompleted();
		}
	}
}
