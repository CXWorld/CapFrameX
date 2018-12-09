using CapFrameX.Statistics;
using CapFrameX.ViewModel;
using Prism.Events;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;

namespace CapFrameX.View
{
	/// <summary>
	/// Interaktionslogik für DataView.xaml
	/// </summary>
	public partial class DataView : UserControl
	{
		public DataView()
		{
			InitializeComponent();

			// Design time!
			if (DesignerProperties.GetIsInDesignMode(this))
			{
				DataContext = new DataViewModel(new FrametimeStatisticProvider(), new FrametimeAnalyzer(), new EventAggregator());
			}
		}

		private void ResetZoomOnClick(object sender, RoutedEventArgs e)
		{
			//Use the axis MinValue/MaxValue properties to specify the values to display.
			//use double.Nan to clear it.

			FrametimesX.MinValue = double.NaN;
			FrametimesX.MaxValue = double.NaN;
			FrametimesY.MinValue = double.NaN;
			FrametimesY.MaxValue = double.NaN;

			LShapeX.MinValue = double.NaN;
			LShapeX.MaxValue = double.NaN;
			LShapeY.MinValue = double.NaN;
			LShapeY.MaxValue = double.NaN;
		}
	}
}
