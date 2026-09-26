using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace CapFrameX.Overlay
{
    /// <summary>
    /// Registry locations the Vulkan loader reads implicit layers from.
    /// </summary>
    public enum VulkanLayerRegistryLocation
    {
        /// <summary>HKLM, 64-bit view: read by 64-bit processes.</summary>
        Machine64,

        /// <summary>HKLM, 32-bit view (WOW6432Node): read by 32-bit processes.</summary>
        Machine32,

        /// <summary>HKCU: read by processes of both bitnesses, but ignored for elevated ones.</summary>
        CurrentUser
    }

    /// <summary>
    /// Bitness of a layer DLL, read from its PE header.
    /// </summary>
    public enum VulkanLayerLibraryBitness
    {
        Missing,
        Unknown,
        X86,
        X64
    }

    public enum VulkanLayerRegistrationState
    {
        /// <summary>The registry could not be read.</summary>
        Unknown,

        /// <summary>Neither bitness has a registration.</summary>
        NotRegistered,

        /// <summary>Both bitnesses resolve to a DLL they can load.</summary>
        Registered,

        /// <summary>One bitness works, the other has no registration.</summary>
        Incomplete,

        /// <summary>A registration the loader cannot use shadows the layer for at least one bitness.</summary>
        Conflicting
    }

    /// <summary>
    /// Outcome of checking the CapFrameX implicit layer registration.
    /// </summary>
    public sealed class VulkanLayerRegistrationStatus
    {
        public VulkanLayerRegistrationStatus(VulkanLayerRegistrationState state, string version, string detail)
        {
            State = state;
            Version = version;
            Detail = detail;
        }

        public VulkanLayerRegistrationState State { get; }

        /// <summary>
        /// implementation_version of the working manifest; null when none works.
        /// </summary>
        public string Version { get; }

        /// <summary>
        /// Registered manifests per bitness and, for a conflict, what is wrong with them.
        /// </summary>
        public string Detail { get; }
    }

    /// <summary>
    /// One registry value that registers a CapFrameX layer manifest.
    /// </summary>
    public sealed class VulkanLayerRegistryEntry
    {
        public VulkanLayerRegistryEntry(VulkanLayerRegistryLocation location, string manifestPath, bool isEnabled,
            bool manifestReadable, VulkanLayerLibraryBitness libraryBitness, string implementationVersion)
        {
            Location = location;
            ManifestPath = manifestPath;
            IsEnabled = isEnabled;
            ManifestReadable = manifestReadable;
            LibraryBitness = libraryBitness;
            ImplementationVersion = implementationVersion;
        }

        public VulkanLayerRegistryLocation Location { get; }

        public string ManifestPath { get; }

        /// <summary>
        /// The loader only reads manifests whose DWORD value is 0.
        /// </summary>
        public bool IsEnabled { get; }

        /// <summary>
        /// False for a leftover entry whose manifest is gone; the loader skips those.
        /// </summary>
        public bool ManifestReadable { get; }

        public VulkanLayerLibraryBitness LibraryBitness { get; }

        public string ImplementationVersion { get; }
    }

    /// <summary>
    /// Checks the registration of the CapFrameX implicit Vulkan layer the way the loader sees it.
    /// <para>
    /// The loader identifies a layer by the NAME inside its manifest. A 64-bit process reads the
    /// 64-bit HKLM view plus HKCU, a 32-bit process the 32-bit view plus HKCU; a manifest it
    /// reaches whose DLL it cannot load does not merely fail, it shadows the working registration
    /// and disables the layer for that bitness. HKCU is not split by bitness, so an entry there
    /// always breaks one of the two. All of that is silent and looks exactly like a game without
    /// Vulkan, which is why the Info tab reports it.
    /// </para>
    /// </summary>
    public static class VulkanLayerRegistrationProbe
    {
        public const string LayerName = "VK_LAYER_CAPFRAMEX_overlay";

        private const string ImplicitLayersKey = @"SOFTWARE\Khronos\Vulkan\ImplicitLayers";

        // Every manifest CapFrameX has shipped is named cfx_osd_vklayer*.json (see the installer's
        // purge). Matching the file name as well finds leftovers whose manifest is already gone.
        private const string ManifestPrefix = "cfx_osd_vklayer";

        private static readonly JsonDocumentOptions ManifestJsonOptions = new JsonDocumentOptions
        {
            AllowTrailingCommas = true,
            CommentHandling = JsonCommentHandling.Skip
        };

        public static VulkanLayerRegistrationStatus Query()
        {
            try
            {
                var entries = new List<VulkanLayerRegistryEntry>();
                ReadEntries(entries, RegistryHive.LocalMachine, RegistryView.Registry64, VulkanLayerRegistryLocation.Machine64);
                ReadEntries(entries, RegistryHive.LocalMachine, RegistryView.Registry32, VulkanLayerRegistryLocation.Machine32);
                ReadEntries(entries, RegistryHive.CurrentUser, RegistryView.Default, VulkanLayerRegistryLocation.CurrentUser);
                return Classify(entries);
            }
            catch (Exception ex)
            {
                return new VulkanLayerRegistrationStatus(VulkanLayerRegistrationState.Unknown, null, ex.Message);
            }
        }

        /// <summary>
        /// Applies the loader's rules to the registrations of the CapFrameX layer.
        /// </summary>
        internal static VulkanLayerRegistrationStatus Classify(IReadOnlyCollection<VulkanLayerRegistryEntry> entries)
        {
            var x64 = Evaluate(entries, VulkanLayerRegistryLocation.Machine64, VulkanLayerLibraryBitness.X64);
            var x86 = Evaluate(entries, VulkanLayerRegistryLocation.Machine32, VulkanLayerLibraryBitness.X86);

            var detail = new StringBuilder();
            AppendDetail(detail, "x64", x64);
            AppendDetail(detail, "x86", x86);

            foreach (var stale in entries.Where(entry => entry.IsEnabled && !entry.ManifestReadable))
                detail.AppendLine($"Leftover registration without manifest: {stale.ManifestPath}");

            VulkanLayerRegistrationState state;
            if (x64.Broken != null || x86.Broken != null)
                state = VulkanLayerRegistrationState.Conflicting;
            else if (x64.Working != null && x86.Working != null)
                state = VulkanLayerRegistrationState.Registered;
            else if (x64.Working != null || x86.Working != null)
                state = VulkanLayerRegistrationState.Incomplete;
            else
                state = VulkanLayerRegistrationState.NotRegistered;

            string version = (x64.Working ?? x86.Working)?.ImplementationVersion;
            return new VulkanLayerRegistrationStatus(state, version, detail.ToString().TrimEnd());
        }

        private sealed class BitnessVerdict
        {
            public VulkanLayerRegistryEntry Working;
            public VulkanLayerRegistryEntry Broken;
            public string BrokenReason;
        }

        private static BitnessVerdict Evaluate(IEnumerable<VulkanLayerRegistryEntry> entries,
            VulkanLayerRegistryLocation machineView, VulkanLayerLibraryBitness bitness)
        {
            var verdict = new BitnessVerdict();

            var visible = entries.Where(entry => entry.IsEnabled && entry.ManifestReadable
                && (entry.Location == machineView || entry.Location == VulkanLayerRegistryLocation.CurrentUser));

            foreach (var entry in visible)
            {
                string reason = GetBreakage(entry, bitness);
                if (reason == null)
                {
                    verdict.Working = verdict.Working ?? entry;
                }
                else if (verdict.Broken == null)
                {
                    verdict.Broken = entry;
                    verdict.BrokenReason = reason;
                }
            }

            return verdict;
        }

        /// <summary>
        /// Why a process of the given bitness cannot use this registration; null if it can. An
        /// unreadable PE header is given the benefit of the doubt rather than reported as a conflict.
        /// </summary>
        private static string GetBreakage(VulkanLayerRegistryEntry entry, VulkanLayerLibraryBitness bitness)
        {
            string where = entry.Location == VulkanLayerRegistryLocation.CurrentUser ? "registered in HKCU, " : string.Empty;

            switch (entry.LibraryBitness)
            {
                case VulkanLayerLibraryBitness.Missing:
                    return where + "layer DLL is missing";
                case VulkanLayerLibraryBitness.X86 when bitness == VulkanLayerLibraryBitness.X64:
                    return where + "points to a 32-bit DLL";
                case VulkanLayerLibraryBitness.X64 when bitness == VulkanLayerLibraryBitness.X86:
                    return where + "points to a 64-bit DLL";
                default:
                    return null;
            }
        }

        private static void AppendDetail(StringBuilder detail, string label, BitnessVerdict verdict)
        {
            if (verdict.Broken != null)
                detail.AppendLine($"{label}: {verdict.Broken.ManifestPath} ({verdict.BrokenReason})");
            else if (verdict.Working != null)
                detail.AppendLine($"{label}: {verdict.Working.ManifestPath}");
            else
                detail.AppendLine($"{label}: not registered");
        }

        private static void ReadEntries(List<VulkanLayerRegistryEntry> entries, RegistryHive hive, RegistryView view,
            VulkanLayerRegistryLocation location)
        {
            using (var baseKey = RegistryKey.OpenBaseKey(hive, view))
            using (var layers = baseKey.OpenSubKey(ImplicitLayersKey))
            {
                if (layers == null)
                    return;

                foreach (string manifestPath in layers.GetValueNames())
                {
                    bool manifestReadable = TryReadManifest(manifestPath, out string layerName,
                        out string libraryPath, out string implementationVersion);

                    bool isOurs = manifestReadable
                        ? string.Equals(layerName, LayerName, StringComparison.Ordinal)
                        : IsCapFrameXManifestName(manifestPath);

                    if (!isOurs)
                        continue;

                    bool isEnabled = layers.GetValue(manifestPath) is int flag && flag == 0;
                    var bitness = manifestReadable ? ReadLibraryBitness(libraryPath) : VulkanLayerLibraryBitness.Missing;

                    entries.Add(new VulkanLayerRegistryEntry(location, manifestPath, isEnabled,
                        manifestReadable, bitness, implementationVersion));
                }
            }
        }

        private static bool IsCapFrameXManifestName(string manifestPath)
        {
            try
            {
                return Path.GetFileName(manifestPath).StartsWith(ManifestPrefix, StringComparison.OrdinalIgnoreCase);
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        /// <summary>
        /// Reads name, library path and implementation version of the CapFrameX layer from a
        /// manifest. Handles both the single "layer" object and the "layers" array form; for any
        /// other manifest the name of its (first) layer is returned so the caller can skip it.
        /// </summary>
        private static bool TryReadManifest(string manifestPath, out string layerName,
            out string libraryPath, out string implementationVersion)
        {
            layerName = null;
            libraryPath = null;
            implementationVersion = null;

            try
            {
                if (!File.Exists(manifestPath))
                    return false;

                using (var document = JsonDocument.Parse(File.ReadAllText(manifestPath), ManifestJsonOptions))
                {
                    var root = document.RootElement;
                    var layers = new List<JsonElement>();

                    if (root.TryGetProperty("layer", out JsonElement single))
                        layers.Add(single);
                    if (root.TryGetProperty("layers", out JsonElement array) && array.ValueKind == JsonValueKind.Array)
                        layers.AddRange(array.EnumerateArray());

                    JsonElement? layer = layers.Cast<JsonElement?>()
                        .FirstOrDefault(candidate => GetString(candidate.Value, "name") == LayerName)
                        ?? layers.Cast<JsonElement?>().FirstOrDefault();

                    if (layer == null)
                        return true;

                    layerName = GetString(layer.Value, "name");
                    implementationVersion = GetString(layer.Value, "implementation_version");

                    string library = GetString(layer.Value, "library_path");
                    if (!string.IsNullOrWhiteSpace(library))
                    {
                        libraryPath = Path.IsPathRooted(library)
                            ? library
                            : Path.GetFullPath(Path.Combine(Path.GetDirectoryName(manifestPath) ?? string.Empty, library));
                    }

                    return true;
                }
            }
            catch (Exception)
            {
                // Unparsable JSON: the loader skips such a manifest as well.
                return false;
            }
        }

        private static string GetString(JsonElement element, string property)
            => element.ValueKind == JsonValueKind.Object
               && element.TryGetProperty(property, out JsonElement value)
               && value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null;

        /// <summary>
        /// Reads the machine type from a DLL's PE header.
        /// </summary>
        internal static VulkanLayerLibraryBitness ReadLibraryBitness(string libraryPath)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(libraryPath) || !File.Exists(libraryPath))
                    return VulkanLayerLibraryBitness.Missing;

                using (var stream = File.OpenRead(libraryPath))
                using (var reader = new BinaryReader(stream))
                {
                    if (stream.Length < 0x40)
                        return VulkanLayerLibraryBitness.Unknown;

                    stream.Position = 0x3C;
                    int peOffset = reader.ReadInt32();
                    if (peOffset <= 0 || peOffset + 6 > stream.Length)
                        return VulkanLayerLibraryBitness.Unknown;

                    stream.Position = peOffset;
                    if (reader.ReadUInt32() != 0x00004550) // "PE\0\0"
                        return VulkanLayerLibraryBitness.Unknown;

                    switch (reader.ReadUInt16())
                    {
                        case 0x8664:
                            return VulkanLayerLibraryBitness.X64;
                        case 0x014C:
                            return VulkanLayerLibraryBitness.X86;
                        default:
                            return VulkanLayerLibraryBitness.Unknown;
                    }
                }
            }
            catch (IOException)
            {
                return VulkanLayerLibraryBitness.Unknown;
            }
            catch (UnauthorizedAccessException)
            {
                return VulkanLayerLibraryBitness.Unknown;
            }
        }
    }
}
