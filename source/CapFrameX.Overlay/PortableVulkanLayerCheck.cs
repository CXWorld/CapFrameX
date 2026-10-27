using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace CapFrameX.Overlay
{
    public enum PortableVulkanLayerAction
    {
        /// <summary>Games of both bitnesses load the layers of this folder or identical files.</summary>
        None,

        /// <summary>No CapFrameX layer is registered for either bitness.</summary>
        Install,

        /// <summary>A bitness has no registration, loads other layer files, or has a registration that cannot work.</summary>
        Update
    }

    /// <summary>
    /// One bitness of the layer pair staged beside the application.
    /// </summary>
    public sealed class VulkanLayerPayload
    {
        public VulkanLayerPayload(string manifestPath, string libraryHash)
        {
            ManifestPath = manifestPath;
            LibraryHash = libraryHash;
        }

        public string ManifestPath { get; }

        /// <summary>
        /// SHA-256 of the layer DLL, so that a registration of identical files elsewhere (an
        /// installed CapFrameX of the same build) needs no change.
        /// </summary>
        public string LibraryHash { get; }
    }

    public sealed class PortableVulkanLayerAssessment
    {
        public PortableVulkanLayerAssessment(PortableVulkanLayerAction action, string signature, string detail)
        {
            Action = action;
            Signature = signature;
            Detail = detail;
        }

        public PortableVulkanLayerAction Action { get; }

        /// <summary>
        /// Identifies the layers of this folder and the registrations the loader reads, so that a
        /// declined prompt only comes back once one of them changes.
        /// </summary>
        public string Signature { get; }

        /// <summary>
        /// The registrations per bitness, as the Info tab lists them.
        /// </summary>
        public string Detail { get; }
    }

    /// <summary>
    /// Decides whether the portable CapFrameX has to register the Vulkan layers it ships. Portable
    /// extraction writes no registry values, so without a registration Vulkan games show no in-game
    /// overlay, and a registration of another CapFrameX folder loads layer files that may not match
    /// this build. The registration itself is done by <see cref="ScriptFileName"/>, staged beside
    /// the x64 layer.
    /// </summary>
    public static class PortableVulkanLayerCheck
    {
        public const string ScriptFileName = "Register-CapFrameXVulkanLayer.ps1";

        private const string LayerFolderName = "vulkan";
        private const string ManifestFileName = "cfx_osd_vklayer_v1.json";

        public static string GetScriptPath(string appDirectory)
            => Path.Combine(appDirectory, LayerFolderName, ScriptFileName);

        /// <summary>
        /// Compares the layers staged in <paramref name="appDirectory"/> with the registry. Returns
        /// null, with the reason in <paramref name="problem"/>, when the folder has no usable pair.
        /// </summary>
        public static PortableVulkanLayerAssessment Assess(string appDirectory, out string problem)
        {
            string folder = Path.Combine(appDirectory, LayerFolderName);
            var x64 = ReadPayload(folder, VulkanLayerLibraryBitness.X64, out problem);
            if (x64 == null)
                return null;

            var x86 = ReadPayload(Path.Combine(folder, "x86"), VulkanLayerLibraryBitness.X86, out problem);
            if (x86 == null)
                return null;

            return Assess(VulkanLayerRegistrationProbe.ReadRegistrations(), x64, x86, ComputeFileHash);
        }

        internal static PortableVulkanLayerAssessment Assess(IReadOnlyCollection<VulkanLayerRegistryEntry> entries,
            VulkanLayerPayload x64, VulkanLayerPayload x86, Func<string, string> hashOf)
        {
            var hashes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            string HashOf(string path)
            {
                if (string.IsNullOrEmpty(path))
                    return null;
                if (!hashes.TryGetValue(path, out string hash))
                    hashes[path] = hash = hashOf(path);
                return hash;
            }

            var visible64 = VulkanLayerRegistrationProbe.VisibleTo(entries, VulkanLayerLibraryBitness.X64).ToList();
            var visible86 = VulkanLayerRegistrationProbe.VisibleTo(entries, VulkanLayerLibraryBitness.X86).ToList();

            PortableVulkanLayerAction action;
            if (visible64.Count == 0 && visible86.Count == 0)
                action = PortableVulkanLayerAction.Install;
            else if (LoadsPayload(visible64, x64, HashOf) && LoadsPayload(visible86, x86, HashOf))
                action = PortableVulkanLayerAction.None;
            else
                action = PortableVulkanLayerAction.Update;

            var signature = new StringBuilder()
                .Append(x64.ManifestPath).Append('|').Append(x64.LibraryHash).Append('\n')
                .Append(x86.ManifestPath).Append('|').Append(x86.LibraryHash).Append('\n');

            foreach (var entry in visible64.Concat(visible86).Distinct()
                .OrderBy(entry => entry.Location)
                .ThenBy(entry => entry.ManifestPath, StringComparer.OrdinalIgnoreCase))
            {
                signature.Append(entry.Location).Append('|').Append(entry.ManifestPath)
                    .Append('|').Append(HashOf(entry.LibraryPath)).Append('\n');
            }

            return new PortableVulkanLayerAssessment(action, Sha256Hex(Encoding.UTF8.GetBytes(signature.ToString())),
                VulkanLayerRegistrationProbe.Classify(entries).Detail);
        }

        /// <summary>
        /// Every registration a process of this bitness reads is in HKLM and names this manifest or
        /// a DLL with the same content. Of several registrations the loader uses one, so a single
        /// foreign one can already load other files. HKCU never counts: elevated games ignore it.
        /// </summary>
        private static bool LoadsPayload(IReadOnlyCollection<VulkanLayerRegistryEntry> visible,
            VulkanLayerPayload payload, Func<string, string> hashOf)
        {
            return visible.Count > 0 && visible.All(entry =>
            {
                if (entry.Location == VulkanLayerRegistryLocation.CurrentUser)
                    return false;
                if (IsSamePath(entry.ManifestPath, payload.ManifestPath))
                    return true;

                string hash = hashOf(entry.LibraryPath);
                return hash != null && string.Equals(hash, payload.LibraryHash, StringComparison.OrdinalIgnoreCase);
            });
        }

        private static VulkanLayerPayload ReadPayload(string folder, VulkanLayerLibraryBitness bitness, out string problem)
        {
            problem = null;
            string manifest = Path.GetFullPath(Path.Combine(folder, ManifestFileName));

            if (!VulkanLayerRegistrationProbe.TryReadManifest(manifest, out string layerName, out string libraryPath, out _)
                || layerName != VulkanLayerRegistrationProbe.LayerName || libraryPath == null)
            {
                problem = $"no CapFrameX layer manifest at {manifest}";
                return null;
            }

            if (VulkanLayerRegistrationProbe.ReadLibraryBitness(libraryPath) != bitness)
            {
                problem = $"{libraryPath} is missing or not the {bitness} layer";
                return null;
            }

            string hash = ComputeFileHash(libraryPath);
            if (hash == null)
            {
                problem = $"{libraryPath} cannot be read";
                return null;
            }

            return new VulkanLayerPayload(manifest, hash);
        }

        private static bool IsSamePath(string left, string right)
        {
            try
            {
                return string.Equals(Path.GetFullPath(left), Path.GetFullPath(right), StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception)
            {
                return false;
            }
        }

        internal static string ComputeFileHash(string path)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                    return null;

                using (var stream = File.OpenRead(path))
                    return Convert.ToHexString(SHA256.HashData(stream));
            }
            catch (IOException)
            {
                return null;
            }
            catch (UnauthorizedAccessException)
            {
                return null;
            }
        }

        private static string Sha256Hex(byte[] data) => Convert.ToHexString(SHA256.HashData(data));
    }
}
