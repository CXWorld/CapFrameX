using System.Windows.Controls;

namespace CapFrameX.View.Controls
{
	/// <summary>
	/// Interaction logic for FpsGraphControl.xaml
	/// </summary>
	public partial class FrametimeDistributionGraphControl : UserControl
    {
        public FrametimeDistributionGraphControl()
        {
            InitializeComponent();
		}

		private void ResetChart_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
		{
            FrametimeDistributionPlotView.ResetAllAxes();
		}
	}
}
