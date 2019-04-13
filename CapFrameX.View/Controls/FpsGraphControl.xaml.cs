using System.Windows.Controls;

namespace CapFrameX.View.Controls
{
	/// <summary>
	/// Interaction logic for FpsGraphControl.xaml
	/// </summary>
	public partial class FpsGraphControl : UserControl
    {
        public FpsGraphControl()
        {
            InitializeComponent();
			OxyPlotHelper.SetYAxisZoomer(FpsPlotView);
		}

		private void ResetChart_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
		{
			FpsPlotView.ResetAllAxes();
		}
	}
}
