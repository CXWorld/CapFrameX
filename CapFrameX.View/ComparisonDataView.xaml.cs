using CapFrameX.ViewModel;
using System.ComponentModel;
using System.Windows.Controls;

namespace CapFrameX.View
{
	/// <summary>
	/// Interaktionslogik für ComparisonDataView.xaml
	/// </summary>
	public partial class ComparisonDataView : UserControl
    {
        public ComparisonDataView()
        {
            InitializeComponent();

			// Design time!
			if (DesignerProperties.GetIsInDesignMode(this))
			{
				DataContext = new ComparisonDataViewModel();
			}
		}

		private void ResetChart_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
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
