using CapFrameX.Configuration;
using CapFrameX.ViewModel;
using Prism.Events;
using Prism.Regions;
using System.ComponentModel;
using System.Windows.Controls;

namespace CapFrameX.View
{
	/// <summary>
	/// Interaktionslogik für ColorbarView.xaml
	/// </summary>
	public partial class ColorbarView : UserControl
	{
		public ColorbarView()
		{
			InitializeComponent();

			// Design time!
			if (DesignerProperties.GetIsInDesignMode(this))
			{
				var appConfiguration = new CapFrameXConfiguration();
				DataContext = new ColorbarViewModel(new RegionManager(), new EventAggregator(), appConfiguration);
			}
		}
	}
}
