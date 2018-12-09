using CapFrameX.ViewModel;
using System.ComponentModel;
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

            // Design time!
            if (DesignerProperties.GetIsInDesignMode(this))
            {
                DataContext = new Animation3DViewModel();
            }            
        }
    }
}
