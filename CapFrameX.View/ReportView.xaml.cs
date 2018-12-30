using CapFrameX.ViewModel;
using System.ComponentModel;
using System.Windows.Controls;

namespace CapFrameX.View
{
    /// <summary>
    /// Interaktionslogik für ReportView.xaml
    /// </summary>
    public partial class ReportView : UserControl
    {
        public ReportView()
        {
            InitializeComponent();

            // Design time!
            if (DesignerProperties.GetIsInDesignMode(this))
            {
                DataContext = new ReportViewModel();
            }
        }
    }
}
