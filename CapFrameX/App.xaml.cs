using CapFrameX.PresentMonInterface;
using System;
using System.Diagnostics;
using System.Linq;
using System.Security.Principal;
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
            if (!IsAdministrator)
            {
                MessageBox.Show("Run CapFrameX as administrator. Right click on desktop shortcut" + Environment.NewLine
                    + "and got to Properties -> Shortcut -> Advanced then check option Run as administrator.");
                Current.Shutdown();
            }

            Process proc = Process.GetCurrentProcess();
            var count = Process.GetProcesses().Where(p =>
                             p.ProcessName == proc.ProcessName).Count();

            if (count > 1)
            {
                MessageBox.Show("Already an instance is running...");
                Current.Shutdown();
            }
        }

        public static bool IsAdministrator =>
            new WindowsPrincipal(WindowsIdentity.GetCurrent())
                   .IsInRole(WindowsBuiltInRole.Administrator);
    }
}
