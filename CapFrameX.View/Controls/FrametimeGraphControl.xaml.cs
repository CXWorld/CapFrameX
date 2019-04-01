using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace CapFrameX.View.Controls
{
    /// <summary>
    /// Interaktionslogik für FrametimeGraphControl.xaml
    /// </summary>
    public partial class FrametimeGraphControl : UserControl
    {
        public FrametimeGraphControl()
        {
            InitializeComponent();
        }

		private void ResetChart_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
		{
			//Use the axis MinValue/MaxValue properties to specify the values to display.
			//use double.Nan to clear it.

			FrametimesX.MinValue = double.NaN;
			FrametimesX.MaxValue = double.NaN;
			FrametimesY.MinValue = double.NaN;
			FrametimesY.MaxValue = double.NaN;
		}
	}
}
