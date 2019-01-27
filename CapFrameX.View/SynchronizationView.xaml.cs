using CapFrameX.Configuration;
using CapFrameX.Statistics;
using CapFrameX.ViewModel;
using LiveCharts;
using LiveCharts.Wpf;
using Prism.Events;
using System.ComponentModel;
using System.Windows.Controls;

namespace CapFrameX.View
{
	/// <summary>
	/// Interaktionslogik für SynchronizationView.xaml
	/// </summary>
	public partial class SynchronizationView : UserControl
	{
		public SynchronizationView()
		{
			InitializeComponent();

			// Design time!
			if (DesignerProperties.GetIsInDesignMode(this))
			{
				DataContext = new SynchronizationViewModel(new FrametimeStatisticProvider(),
					new EventAggregator(), new CapFrameXConfiguration());
			}
		}

		private void ResetChart_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
		{
			//Use the axis MinValue/MaxValue properties to specify the values to display.
			//use double.Nan to clear it.

			FrameDisplayChangeTimesX.MinValue = double.NaN;
			FrameDisplayChangeTimesX.MaxValue = double.NaN;
			FrameDisplayChangeTimesY.MinValue = double.NaN;
			FrameDisplayChangeTimesY.MaxValue = double.NaN;
		}

		private void Chart_OnDataClick(object sender, ChartPoint chartpoint)
		{
			var chart = (PieChart)chartpoint.ChartView;

			//clear selected slice.
			foreach (PieSeries series in chart.Series)
				series.PushOut = 0;

			var selectedSeries = (PieSeries)chartpoint.SeriesView;
			selectedSeries.PushOut = 8;
		}
	}
}
