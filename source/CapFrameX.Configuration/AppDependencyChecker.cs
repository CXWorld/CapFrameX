using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;

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

    [Flags]
    public enum DotNetComponents
    {
        None = 0,
        Runtime = 1,
        DesktopRuntime = 2,
        Sdk = 4
    }

    /// <summary>
    /// Checks for required runtime dependencies in portable mode.
    /// </summary>
    public static class AppDependencyChecker
    {
        public const int MajorDotNetVersionRequired = 10;

        // Registry keys for VC++ 2015-2022 Redistributable detection (from Bundle.wxs)
        private const string VCRedistx64RegistryKey = @"SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\x64";

        // File system paths for .NET installation detection
        private static readonly string DotNetBasePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "dotnet");
        private static readonly string DotNetRuntimePath = Path.Combine(DotNetBasePath, "shared", "Microsoft.NETCore.App");
        private static readonly string DotNetDesktopRuntimePath = Path.Combine(DotNetBasePath, "shared", "Microsoft.WindowsDesktop.App");
        private static readonly string DotNetSdkPath = Path.Combine(DotNetBasePath, "sdk");

        /// <summary>
        /// Checks for missing dependencies and returns a report.
        /// </summary>
        /// <returns>A report containing information about missing dependencies.</returns>
        public static DependencyCheckReport CheckAndNotifyMissingDependencies()
        {
            var report = new DependencyCheckReport
            {
                Valid = true
            };

            // Check for the required .NET runtime.
            if (!IsRequiredDotNetInstalled())
            {
                report.Valid = false;
                report.MissingDotNetFrameworkVersion = $"{MajorDotNetVersionRequired}.0 (x64)";
            }

            // Check for Visual C++ Redistributables
            var missingVCRedist = new List<string>();

            if (!IsVCRedistx64Installed())
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
        /// Checks whether at least one component of the required .NET version is installed.
        /// </summary>
        private static bool IsRequiredDotNetInstalled()
        {
            return GetInstalledRequiredDotNetComponents() != DotNetComponents.None;
        }

        /// <summary>
        /// Gets all installed .NET components for the required major version.
        /// </summary>
        private static DotNetComponents GetInstalledRequiredDotNetComponents()
        {
            var result = DotNetComponents.None;

            if (IsDotNetComponentInstalled(DotNetRuntimePath, MajorDotNetVersionRequired))
            {
                result |= DotNetComponents.Runtime;
            }

            if (IsDotNetComponentInstalled(DotNetDesktopRuntimePath, MajorDotNetVersionRequired))
            {
                result |= DotNetComponents.DesktopRuntime;
            }

            if (IsDotNetComponentInstalled(DotNetSdkPath, MajorDotNetVersionRequired))
            {
                result |= DotNetComponents.Sdk;
            }

            return result;
        }

        /// <summary>
        /// Checks if a .NET component is installed at the specified path with at least the required major version.
        /// Looks for version folders (e.g., "10.0.0", "10.0.1") in the component directory.
        /// </summary>
        private static bool IsDotNetComponentInstalled(string componentPath, int requiredMajorVersion)
        {
            try
            {
                if (!Directory.Exists(componentPath))
                    return false;

                var versionDirectories = Directory.GetDirectories(componentPath);

                foreach (var versionDir in versionDirectories)
                {
                    var folderName = Path.GetFileName(versionDir);
                    if (TryParseMajorVersion(folderName, out int majorVersion) &&
                        majorVersion >= requiredMajorVersion)
                    {
                        return true;
                    }
                }
            }
            catch (Exception)
            {
                // File system access failed
            }

            return false;
        }

        /// <summary>
        /// Tries to parse the major version from a version string (e.g., "10.0.0" -> 10).
        /// </summary>
        private static bool TryParseMajorVersion(string version, out int majorVersion)
        {
            majorVersion = 0;

            if (string.IsNullOrEmpty(version))
                return false;

            var versionParts = version.Split('.');
            return versionParts.Length > 0 && int.TryParse(versionParts[0], out majorVersion);
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
