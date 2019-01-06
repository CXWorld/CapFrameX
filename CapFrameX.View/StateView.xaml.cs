using CapFrameX.Configuration;
using CapFrameX.OcatInterface;
using CapFrameX.ViewModel;
using Prism.Events;
using System.ComponentModel;
using System.Windows.Controls;

namespace CapFrameX.View
{
	/// <summary>
	/// Interaktionslogik für StateView.xaml
	/// </summary>
	public partial class StateView : UserControl
	{
		public StateView()
		{
			InitializeComponent();

			if (DesignerProperties.GetIsInDesignMode(this))
			{
				var appConfiguration = new CapFrameXConfiguration();
				DataContext = new StateViewModel( new RecordDirectoryObserver(appConfiguration), new EventAggregator(), appConfiguration);
			}
		}
	}
}
