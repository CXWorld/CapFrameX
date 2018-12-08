using CapFrameX.ViewModel;
using System.ComponentModel;
using System.Windows.Controls;

namespace CapFrameX.View
{
	/// <summary>
	/// Interaktionslogik für ControlView.xaml
	/// </summary>
	public partial class ControlView : UserControl
	{
		public ControlView()
		{
			InitializeComponent();

			// Design time!
			if (DesignerProperties.GetIsInDesignMode(this))
			{
				DataContext = new ControlViewModel();
			}
		}
	}
}
