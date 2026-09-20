using System;
using System.Collections.Generic;
using Microsoft.Win32;

namespace CapFrameX.Configuration
{
    /// <summary>
    /// Report containing the results of dependency checks.
    /// </summary>
    public class DependencyCheckReport
    {
        /// <summary>
        /// Gets whether all required dependencies are installed.
        /// </summary>
        public bool Valid { get; set; }

        /// <summary>
        /// Gets the missing .NET version if not installed, null otherwise.
        /// </summary>
        public string MissingDotNetFrameworkVersion { get; set; }

        /// <summary>
        /// Gets the list of missing Visual C++ Redistributable versions, null if all are installed.
        /// </summary>
        public List<string> MissingVCRedistVersions { get; set; }
    }

    /// <summary>
    /// Checks for required runtime dependencies in portable mode.
    /// </summary>
    public static class AppDependencyChecker
    {
        public const int MajorDotNetVersionRequired = DotNetRuntimeDetector.RequiredMajorVersion;

        // Registry keys for VC++ 2015-2022 Redistributable detection (from Bundle.wxs)
        private const string VCRedistx64RegistryKey = @"SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\x64";

        private static readonly string DotNetBasePath = DotNetRuntimeDetector.GetSystemWideDotNetBasePath();

        /// <summary>
        /// Checks for missing dependencies and returns a report.
        /// </summary>
        /// <returns>A report containing information about missing dependencies.</returns>
        public static DependencyCheckReport CheckAndNotifyMissingDependencies()
        {
            return CheckMissingDependencies(DotNetBasePath, IsVCRedistx64Installed());
        }

        /// <summary>
        /// Checks dependencies using explicit probe results so the decision logic can be tested
        /// without depending on the development machine's installed runtimes or registry.
        /// </summary>
        internal static DependencyCheckReport CheckMissingDependencies(
            string dotNetBasePath,
            bool isVCRedistx64Installed)
        {
            var report = new DependencyCheckReport
            {
                Valid = true
            };

            // A WPF application needs Microsoft.WindowsDesktop.App. Microsoft.NETCore.App on its
            // own is insufficient, and a later major version is not used by the default .NET
            // roll-forward policy.
            if (!IsRequiredDotNetInstalled(dotNetBasePath))
            {
                report.Valid = false;
                report.MissingDotNetFrameworkVersion = $"{MajorDotNetVersionRequired}.0 (x64)";
            }

            // Check for Visual C++ Redistributables
            var missingVCRedist = new List<string>();

            if (!isVCRedistx64Installed)
            {
                missingVCRedist.Add("2015-2022 (x64)");
            }

            if (missingVCRedist.Count > 0)
            {
                report.Valid = false;
                report.MissingVCRedistVersions = missingVCRedist;
            }

            return report;
        }

        /// <summary>
        /// Checks whether the required .NET Desktop Runtime is installed.
        /// </summary>
        internal static bool IsRequiredDotNetInstalled(string dotNetBasePath)
        {
            return DotNetRuntimeDetector.IsRequiredDesktopRuntimeInstalled(dotNetBasePath);
        }

        /// <summary>
        /// Gets all installed .NET components for the required major version.
        /// </summary>
        private static DotNetComponents GetInstalledRequiredDotNetComponents()
        {
            return GetInstalledRequiredDotNetComponents(DotNetBasePath);
        }

        internal static DotNetComponents GetInstalledRequiredDotNetComponents(string dotNetBasePath)
        {
            return DotNetRuntimeDetector.GetInstalledRequiredComponents(dotNetBasePath);
        }

        /// <summary>
        /// Gibt eine lesbare Beschreibung der installierten Komponenten zur�ck.
        /// </summary>
        public static string GetInstalledComponentsDescription()
        {
            var components = GetInstalledRequiredDotNetComponents();

            if (components == DotNetComponents.None)
                return $".NET {MajorDotNetVersionRequired} ist nicht installiert.";

            var installed = new List<string>();

            if (components.HasFlag(DotNetComponents.Runtime))
                installed.Add("Runtime");
            if (components.HasFlag(DotNetComponents.DesktopRuntime))
                installed.Add("Desktop Runtime");
            if (components.HasFlag(DotNetComponents.Sdk))
                installed.Add("SDK");

            return $".NET {MajorDotNetVersionRequired} installiert: {string.Join(", ", installed)}";
        }

        /// <summary>
        /// Checks if Visual C++ 2015-2022 Redistributable (x64) is installed.
        /// Registry key: HKLM\SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\x64
        /// </summary>
        private static bool IsVCRedistx64Installed()
        {
            try
            {
                using (var key = Registry.LocalMachine.OpenSubKey(VCRedistx64RegistryKey))
                {
                    if (key != null)
                    {
                        var installed = key.GetValue("Installed");
                        if (installed != null && Convert.ToInt32(installed) == 1)
                        {
                            return true;
                        }
                    }
                }
            }
            catch (Exception)
            {
                // Registry access failed, assume not installed
            }

            return false;
        }
    }
}
