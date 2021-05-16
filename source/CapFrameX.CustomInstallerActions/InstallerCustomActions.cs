using Microsoft.Deployment.WindowsInstaller;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;

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

            try
            {
                const string run = "SOFTWARE\\WOW6432Node\\Microsoft\\Windows\\CurrentVersion\\Run";
                const string appName = "CapFrameX";

                RegistryKey startKey = Registry.LocalMachine.OpenSubKey(run, true);
                var val = startKey.GetValue("appName");

                if (val != null)
                    startKey.DeleteValue(appName);
            }
            catch { session.Log("Error RemoveAutoStartKey"); }

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
