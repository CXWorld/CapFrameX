using CapFrameX.ViewModel;
using System.Windows.Controls;

namespace CapFrameX.View
{
    /// <summary>
    /// Interaction logic for Animation2DView.xaml
    /// </summary>
    public partial class Animation2DView : UserControl
    {
        public Animation2DView()
        {
            InitializeComponent();
            DataContext = new Animation2DViewModel(FrameTestCanvas);
        }
    }
}
