using CapFrameX.Configuration;
using CapFrameX.Data;
using CapFrameX.Overlay;
using CapFrameX.PresentMonInterface;
using CapFrameX.Statistics;
using CapFrameX.ViewModel;
using Prism.Events;
using System.ComponentModel;
using System.Diagnostics;
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
				var statisticProvider = new FrametimeStatisticProvider(appConfiguration);
				var recordDirectoryObserver = new RecordDirectoryObserver(appConfiguration);
				var recordDataProvider = new RecordDataProvider(recordDirectoryObserver, appConfiguration);
				var overlayEntryProvider = new OverlayEntryProvider();
				DataContext = new StateViewModel(new RecordDirectoryObserver(appConfiguration),
					new EventAggregator(), appConfiguration, new PresentMonCaptureService(),
					new OverlayService(statisticProvider, recordDataProvider, overlayEntryProvider, appConfiguration));
			}
		}

		private void Hyperlink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
		{
			Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri));
			e.Handled = true;
		}
	}
}
