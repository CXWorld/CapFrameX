using CapFrameX.ViewModel;
using System.Windows.Controls;

namespace CapFrameX.View
{
    /// <summary>
    /// Interaction logic for Animation3DView.xaml
    /// </summary>
    public partial class Animation3DView : UserControl
    {
        public Animation3DView()
        {
            InitializeComponent();
            DataContext = new Animation3DViewModel();
        }
    }
}
