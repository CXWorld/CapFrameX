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
    /// Interaktionslogik für FpsGraphControl.xaml
    /// </summary>
    public partial class FpsGraphControl : UserControl
    {
        public FpsGraphControl()
        {
            InitializeComponent();
        }

		private void ResetChart_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
		{
			//Use the axis MinValue/MaxValue properties to specify the values to display.
			//use double.Nan to clear it.

			FpsX.MinValue = double.NaN;
			FpsX.MaxValue = double.NaN;
			FpsY.MinValue = double.NaN;
			FpsY.MaxValue = double.NaN;
		}
	}
}
