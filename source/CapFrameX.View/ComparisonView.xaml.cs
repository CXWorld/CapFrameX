using CapFrameX.ViewModel;
using System;
using System.Reactive.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace CapFrameX.View
{
	/// <summary>
	/// Interaction logic for ComparisonDataView.xaml
	/// </summary>
	public partial class ComparisonView : UserControl
	{
		public ComparisonView()
		{			
			InitializeComponent();

			var context = SynchronizationContext.Current;
			(DataContext as ComparisonViewModel)?.ResetLShapeChart
				.ObserveOn(context)
				.SubscribeOn(context)
				.Subscribe(x => ResetLShapeChart());
				
			// L-shape chart y axis formatter
			Func<double, string> formatFunc = (x) => string.Format("{0:0.0}", x);
			LShapeY.LabelFormatter = formatFunc;
		}

		private void ResetLShapeChart_MouseDoubleClick(object sender, MouseButtonEventArgs e)
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

		private void FirstSecondsTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
		{
			var key = e.Key;

			if (key == Key.Enter)
			{
				GraphTab.Focus();
			}
			(DataContext as ComparisonViewModel).OnRangeSliderValuesChanged();
		}
		
		private void LastSecondsTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
		{
			var key = e.Key;

			if (key == Key.Enter)
			{
				GraphTab.Focus();
			}
			(DataContext as ComparisonViewModel).OnRangeSliderValuesChanged();
		}

        private void RangeSlider_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
        {
            (DataContext as ComparisonViewModel).OnRangeSliderDragCompleted();
        }

        private void CustomTitle_PreviewKeyDown(object sender, KeyEventArgs e)
		{
			var key = e.Key;

			if (key == Key.Enter)
			{
				GraphTab.Focus();
			}
		}

        private void ColorPickerPopup_Closed(object sender, EventArgs e)
        {

             (DataContext as ComparisonViewModel).SaveLineGraphColors();
            (DataContext as ComparisonViewModel).SetLineGraphColors();
        }

        private void NumberValidationTextBox(object sender, TextCompositionEventArgs e)
		{
			Regex regex = new Regex("[^0-9.-]+");
			e.Handled = regex.IsMatch(e.Text);
		}
    }
}
