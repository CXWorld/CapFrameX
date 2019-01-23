using CapFrameX.ViewModel;
using System.ComponentModel;
using System.Windows.Controls;

namespace CapFrameX.View
{
	/// <summary>
	/// Interaktionslogik für SynchronizationView.xaml
	/// </summary>
	public partial class SynchronizationView : UserControl
	{
		public SynchronizationView()
		{
			InitializeComponent();

			// Design time!
			if (DesignerProperties.GetIsInDesignMode(this))
			{
				DataContext = new SynchronizationViewModel();
			}
		}
	}
}
