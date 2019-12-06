using System.Windows.Controls;

namespace CapFrameX.View.Controls
{
	/// <summary>
	/// Interaction logic for FrametimeGraphControl.xaml
	/// </summary>
	public partial class FrametimeGraphControl : UserControl
	{
        public FrametimeGraphControl()
        {
            InitializeComponent();
			OxyPlotHelper.SetAxisZoomWheelAndPan(FrametimePlotView);
		}

		private void ResetChart_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
		{
			FrametimePlotView.ResetAllAxes();
		}
	}
}
