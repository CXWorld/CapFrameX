using CapFrameX.Configuration;
using CapFrameX.OcatInterface;
using CapFrameX.ViewModel;
using Prism.Events;
using System.ComponentModel;
using System.Windows.Controls;

namespace CapFrameX.View
{
	/// <summary>
	/// Interaction logic for  AggregationView.xaml
	/// </summary>
	public partial class AggregationView : UserControl
	{
		public AggregationView()
		{
			InitializeComponent();

			// Design time!
			if (DesignerProperties.GetIsInDesignMode(this))
			{
				var appConfiguration = new CapFrameXConfiguration();
				DataContext = new AggregationViewModel(new RecordDirectoryObserver(appConfiguration), new EventAggregator(), appConfiguration);
			}
		}
	}
}
