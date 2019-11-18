using CapFrameX.Configuration;
using CapFrameX.Data;
using CapFrameX.ViewModel;
using Prism.Events;
using Prism.Regions;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;

namespace CapFrameX.View
{
	/// <summary>
	/// Interaction logic for ColorbarView.xaml
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
				DataContext = new ColorbarViewModel(new RegionManager(), new RecordDirectoryObserver(appConfiguration), new EventAggregator(), appConfiguration);
			}
		}

		private void PopupBox_RequestBringIntoView(object sender, System.Windows.RequestBringIntoViewEventArgs e)
        {
		}

        private void CapturePageHelp_Click(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Process.Start("https://github.com/DevTechProfile/CapFrameX#instruction-manual");
        }

		private void GitHubButton_Click(object sender, RoutedEventArgs e)
		{
			System.Diagnostics.Process.Start("https://github.com/DevTechProfile/CapFrameX");
		}
	}
}
