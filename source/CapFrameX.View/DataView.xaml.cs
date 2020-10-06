using CapFrameX.ViewModel;
using LiveCharts;
using LiveCharts.Wpf;
using System;
using System.Reactive.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Controls;
using System.Windows.Input;

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

		private void FirstSecondsTextBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
		{
			var key = e.Key;

			if (key == Key.Enter)
			{
				GraphTab.Focus();
				(DataContext as DataViewModel).OnRangeSliderDragCompleted();
			}
				
		}

		private void LastSecondsTextBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
		{
			var key = e.Key;

			if (key == Key.Enter)
			{
				GraphTab.Focus();
				(DataContext as DataViewModel).OnRangeSliderDragCompleted();
			}			
		}

		private void StutteringThreshold_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
			var key = e.Key;

			if (key == Key.Enter)
			{
				GraphTab.Focus();
				(DataContext as DataViewModel).OnStutteringOptionsChanged();
			}
		}

		private void StutteringFactor_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
		{
			var key = e.Key;

			if (key == Key.Enter)
			{
				GraphTab.Focus();
				(DataContext as DataViewModel).OnStutteringOptionsChanged();
			}
		}
		
		private void NumberValidationTextBox(object sender, TextCompositionEventArgs e)
		{
			Regex regex = new Regex("[^0-9.-]+");
			e.Handled = regex.IsMatch(e.Text);
		}
	}
}
