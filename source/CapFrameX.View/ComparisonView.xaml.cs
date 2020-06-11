using CapFrameX.ViewModel;
using System;
using System.Reactive.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
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
				.Subscribe(dummy => ResetLShapeChart());
		}

		private void ResetLShapeChart_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
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

		private void SelectedColorChanged(object sender, RoutedPropertyChangedEventArgs<Color?> e) { }
	}
}
