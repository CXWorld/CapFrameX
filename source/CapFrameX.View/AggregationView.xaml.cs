using CapFrameX.Configuration;
using CapFrameX.Data;
using CapFrameX.Statistics;
using CapFrameX.ViewModel;
using Prism.Events;
using System.ComponentModel;
using System.Windows.Controls;
using System.Windows.Input;

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
				var recordDataProvider = new RecordDataProvider(new RecordDirectoryObserver(appConfiguration), appConfiguration);
				DataContext = new AggregationViewModel(new FrametimeStatisticProvider(appConfiguration), recordDataProvider, new EventAggregator(), appConfiguration);
			}
		}

		private void AggregationItemDataGrid_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
		{
			(DataContext as AggregationViewModel).SelectedAggregationEntryIndex = -1;
			Keyboard.ClearFocus();
		}
	}
}
