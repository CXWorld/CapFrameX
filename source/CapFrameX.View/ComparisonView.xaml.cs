using CapFrameX.Configuration;
using CapFrameX.Statistics;
using CapFrameX.View.Controls;
using CapFrameX.ViewModel;
using Prism.Events;
using System.ComponentModel;
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
            OxyPlotHelper.SetYAxisZoomWheelAndPan(ComparisonPlotView);

            // Design time!
            if (DesignerProperties.GetIsInDesignMode(this))
            {
                var appConfiguration = new CapFrameXConfiguration();
                DataContext = new ComparisonViewModel(new FrametimeStatisticProvider(appConfiguration), 
					new FrametimeAnalyzer(), new EventAggregator(), appConfiguration);
            }
        }

        private void ResetFrametimeChart_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            ComparisonPlotView.ResetAllAxes();
        }

		private void ResetLShapeChart_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
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
