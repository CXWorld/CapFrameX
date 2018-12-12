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
    }
}
