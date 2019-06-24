using CapFrameX.ViewModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace CapFrameX.View
{
    /// <summary>
    /// Interaction logic for ComparisonView.xaml
    /// </summary>
    public partial class ComparisonView : UserControl
    {
        public ComparisonView()
        {
            InitializeComponent();

            // Design time!
            if (DesignerProperties.GetIsInDesignMode(this))
            {
                DataContext = new ComparisonViewModel();
            }            
        }

        private void SelectedColorChanged(object sender, RoutedPropertyChangedEventArgs<Color?> e) { }
    }
}
