using System.Windows.Controls;
using System.Windows.Input;

namespace CapFrameX.View
{
    /// <summary>
    /// Interaction logic for PmdView.xaml
    /// </summary>
    public partial class PmdView : UserControl
    {
        public PmdView()
        {
            InitializeComponent();
        }

        private void ResetEPS12VChartMetrics_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            EPS12VPlotViewMetrics.ResetAllAxes();
        }

        private void ResetPciExpressChartMetrics_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            PciExpressPlotViewMetrics.ResetAllAxes();
        }

        private void ResetEPS12VChartAnalysis_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            EPS12VPlotViewAnalysis.ResetAllAxes();
        }

        private void ResetPciExpressChartAnalysis_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            PciExpressPlotViewAnalysis.ResetAllAxes();
        }

        private void ResetFrametimeChart_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            FrametimeAnalysis.ResetAllAxes();
        }

        private void FrametimeCheckBox_Checked(object sender, System.Windows.RoutedEventArgs e)
        {
            if (AnalysisChartGrid.RowDefinitions.Count == 2)
                AnalysisChartGrid.RowDefinitions.Add(new RowDefinition());
            else
                return;
        }

        private void FrametimeCheckBox_Unchecked(object sender, System.Windows.RoutedEventArgs e)
        {
            if (AnalysisChartGrid.RowDefinitions.Count == 3)
                AnalysisChartGrid.RowDefinitions.RemoveAt(2);
            else
                return;
        }
    }
}
