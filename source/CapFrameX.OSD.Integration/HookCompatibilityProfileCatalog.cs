using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
        internal const string FileName = "HookCompatibilityProfiles.xml";
        private const string ResourceName =
            "CapFrameX.OSD.Integration.HookCompatibilityProfiles.xml";

        private static readonly Lazy<IReadOnlyDictionary<string, HookCompatibilityProfile>>
            Profiles = new Lazy<IReadOnlyDictionary<string, HookCompatibilityProfile>>(
                LoadEmbeddedProfiles);

        internal static bool TryGet(string executablePathOrName,
            out HookCompatibilityProfile profile,
            IReadOnlyDictionary<string, HookCompatibilityProfile> profiles = null)
        {
            profile = null;
            string key = NormalizeExecutableName(executablePathOrName);
            return key != null && (profiles ?? Profiles.Value).TryGetValue(key, out profile);
        }

        internal static bool TryGetForProcess(int processId,
            out HookCompatibilityProfile profile,
            IReadOnlyDictionary<string, HookCompatibilityProfile> profiles = null)
        {
            profile = null;
            if (processId <= 0) return false;

            try
            {
                using (var process = Process.GetProcessById(processId))
                    return TryGet(process.ProcessName, out profile, profiles);
            }
            catch (Exception ex) when (ex is ArgumentException ||
                                       ex is InvalidOperationException ||
                                       ex is System.ComponentModel.Win32Exception ||
                                       ex is NotSupportedException)
            {
                return false;
            }
        }

        internal static IReadOnlyList<HookCompatibilityProfile> GetEarlyInjectionProfiles(
            IReadOnlyDictionary<string, HookCompatibilityProfile> profiles = null)
        {
            var result = new List<HookCompatibilityProfile>();
            foreach (HookCompatibilityProfile profile in (profiles ?? Profiles.Value).Values)
            {
                if (profile.RequiresEarlyInjection) result.Add(profile);
            }
            return result;
        }

        /// <summary>
        /// A startup snapshot: external profiles replace entire matching embedded profiles,
        /// and user profiles take precedence over profiles shipped beside the executable.
        /// </summary>
        internal static IReadOnlyDictionary<string, HookCompatibilityProfile> LoadProfiles(
            string applicationProfilesPath = null, string userProfilesPath = null)
        {
            var profiles = new Dictionary<string, HookCompatibilityProfile>(
                StringComparer.OrdinalIgnoreCase);
            foreach (var entry in Profiles.Value)
                profiles.Add(entry.Key, entry.Value);

            EnsureUserProfilesFile(userProfilesPath);

            foreach (string path in new[] { applicationProfilesPath, userProfilesPath })
            {
                if (string.IsNullOrWhiteSpace(path)) continue;
                try
                {
                    IReadOnlyDictionary<string, HookCompatibilityProfile> external;
                    using (Stream stream = File.OpenRead(path))
                        external = ParseProfiles(stream);

                    // Parse the whole source before merging, so an invalid entry cannot leave
                    // only part of a file applied. Omitted attributes use the external profile's
                    // defaults rather than inheriting flags or timing from the replaced profile.
                    foreach (var entry in external)
                        profiles[entry.Key] = entry.Value;
                    Log.Information(
                        "HookOverlay: loaded {count} external compatibility profiles from {path}",
                        external.Count, path);
                }
                catch (Exception ex) when (ex is FileNotFoundException || ex is DirectoryNotFoundException)
                {
                    // Both external sources are optional.
                }
                catch (Exception ex)
                {
                    Log.Warning(ex,
                        "HookOverlay: ignoring external compatibility profiles at {path}; keeping profiles from the other sources",
                        path);
                }
            }

            return new ReadOnlyDictionary<string, HookCompatibilityProfile>(profiles);
        }

        private static void EnsureUserProfilesFile(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || File.Exists(path)) return;

            string temporaryPath = null;
            try
            {
                string targetPath = Path.GetFullPath(path);
                string directory = Path.GetDirectoryName(targetPath);
                Directory.CreateDirectory(directory);
                temporaryPath = Path.Combine(directory,
                    $".{FileName}.{Guid.NewGuid():N}.tmp");
                var template = new XDocument(
                    new XDeclaration("1.0", "utf-8", null),
                    new XElement("HookCompatibilityProfiles", new XAttribute("version", 1),
                        new XComment(" Add custom Profile elements here. Restart CapFrameX and the game after editing. ")));
                using (var stream = new FileStream(temporaryPath, FileMode.CreateNew,
                    FileAccess.Write, FileShare.None))
                    template.Save(stream);

                try
                {
                    // Publish a complete file, and never replace an existing user file even if
                    // another CapFrameX instance created it after the check above.
                    File.Move(temporaryPath, targetPath, overwrite: false);
                    Log.Information("HookOverlay: created empty user compatibility profiles at {path}",
                        targetPath);
                }
                catch (IOException) when (File.Exists(targetPath))
                {
                    // Another instance or the user supplied a file first; keep its contents.
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "HookOverlay: could not create user compatibility profiles at {path}",
                    path);
            }
            finally
            {
                if (temporaryPath != null)
                {
                    try { File.Delete(temporaryPath); }
                    catch (Exception ex)
                    {
                        Log.Warning(ex,
                            "HookOverlay: could not remove temporary compatibility profile file {path}",
                            temporaryPath);
                    }
                }
            }
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
