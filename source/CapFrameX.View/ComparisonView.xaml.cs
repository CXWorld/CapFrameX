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

        private void ResetChart_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            ComparisonPlotView.ResetAllAxes();
        }

        private void SelectedColorChanged(object sender, RoutedPropertyChangedEventArgs<Color?> e) { }
    }
}
