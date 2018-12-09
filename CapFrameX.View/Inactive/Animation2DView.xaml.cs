using CapFrameX.ViewModel;
using System.ComponentModel;
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

            // Design time!
            if (DesignerProperties.GetIsInDesignMode(this))
            {
                DataContext = new Animation2DViewModel(FrameTestCanvas);
            }            
        }
    }
}
