using Microsoft.Deployment.WindowsInstaller;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Win32.TaskScheduler;
using System.Linq;

namespace CapFrameX.CustomInstallerActions
{
    public class InstallerCustomActions
    {
        private static HashSet<string> _validFilenames = new HashSet<string>()
        {
            "settings.json",
            "AppSettings.json",
            "OverlayEntryConfiguration_0.json",
            "OverlayEntryConfiguration_1.json",
            "OverlayEntryConfiguration_2.json",
            "Processes.json",
            "SensorEntryConfiguration.json"
        };

        [CustomAction]
        public static ActionResult RemoveAutoStartKey(Session session)
        {
            session.Log("Begin RemoveAutoStartKey");

            const string appName = "CapFrameX";

            try
            {
                using (TaskService ts = new TaskService())
                {

                    if (ts.RootFolder.GetTasks().Any(t => t.Name == appName))
                    {
                        ts.RootFolder.DeleteTask(appName);
                    }
                }

                RegistryKey startKey = Registry.LocalMachine.OpenSubKey("SOFTWARE\\WOW6432Node\\Microsoft\\Windows\\CurrentVersion\\Run", true);
                startKey?.DeleteValue(appName);
            }
            catch { session.Log("Error while cleaning registry or removing autostart!");}

            return ActionResult.Success;
        }

        [CustomAction]
        public static ActionResult CopyConfigResources(Session session)
        {
            session.Log("Begin CopyConfigResources");

            try
            {
                // https://www.advancedinstaller.com/user-guide/set-windows-installer-property-custom-action.html
                var configSourcePath = Path.Combine(session["INSTALLLOCATION"], "Configuration");
                if (Directory.Exists(configSourcePath))
                {
                    var configDestinationPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                        @"CapFrameX\Configuration\");

                    if (!Directory.Exists(configDestinationPath))
                    {
                        Directory.CreateDirectory(configDestinationPath);

                        foreach (var fullPath in Directory.EnumerateFiles(configSourcePath))
                        {
                            var fileName = Path.GetFileName(fullPath);

                            if (_validFilenames.Contains(fileName))
                                File.Copy(fullPath, Path.Combine(configDestinationPath, fileName));
                        }
                    }
                }
            }
            catch { session.Log("Error CopyConfigResources"); }

            return ActionResult.Success;
        }
    }
}
