using System;
using System.Collections.Generic;
using System.IO;
using Serilog;

namespace CapFrameX.OSD.Integration
{
    internal enum HookEvidenceScanState
    {
        /// <summary>The module list could not be read. Nothing below may be treated as absent.</summary>
        Unknown = 0,
        Complete = 1
    }

    /// <summary>
    /// Which injection path attaches the hook. Only the early path (process-start probe, gated
    /// on a precursor module) attaches before the swapchain exists; the normal path starts from
    /// a PresentMon frame row, i.e. after the game has already presented.
    /// </summary>
    internal enum HookAttachMode
    {
        Late = 0,
        Early = 1
    }

    /// <summary>
    /// What the target process reveals about its presentation before the hook is injected:
    /// which frame-generation runtimes are resident, whether a presentation proxy sits in front
    /// of DXGI, whether the FidelityFX loader is resident twice. The planner picks the first
    /// routing stage from it and the learned profile is keyed by its <see cref="Signature"/>.
    /// </summary>
    internal sealed class HookTargetEvidence
    {
        internal static readonly string[] StreamlineModules = { "sl.interposer.dll" };
        internal static readonly string[] StreamlineDlssgModules = { "sl.dlss_g.dll", "nvngx_dlssg.dll" };
        internal const string Dlssg2Fsr3Prefix = "dlssg_to_fsr3";
        internal static readonly string[] XeFgModules = { "libxess_fg.dll" };
        internal static readonly string[] FfxFgModules =
        {
            "amd_fidelityfx_dx12.dll",
            "amd_fidelityfx_framegeneration_dx12.dll",
            "ffx_backend_dx12_x64.dll",
            "ffx_backend_dx12_x86.dll"
        };
        internal static readonly string[] FfxLoaderModules =
        {
            "amd_fidelityfx_loader_dx12.dll",
            "amd_fidelityfx_dx12.dll"
        };
        internal const string UnknownSignature = "unknown";
        internal const string EmptySignature = "none";

        internal HookTargetEvidence(HookEvidenceScanState scanState, bool streamline,
            bool streamlineDlssg, bool dlssg2Fsr3, bool xeFg, bool ffxFg, int ffxLoaderCopies,
            bool nonSystemDxgiProxy, bool d3d12Loaded, bool d3d11Loaded, string runtime,
            HookAttachMode attachMode, IReadOnlyList<string> ffxLoaderPaths,
            string dxgiProxyPath)
        {
            ScanState = scanState;
            Streamline = streamline;
            StreamlineDlssg = streamlineDlssg;
            Dlssg2Fsr3 = dlssg2Fsr3;
            XeFg = xeFg;
            FfxFg = ffxFg;
            FfxLoaderCopies = ffxLoaderCopies;
            NonSystemDxgiProxy = nonSystemDxgiProxy;
            D3D12Loaded = d3d12Loaded;
            D3D11Loaded = d3d11Loaded;
            Runtime = runtime;
            AttachMode = attachMode;
            FfxLoaderPaths = ffxLoaderPaths ?? Array.Empty<string>();
            DxgiProxyPath = dxgiProxyPath;
            Signature = scanState == HookEvidenceScanState.Complete
                ? BuildSignature(streamline, streamlineDlssg, dlssg2Fsr3, xeFg, ffxFg,
                    ffxLoaderCopies >= 2, nonSystemDxgiProxy)
                : UnknownSignature;
        }

        internal HookEvidenceScanState ScanState { get; }
        internal bool Streamline { get; }
        internal bool StreamlineDlssg { get; }
        internal bool Dlssg2Fsr3 { get; }
        internal bool XeFg { get; }
        internal bool FfxFg { get; }
        /// <summary>Resident FidelityFX loader copies with distinct paths.</summary>
        internal int FfxLoaderCopies { get; }
        /// <summary>A dxgi.dll mapped from outside the system directory (OptiScaler and kin).</summary>
        internal bool NonSystemDxgiProxy { get; }
        internal bool D3D12Loaded { get; }
        internal bool D3D11Loaded { get; }
        /// <summary>PresentMon runtime column, may be null on the early path.</summary>
        internal string Runtime { get; }
        internal HookAttachMode AttachMode { get; }
        internal IReadOnlyList<string> FfxLoaderPaths { get; }
        internal string DxgiProxyPath { get; }

        /// <summary>
        /// Canonical key: sorted presence tokens only. Paths, attach mode and the runtime column
        /// are deliberately excluded so irrelevant variance cannot fragment learned entries.
        /// </summary>
        internal string Signature { get; }

        internal bool IsKnown => ScanState == HookEvidenceScanState.Complete;

        internal bool HasFrameGenerationRuntime =>
            Streamline || StreamlineDlssg || Dlssg2Fsr3 || XeFg || FfxFg;

        /// <summary>Anything that gives the FidelityFX-lifecycle switch a meaning.</summary>
        internal bool HasFidelityFxEvidence =>
            FfxFg || FfxLoaderCopies >= 1 || NonSystemDxgiProxy;

        /// <summary>
        /// The constellation in which the hook's FidelityFX export arming stalled: a second
        /// loader copy or a proxy answering GetProcAddress for the ffx* exports.
        /// </summary>
        internal bool SuggestsFidelityFxArmingHazard =>
            FfxLoaderCopies >= 2 || NonSystemDxgiProxy;

        internal static HookTargetEvidence Unknown(string runtime, HookAttachMode attachMode)
            => new HookTargetEvidence(HookEvidenceScanState.Unknown, false, false, false, false,
                false, 0, false, false, false, runtime, attachMode, null, null);

        internal static string BuildSignature(bool streamline, bool streamlineDlssg,
            bool dlssg2Fsr3, bool xeFg, bool ffxFg, bool ffxLoaderDuplicate, bool dxgiProxy)
        {
            var tokens = new List<string>();
            if (dlssg2Fsr3) tokens.Add("dlssg2fsr3");
            if (dxgiProxy) tokens.Add("dxgiproxy");
            if (ffxFg) tokens.Add("ffxfg");
            if (ffxLoaderDuplicate) tokens.Add("ffxloader2");
            if (streamline) tokens.Add("sl");
            if (streamlineDlssg) tokens.Add("sldlssg");
            if (xeFg) tokens.Add("xefg");
            tokens.Sort(StringComparer.Ordinal);
            return tokens.Count == 0 ? EmptySignature : string.Join("+", tokens);
        }

        /// <summary>
        /// Pure classification of a module list. <paramref name="systemRoot"/> defaults to
        /// %SystemRoot%; tests pass their own.
        /// </summary>
        internal static HookTargetEvidence FromModules(IEnumerable<HookModuleRecord> modules,
            string runtime, HookAttachMode attachMode, string systemRoot = null)
        {
            if (modules == null) return Unknown(runtime, attachMode);
            string root = systemRoot ?? Environment.GetEnvironmentVariable("SystemRoot") ??
                @"C:\Windows";
            string system32 = NormalizeDirectory(Path.Combine(root, "System32"));
            string sysWow64 = NormalizeDirectory(Path.Combine(root, "SysWOW64"));

            bool streamline = false, streamlineDlssg = false, dlssg2Fsr3 = false, xeFg = false,
                ffxFg = false, d3d12 = false, d3d11 = false;
            string dxgiProxyPath = null;
            var loaderPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var loaderPathList = new List<string>();
            foreach (HookModuleRecord module in modules)
            {
                string name = module.Name;
                if (string.IsNullOrWhiteSpace(name)) continue;
                if (Matches(name, StreamlineModules)) streamline = true;
                if (Matches(name, StreamlineDlssgModules)) streamlineDlssg = true;
                if (name.StartsWith(Dlssg2Fsr3Prefix, StringComparison.OrdinalIgnoreCase))
                    dlssg2Fsr3 = true;
                if (Matches(name, XeFgModules)) xeFg = true;
                if (Matches(name, FfxFgModules)) ffxFg = true;
                if (Matches(name, FfxLoaderModules))
                {
                    string path = NormalizePath(module.Path) ?? name;
                    if (loaderPaths.Add(path)) loaderPathList.Add(path);
                }
                if (string.Equals(name, "d3d12.dll", StringComparison.OrdinalIgnoreCase))
                    d3d12 = true;
                if (string.Equals(name, "d3d11.dll", StringComparison.OrdinalIgnoreCase))
                    d3d11 = true;
                if (string.Equals(name, "dxgi.dll", StringComparison.OrdinalIgnoreCase))
                {
                    string directory = NormalizeDirectory(SafeDirectoryName(module.Path));
                    if (directory != null &&
                        !string.Equals(directory, system32, StringComparison.OrdinalIgnoreCase) &&
                        !string.Equals(directory, sysWow64, StringComparison.OrdinalIgnoreCase))
                        dxgiProxyPath = module.Path;
                }
            }

            return new HookTargetEvidence(HookEvidenceScanState.Complete, streamline,
                streamlineDlssg, dlssg2Fsr3, xeFg, ffxFg, loaderPathList.Count,
                dxgiProxyPath != null, d3d12, d3d11, runtime, attachMode, loaderPathList,
                dxgiProxyPath);
        }

        internal string Describe()
        {
            if (!IsKnown) return "module list unreadable";
            var parts = new List<string>();
            if (Streamline) parts.Add("Streamline");
            if (StreamlineDlssg) parts.Add("DLSS-G");
            if (Dlssg2Fsr3) parts.Add("dlssg-to-fsr3");
            if (XeFg) parts.Add("XeSS-FG");
            if (FfxFg) parts.Add("FSR-FG");
            if (FfxLoaderCopies >= 2) parts.Add($"{FfxLoaderCopies} FidelityFX loader copies");
            if (NonSystemDxgiProxy) parts.Add($"dxgi.dll proxy at '{DxgiProxyPath}'");
            parts.Add(D3D12Loaded ? "d3d12" : "no d3d12");
            return string.Join(", ", parts);
        }

        private static bool Matches(string name, string[] candidates)
        {
            foreach (string candidate in candidates)
            {
                if (string.Equals(name, candidate, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        private static string SafeDirectoryName(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return null;
            try { return Path.GetDirectoryName(path); }
            catch (ArgumentException) { return null; }
        }

        private static string NormalizePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return null;
            try { return Path.GetFullPath(path); }
            catch (Exception ex) when (ex is ArgumentException || ex is NotSupportedException ||
                                       ex is PathTooLongException)
            {
                return path;
            }
        }

        private static string NormalizeDirectory(string directory)
        {
            string normalized = NormalizePath(directory);
            return normalized?.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
    }

    internal static class HookTargetEvidenceProbe
    {
        /// <summary>
        /// Scans <paramref name="pid"/> now. Called at plan time (once per injection attempt)
        /// and again when a stage verdict is recorded, so the stored signature reflects every
        /// runtime the title loaded — the early path sees only the gate module at plan time.
        /// </summary>
        internal static HookTargetEvidence Probe(int pid, string runtime,
            HookAttachMode attachMode)
        {
            if (!HookModuleScanner.TryEnumerate(pid, out IReadOnlyList<HookModuleRecord> modules,
                out string error))
            {
                Log.Debug("HookOverlay: compatibility evidence scan of pid {pid} inconclusive ({error})",
                    pid, error);
                return HookTargetEvidence.Unknown(runtime, attachMode);
            }
            return HookTargetEvidence.FromModules(modules, runtime, attachMode);
        }
    }
}
