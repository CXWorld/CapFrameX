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
        DisableDxgiSwapchainReleaseHook = 1u << 0
    }

    internal sealed class HookCompatibilityProfile
    {
        internal HookCompatibilityProfile(string executableName,
            bool disableDxgiSwapchainReleaseHook, TimeSpan injectionDelay,
            string source)
        {
            ExecutableName = executableName;
            DisableDxgiSwapchainReleaseHook = disableDxgiSwapchainReleaseHook;
            InjectionDelay = injectionDelay;
            Source = source;
        }

        internal string ExecutableName { get; }
        internal bool DisableDxgiSwapchainReleaseHook { get; }
        internal TimeSpan InjectionDelay { get; }
        internal string Source { get; }

        internal NativeHookCompatibilityFlags NativeFlags =>
            DisableDxgiSwapchainReleaseHook
                ? NativeHookCompatibilityFlags.DisableDxgiSwapchainReleaseHook
                : NativeHookCompatibilityFlags.None;
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

                bool disableRelease = ParseBooleanAttribute(element,
                    "disableDxgiSwapchainReleaseHook");
                int delayMilliseconds = ParseNonNegativeIntegerAttribute(element,
                    "injectionDelayMilliseconds");
                if (!disableRelease && delayMilliseconds == 0)
                    throw new InvalidDataException($"Profile '{executable}' has no compatibility settings.");

                var profile = new HookCompatibilityProfile(
                    Path.GetFileName(executable), disableRelease,
                    TimeSpan.FromMilliseconds(delayMilliseconds),
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
