using CapFrameX.ViewModel;
using Prism.Events;
using System.ComponentModel;
using System.Windows.Controls;

namespace CapFrameX.View
{
	/// <summary>
	/// Interaktionslogik für OverlayView.xaml
	/// </summary>
	public partial class OverlayView : UserControl
	{
		public OverlayView()
		{
			InitializeComponent();

			if (DesignerProperties.GetIsInDesignMode(this))
			{
				DataContext = new OverlayViewModel(new EventAggregator());
			}
		}
	}
}
