using CapFrameX.Configuration;
using CapFrameX.Statistics;
using CapFrameX.View.Controls;
using CapFrameX.ViewModel;
using LiveCharts;
using LiveCharts.Wpf;
using Prism.Events;
using System.ComponentModel;
using System.Windows.Controls;

namespace CapFrameX.View
{
	/// <summary>
	/// Interaction logic for SynchronizationView.xaml
	/// </summary>
	public partial class SynchronizationView : UserControl
	{
		public SynchronizationView()
		{
			InitializeComponent();
			OxyPlotHelper.SetAxisZoomWheelAndPan(SynchronizationPlotView);

			// Design time!
			if (DesignerProperties.GetIsInDesignMode(this))
			{
				var appConfiguration = new CapFrameXConfiguration();
				DataContext = new SynchronizationViewModel(new FrametimeStatisticProvider(appConfiguration),
					new EventAggregator(), appConfiguration);
			}
		}

		private void ResetChart_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
		{
			SynchronizationPlotView.ResetAllAxes();
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
