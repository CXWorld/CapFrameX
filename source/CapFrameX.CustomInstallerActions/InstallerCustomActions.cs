using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CapFrameX.Configuration;
using Microsoft.Win32;
using Microsoft.Win32.TaskScheduler;
using WixToolset.Dtf.WindowsInstaller;

namespace CapFrameX.CustomInstallerActions
{
    public class InstallerCustomActions
    {
        private static HashSet<string> _validFilenames = 
            new HashSet<string>()
        {
            "settings.json",
            "AppSettings.json",
            "OverlayEntryConfiguration_0.json",
            "OverlayEntryConfiguration_1.json",
            "OverlayEntryConfiguration_2.json",
            "Processes.json",
            "SensorEntryConfiguration.json"
        };

        private const string APPNAME = "CapFrameX";

        private const string DOTNET_DESKTOP_RUNTIME_PROPERTY = "DOTNETDESKTOPRUNTIMEX64FOUND";

        private const string VULKAN_IMPLICIT_LAYERS = @"SOFTWARE\Khronos\Vulkan\ImplicitLayers";

        // Every manifest this product has ever shipped is named cfx_osd_vklayer*.json, and nothing
        // else uses that prefix. Matching the FILE NAME instead of the full path is what makes the
        // purge version- and location-agnostic: it catches _v1/_v2/..., an install folder from an
        // earlier version, and a developer's build tree registered by vk_layer\register_layer.cmd.
        private const string LAYER_MANIFEST_PREFIX = "cfx_osd_vklayer";

        /// <summary>
        /// Detects the required Desktop Runtime before the MSI launch conditions are evaluated.
        /// This custom action targets .NET Framework 4.7.2, so it can run before the .NET 10 WPF
        /// application and avoids relying on managed application startup for the prerequisite UX.
        /// </summary>
        [CustomAction]
        public static ActionResult DetectDotNetDesktopRuntime(Session session)
        {
            string dotNetBasePath = DotNetRuntimeDetector.GetSystemWideDotNetBasePath();
            bool isInstalled = DotNetRuntimeDetector.IsRequiredDesktopRuntimeInstalled(dotNetBasePath);

            session[DOTNET_DESKTOP_RUNTIME_PROPERTY] = isInstalled ? "1" : string.Empty;
            session.Log(
                ".NET Desktop Runtime {0}.0 (x64) detected: {1}",
                DotNetRuntimeDetector.RequiredMajorVersion,
                isInstalled);

            return ActionResult.Success;
        }

        /// <summary>
        /// Removes every CapFrameX Vulkan layer registration from HKLM, in BOTH registry views.
        /// The component-based registration in Product.wxs only ever removes the one exact value
        /// it wrote, which leaves stale entries behind — and a stale entry is worse than none: the
        /// loader identifies a layer by the name inside its manifest, so a manifest it can reach
        /// but whose DLL it cannot load SHADOWS the correct registration and disables the layer for
        /// that bitness entirely. Sequenced before WriteRegistryValues, so an install also ends
        /// with exactly one registration: the one under the install folder.
        /// </summary>
        [CustomAction]
        public static ActionResult PurgeVulkanLayerRegistrations(Session session)
        {
            session.Log("Begin PurgeVulkanLayerRegistrations");

            PurgeLayerValues(session, RegistryHive.LocalMachine, RegistryView.Registry64);
            PurgeLayerValues(session, RegistryHive.LocalMachine, RegistryView.Registry32);

            return ActionResult.Success;
        }

        /// <summary>
        /// The per-user half of the purge. The installer never writes HKCU and register_layer.cmd
        /// deliberately removes it, but a manual or third-party entry there is both reachable and
        /// not split by bitness, so one wrong-bitness manifest disables the layer for everyone.
        /// Runs impersonated on purpose: a deferred, non-impersonated action would address the
        /// SYSTEM account's hive instead of the installing user's.
        /// </summary>
        [CustomAction]
        public static ActionResult PurgeVulkanLayerRegistrationsPerUser(Session session)
        {
            session.Log("Begin PurgeVulkanLayerRegistrationsPerUser");

            PurgeLayerValues(session, RegistryHive.CurrentUser, RegistryView.Default);

            return ActionResult.Success;
        }

        private static void PurgeLayerValues(Session session, RegistryHive hive, RegistryView view)
        {
            try
            {
                using (var baseKey = RegistryKey.OpenBaseKey(hive, view))
                using (var layers = baseKey.OpenSubKey(VULKAN_IMPLICIT_LAYERS, true))
                {
                    if (layers == null)
                        return;

                    // Snapshot the names: deleting while enumerating the live key is undefined.
                    foreach (var name in layers.GetValueNames().ToArray())
                    {
                        if (!IsCapFrameXLayerManifest(name))
                            continue;

                        try
                        {
                            layers.DeleteValue(name, false);
                            session.Log("Removed layer registration ({0}/{1}): {2}", hive, view, name);
                        }
                        catch (Exception ex)
                        {
                            // Never fail the install over a leftover we could not reach.
                            session.Log("Could not remove layer registration {0}: {1}", name, ex.Message);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                session.Log("PurgeLayerValues({0}/{1}) failed: {2}", hive, view, ex.Message);
            }
        }

        private static bool IsCapFrameXLayerManifest(string valueName)
        {
            if (string.IsNullOrEmpty(valueName))
                return false;

            string fileName;
            try
            {
                // The value name is a path by convention, but nothing enforces that it is a valid
                // one; a foreign entry with invalid path characters must not abort the purge.
                fileName = Path.GetFileName(valueName);
            }
            catch (ArgumentException)
            {
                return false;
            }

            return fileName != null
                && fileName.StartsWith(LAYER_MANIFEST_PREFIX, StringComparison.OrdinalIgnoreCase)
                && fileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase);
        }

        [CustomAction]
        public static ActionResult RemoveAppdataConfigFiles(Session session)
        {
            session.Log("Begin RemoveAppdataConfigFiles");

            try
            {
                var appdataFolder = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                var configFolder = Path.Combine(appdataFolder, APPNAME);

                // Only remove UI config files
                if (Directory.Exists(configFolder))
                {
                    foreach (var file in Directory.GetFiles(configFolder, "*.json"))
                    {
                        File.Delete(file);
                    }
                }
            }
            catch { session.Log("Error while removing AppData config files!"); }

            return ActionResult.Success;
        }

        [CustomAction]
        public static ActionResult RemoveAutoStartKey(Session session)
        {
            session.Log("Begin RemoveAutoStartKey");

            try
            {
                using (TaskService ts = new TaskService())
                {

                    if (ts.RootFolder.GetTasks().Any(t => t.Name == APPNAME))
                    {
                        ts.RootFolder.DeleteTask(APPNAME);
                    }
                }

                RegistryKey startKey = Registry.LocalMachine.OpenSubKey("SOFTWARE\\WOW6432Node\\Microsoft\\Windows\\CurrentVersion\\Run", true);
                startKey?.DeleteValue(APPNAME);
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
                    var configDestinationPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
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
