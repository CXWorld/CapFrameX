using PmcReader.Interop;
using System;
using System.Windows.Forms;

namespace PmcReader
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            // Was configured through App.config's System.Windows.Forms.ApplicationConfigurationSection
            // on .NET Framework. That section is ignored on .NET, so the mode is set here instead -
            // before anything can create a window, which is what makes the call take effect.
            Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
            Ring0.Open();
            OpCode.Open();
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new HaswellForm());
            OpCode.Close();
            Ring0.Close();
        }
    }
}
