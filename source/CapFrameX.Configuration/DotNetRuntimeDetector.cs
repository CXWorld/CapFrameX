using System;
using System.IO;

namespace CapFrameX.Configuration
{
    [Flags]
    public enum DotNetComponents
    {
        None = 0,
        Runtime = 1,
        DesktopRuntime = 2,
        Sdk = 4
    }

    /// <summary>
    /// Detects stable, system-wide x64 .NET installations. This file is also compiled into the
    /// installer custom-action assembly so setup can perform the check before CapFrameX starts.
    /// </summary>
    internal static class DotNetRuntimeDetector
    {
        internal const int RequiredMajorVersion = 10;

        private const string DotNetRuntimeMarker = "System.Private.CoreLib.dll";
        private const string DesktopRuntimeMarker = "PresentationFramework.dll";
        private const string SdkMarker = "dotnet.dll";

        internal static string GetSystemWideDotNetBasePath()
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "dotnet");
        }

        internal static bool IsRequiredDesktopRuntimeInstalled(string dotNetBasePath)
        {
            const DotNetComponents requiredComponents =
                DotNetComponents.Runtime | DotNetComponents.DesktopRuntime;
            var installedComponents = GetInstalledRequiredComponents(dotNetBasePath);

            return (installedComponents & requiredComponents) == requiredComponents;
        }

        internal static DotNetComponents GetInstalledRequiredComponents(string dotNetBasePath)
        {
            var result = DotNetComponents.None;
            var sharedFrameworkPath = Path.Combine(dotNetBasePath, "shared");
            var dotNetRuntimePath = Path.Combine(sharedFrameworkPath, "Microsoft.NETCore.App");
            var dotNetDesktopRuntimePath = Path.Combine(sharedFrameworkPath, "Microsoft.WindowsDesktop.App");
            var dotNetSdkPath = Path.Combine(dotNetBasePath, "sdk");

            if (IsCompatibleComponentInstalled(dotNetRuntimePath, DotNetRuntimeMarker))
            {
                result |= DotNetComponents.Runtime;
            }

            if (IsCompatibleComponentInstalled(dotNetDesktopRuntimePath, DesktopRuntimeMarker))
            {
                result |= DotNetComponents.DesktopRuntime;
            }

            if (IsCompatibleComponentInstalled(dotNetSdkPath, SdkMarker))
            {
                result |= DotNetComponents.Sdk;
            }

            return result;
        }

        private static bool IsCompatibleComponentInstalled(string componentPath, string markerFileName)
        {
            try
            {
                if (!Directory.Exists(componentPath))
                {
                    return false;
                }

                foreach (var versionDirectory in Directory.GetDirectories(componentPath))
                {
                    var folderName = Path.GetFileName(versionDirectory);
                    if (Version.TryParse(folderName, out var installedVersion) &&
                        installedVersion.Major == RequiredMajorVersion &&
                        File.Exists(Path.Combine(versionDirectory, markerFileName)))
                    {
                        return true;
                    }
                }
            }
            catch (Exception)
            {
                // File system access failed; a dependency check must fail closed.
            }

            return false;
        }
    }
}
