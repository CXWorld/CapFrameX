using CapFrameX.PresentMonInterface;
using System.Diagnostics;
using System.Linq;
using System.Windows;

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


        private void Application_Startup(object sender, StartupEventArgs e)
        {

            Process proc = Process.GetCurrentProcess();
            var processes = Process.GetProcesses().Where(p =>
                             p.ProcessName == proc.ProcessName);

            if (processes.Any())
            {
                MessageBox.Show("Already an instance is running...");
                Current.Shutdown();
            }
        }
    }
}
