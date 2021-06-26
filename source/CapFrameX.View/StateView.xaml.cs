using CapFrameX.Configuration;
using CapFrameX.Data;
using CapFrameX.Overlay;
using CapFrameX.PresentMonInterface;
using CapFrameX.Statistics;
using CapFrameX.Updater;
using CapFrameX.ViewModel;
using Microsoft.Extensions.Logging;
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
		}

        private void PackIcon_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
			Process.Start(new ProcessStartInfo("https://github.com/DevTechProfile/CapFrameX/releases"));
			e.Handled = true;
		}

        private void SystemInfo_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
			(DataContext as StateViewModel).RefreshSystemInfo();
			e.Handled = true;
		}
    }
}
