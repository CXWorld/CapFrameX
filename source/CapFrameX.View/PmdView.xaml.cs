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

        private void ResetEPS12VChart_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            EPS12VPlotView.ResetAllAxes();
        }
    }
}
