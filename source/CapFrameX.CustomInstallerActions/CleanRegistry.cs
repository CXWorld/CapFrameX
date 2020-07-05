using Microsoft.Deployment.WindowsInstaller;
using Microsoft.Win32;

namespace CapFrameX.CustomInstallerActions
{
    public class CleanRegistry
    {
        [CustomAction]
        public static ActionResult RemoveAutoStartKey(Session session)
        {
            session.Log("Begin RemoveAutoStartKey");

            try
            {
                string run = "SOFTWARE\\WOW6432Node\\Microsoft\\Windows\\CurrentVersion\\Run";
                string appName = "CapFrameX";

                RegistryKey startKey = Registry.LocalMachine.OpenSubKey(run, true);
                var val = startKey.GetValue("appName");

                if (val != null)
                    startKey.DeleteValue(appName);
            }
            catch { }

            return ActionResult.Success;
        }
    }
}
