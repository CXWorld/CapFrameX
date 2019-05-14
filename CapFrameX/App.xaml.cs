using CapFrameX.PresentMonInterface;
using System;
using System.Windows;
using System.Windows.Navigation;

namespace CapFrameX
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            Bootstrapper bootstrapper = new Bootstrapper();
			bootstrapper.Run(true);
        }

		private void CapFrameXExit(object sender, ExitEventArgs e)
		{
			PresentMonCaptureService.TryKillPresentMon();
		}
	}
}
