using Microsoft.Win32;
using System;
using System.Collections.Generic;

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
        public const int MajorDotNetVersionRequired = 9;

        // Registry keys for VC++ 2015-2022 Redistributable detection (from Bundle.wxs)
        private const string VCRedistx64RegistryKey = @"SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\x64";

        // Registry key for .NET 9.0 detection
        private const string DotNetSharedHostRegistryKey = @"SOFTWARE\dotnet\Setup\InstalledVersions\x64\sharedhost";

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

            // Check for .NET 9.0
            if (!IsDotNet9Installed())
            {
                report.Valid = false;
                report.MissingDotNetFrameworkVersion = "9.0 (x64)";
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
        /// Checks if .NET 9.0 is installed by checking the sharedhost Version property.
        /// </summary>
        private static bool IsDotNet9Installed()
        {
            try
            {
                // Check sharedhost Version property
                // e.g., HKLM\SOFTWARE\dotnet\Setup\InstalledVersions\x64\sharedhost with Version = 9.0.11
                using (var key = Registry.LocalMachine.OpenSubKey(DotNetSharedHostRegistryKey))
                {
                    if (key != null)
                    {
                        var version = key.GetValue("Version") as string;

                        // parse version for checking major version
                        int majorVersion = 0;
                        if (version != null)
                        {
                            var versionParts = version.Split('.');
                            if (versionParts.Length > 0)
                            {
                                int.TryParse(versionParts[0], out majorVersion);
                            }
                        }

                        if (!string.IsNullOrEmpty(version) && majorVersion >= MajorDotNetVersionRequired)
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
