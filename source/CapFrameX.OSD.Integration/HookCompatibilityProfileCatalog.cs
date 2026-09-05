using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Xml.Linq;
using Serilog;

namespace CapFrameX.OSD.Integration
{
    [Flags]
    internal enum NativeHookCompatibilityFlags : uint
    {
        None = 0,
        // V1 bits 0 and 4 were per-title DXGI lifetime switches. They stay vacant so an older
        // host/native pair cannot reinterpret a routing flag after the lifecycle became universal.
        EnableXeFgNativePresentQueueRoute = 1u << 1,
        EnableGenericD3D12PresentRoute = 1u << 2,
        DisableFidelityFxSwapchainLifecycleHooks = 1u << 3
    }

    internal sealed class HookCompatibilityProfile
    {
        internal HookCompatibilityProfile(string executableName,
            bool enableXeFgNativePresentQueueRoute,
            bool enableGenericD3D12PresentRoute,
            bool disableFidelityFxSwapchainLifecycleHooks,
            TimeSpan injectionDelay,
            string earlyInjectionModule, string source)
        {
            ExecutableName = executableName;
            EnableXeFgNativePresentQueueRoute = enableXeFgNativePresentQueueRoute;
            EnableGenericD3D12PresentRoute = enableGenericD3D12PresentRoute;
            DisableFidelityFxSwapchainLifecycleHooks =
                disableFidelityFxSwapchainLifecycleHooks;
            InjectionDelay = injectionDelay;
            EarlyInjectionModule = earlyInjectionModule;
            Source = source;
        }

        internal string ExecutableName { get; }
        internal bool EnableXeFgNativePresentQueueRoute { get; }
        internal bool EnableGenericD3D12PresentRoute { get; }
        internal bool DisableFidelityFxSwapchainLifecycleHooks { get; }
        internal TimeSpan InjectionDelay { get; }
        internal string EarlyInjectionModule { get; }
        internal bool RequiresEarlyInjection =>
            !string.IsNullOrWhiteSpace(EarlyInjectionModule);
        internal string Source { get; }

        internal NativeHookCompatibilityFlags NativeFlags
        {
            get
            {
                NativeHookCompatibilityFlags flags =
                    NativeHookCompatibilityFlags.None;
                if (EnableXeFgNativePresentQueueRoute)
                    flags |= NativeHookCompatibilityFlags.EnableXeFgNativePresentQueueRoute;
                if (EnableGenericD3D12PresentRoute)
                    flags |= NativeHookCompatibilityFlags.EnableGenericD3D12PresentRoute;
                if (DisableFidelityFxSwapchainLifecycleHooks)
                    flags |= NativeHookCompatibilityFlags.DisableFidelityFxSwapchainLifecycleHooks;
                return flags;
            }
        }
    }

    internal static class HookCompatibilityProfileCatalog
    {
        private const string ResourceName =
            "CapFrameX.OSD.Integration.HookCompatibilityProfiles.xml";

        private static readonly Lazy<IReadOnlyDictionary<string, HookCompatibilityProfile>>
            Profiles = new Lazy<IReadOnlyDictionary<string, HookCompatibilityProfile>>(
                LoadEmbeddedProfiles);

        internal static bool TryGet(string executablePathOrName,
            out HookCompatibilityProfile profile)
        {
            profile = null;
            string key = NormalizeExecutableName(executablePathOrName);
            return key != null && Profiles.Value.TryGetValue(key, out profile);
        }

        internal static bool TryGetForProcess(int processId,
            out HookCompatibilityProfile profile)
        {
            profile = null;
            if (processId <= 0) return false;

            try
            {
                using (var process = Process.GetProcessById(processId))
                    return TryGet(process.ProcessName, out profile);
            }
            catch (Exception ex) when (ex is ArgumentException ||
                                       ex is InvalidOperationException ||
                                       ex is System.ComponentModel.Win32Exception ||
                                       ex is NotSupportedException)
            {
                return false;
            }
        }

        internal static IReadOnlyList<HookCompatibilityProfile> GetEarlyInjectionProfiles()
        {
            var result = new List<HookCompatibilityProfile>();
            foreach (HookCompatibilityProfile profile in Profiles.Value.Values)
            {
                if (profile.RequiresEarlyInjection) result.Add(profile);
            }
            return result;
        }

        internal static IReadOnlyDictionary<string, HookCompatibilityProfile>
            ParseProfiles(Stream stream)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));

            XDocument document = XDocument.Load(stream, LoadOptions.SetLineInfo);
            XElement root = document.Root;
            if (root == null || root.Name != "HookCompatibilityProfiles" ||
                !int.TryParse((string)root.Attribute("version"),
                    NumberStyles.Integer, CultureInfo.InvariantCulture, out int version) ||
                version != 1)
            {
                throw new InvalidDataException("Unsupported hook compatibility profile format.");
            }

            var profiles = new Dictionary<string, HookCompatibilityProfile>(
                StringComparer.OrdinalIgnoreCase);
            foreach (XElement element in root.Elements("Profile"))
            {
                string executable = ((string)element.Attribute("executable"))?.Trim();
                string key = NormalizeExecutableName(executable);
                if (key == null)
                    throw new InvalidDataException("A hook compatibility profile has no executable name.");

                // V1 catalogs used these two attributes to opt individual games into what is now
                // the universal DXGI lifecycle. Parse them only to retain strict validation of an
                // older catalog, but never publish their retired protocol bits.
                ParseBooleanAttribute(element,
                    "disableDxgiSwapchainReleaseHook");
                bool enableXeFgNativePresentQueueRoute = ParseBooleanAttribute(element,
                    "enableXeFgNativePresentQueueRoute");
                bool enableGenericD3D12PresentRoute = ParseBooleanAttribute(element,
                    "enableGenericD3D12PresentRoute");
                bool disableFidelityFxSwapchainLifecycleHooks =
                    ParseBooleanAttribute(element,
                        "disableFidelityFxSwapchainLifecycleHooks");
                ParseBooleanAttribute(element,
                    "enableDxgiFactorySwapchainLifecycleHooks");
                string earlyInjectionModule = ParseModuleNameAttribute(element,
                    "earlyInjectionModule");
                int delayMilliseconds = ParseNonNegativeIntegerAttribute(element,
                    "injectionDelayMilliseconds");
                if (!enableXeFgNativePresentQueueRoute &&
                    !enableGenericD3D12PresentRoute &&
                    !disableFidelityFxSwapchainLifecycleHooks &&
                    delayMilliseconds == 0 &&
                    earlyInjectionModule == null)
                    throw new InvalidDataException($"Profile '{executable}' has no compatibility settings.");

                var profile = new HookCompatibilityProfile(
                    Path.GetFileName(executable),
                    enableXeFgNativePresentQueueRoute,
                    enableGenericD3D12PresentRoute,
                    disableFidelityFxSwapchainLifecycleHooks,
                    TimeSpan.FromMilliseconds(delayMilliseconds), earlyInjectionModule,
                    ((string)element.Attribute("source"))?.Trim());
                if (profiles.ContainsKey(key))
                    throw new InvalidDataException($"Duplicate hook compatibility profile '{executable}'.");
                profiles.Add(key, profile);
            }

            return profiles;
        }

        private static IReadOnlyDictionary<string, HookCompatibilityProfile>
            LoadEmbeddedProfiles()
        {
            try
            {
                using (Stream stream = typeof(HookCompatibilityProfileCatalog)
                    .GetTypeInfo().Assembly.GetManifestResourceStream(ResourceName))
                {
                    if (stream == null)
                        throw new InvalidDataException(
                            $"Embedded compatibility profile resource '{ResourceName}' is missing.");
                    return ParseProfiles(stream);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "HookOverlay: failed to load compatibility profiles");
                return new Dictionary<string, HookCompatibilityProfile>(
                    StringComparer.OrdinalIgnoreCase);
            }
        }

        private static string NormalizeExecutableName(string executablePathOrName)
        {
            if (string.IsNullOrWhiteSpace(executablePathOrName)) return null;
            try
            {
                string fileName = Path.GetFileName(executablePathOrName.Trim());
                string key = Path.GetFileNameWithoutExtension(fileName);
                return string.IsNullOrWhiteSpace(key) ? null : key;
            }
            catch (ArgumentException)
            {
                return null;
            }
        }

        private static bool ParseBooleanAttribute(XElement element, string attributeName)
        {
            XAttribute attribute = element.Attribute(attributeName);
            if (attribute == null) return false;
            if (bool.TryParse(attribute.Value, out bool value)) return value;
            throw new InvalidDataException(
                $"Profile '{(string)element.Attribute("executable")}' has invalid {attributeName}.");
        }

        private static string ParseModuleNameAttribute(XElement element,
            string attributeName)
        {
            string value = ((string)element.Attribute(attributeName))?.Trim();
            if (string.IsNullOrWhiteSpace(value)) return null;
            if (!string.Equals(value, Path.GetFileName(value),
                    StringComparison.Ordinal) ||
                !string.Equals(Path.GetExtension(value), ".dll",
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    $"Profile '{(string)element.Attribute("executable")}' has invalid {attributeName}.");
            }
            return value;
        }

        private static int ParseNonNegativeIntegerAttribute(XElement element,
            string attributeName)
        {
            XAttribute attribute = element.Attribute(attributeName);
            if (attribute == null) return 0;
            if (int.TryParse(attribute.Value, NumberStyles.Integer,
                    CultureInfo.InvariantCulture, out int value) && value >= 0)
                return value;
            throw new InvalidDataException(
                $"Profile '{(string)element.Attribute("executable")}' has invalid {attributeName}.");
        }
    }
}
