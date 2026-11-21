using System;
using System.Globalization;
using CapFrameX.Contracts.Overlay;

namespace CapFrameX.OSD.Integration
{
    /// <summary>
    /// The texts of the "Upscaler version" and "Frame generation version" entries. Pure: every
    /// input is passed in, the ticks on the hook's clock (GetTickCount64).
    /// </summary>
    internal static class UpscalingVersionText
    {
        /// <summary>
        /// How long after its last dispatch a technique still counts as running. The hook
        /// publishes the time of use about four times per second.
        /// </summary>
        internal const ulong InUseWindowMs = 3000;
        /// <summary>The age of the hook's heartbeat up to which its telemetry is current.</summary>
        internal const long HeartbeatWindowMs = 3000;

        /// <summary>
        /// How far behind the newest evaluation another upscaler may be and still count as
        /// running beside it, see <see cref="Upscaler"/>.
        /// </summary>
        internal const ulong ConcurrentUseWindowMs = 1000;

        internal const string Fsr = "FSR";
        internal const string Dlss = "DLSS";
        /// <summary>DLSS Ray Reconstruction, which upscales in place of Super Resolution.</summary>
        internal const string DlssRayReconstruction = "DLSS RR";
        internal const string Xess = "XeSS";
        /// <summary>Marks the default version of the game's library, see <see cref="HookFidelityFxVersionSource.LibraryDefault"/>.</summary>
        internal const string LibraryDefaultSuffix = " (DLL)";

        // FrameGenerationTechnology and FrameGenerationActivity of the hook status block.
        internal const int TechnologyDlss = 1;
        internal const int TechnologyXess = 2;
        internal const int TechnologyFsr = 3;
        internal const int ActivityInactive = 1;
        internal const int ActivityActive = 2;

        internal static bool IsInUse(ulong lastUseTickMs, ulong nowTickMs)
            => lastUseTickMs != 0 &&
               (nowTickMs < lastUseTickMs || nowTickMs - lastUseTickMs <= InUseWindowMs);

        internal static bool IsInUse(HookFidelityFxProviderVersion version, ulong nowTickMs)
            => IsInUse(version.LastUseTickMs, nowTickMs);

        /// <summary>
        /// The upscaler the game evaluates: "FSR 3.1.5", "DLSS 310.7.130", "DLSS RR 310.7.130"
        /// or "XeSS 2.0.2". Null while none ran within the window; a library that is loaded but
        /// not evaluated names nothing.
        /// </summary>
        /// <remarks>
        /// After a switch in the game's settings the former upscaler is still inside the window:
        /// the one that ran last is named. Upscalers whose last evaluations lie within
        /// <see cref="ConcurrentUseWindowMs"/> of each other run at the same time, which only
        /// something that answers one upscaler's interface with another one produces. Then a
        /// fixed order decides, so the text does not alternate with every refresh: the DLSS and
        /// XeSS libraries, whose own evaluation the hook sees, come before a FidelityFX dispatch.
        /// </remarks>
        internal static string Upscaler(HookFidelityFxProviderVersion fsr, HookUpscalerUses uses,
            ulong nowTickMs)
        {
            // In the order that decides between upscalers running at the same time.
            var candidates = new (string Text, ulong LastUseTickMs)[]
            {
                (RuntimeInUse(DlssRayReconstruction, uses.DlssRayReconstruction, nowTickMs),
                    uses.DlssRayReconstruction.LastUseTickMs),
                (RuntimeInUse(Dlss, uses.DlssSuperResolution, nowTickMs),
                    uses.DlssSuperResolution.LastUseTickMs),
                (RuntimeInUse(Xess, uses.Xess, nowTickMs), uses.Xess.LastUseTickMs),
                (FsrInUse(fsr, nowTickMs), fsr.LastUseTickMs),
            };

            ulong newest = 0;
            foreach (var candidate in candidates)
                if (candidate.Text != null && candidate.LastUseTickMs > newest)
                    newest = candidate.LastUseTickMs;
            foreach (var candidate in candidates)
                if (candidate.Text != null &&
                    newest - candidate.LastUseTickMs <= ConcurrentUseWindowMs)
                    return candidate.Text;
            return null;
        }

        private static string RuntimeInUse(string technology, HookUpscalerRuntimeUse use,
            ulong nowTickMs)
            => IsInUse(use.LastUseTickMs, nowTickMs) && !use.Version.IsEmpty
                ? technology + " " + use.Version.ToDisplayString()
                : null;

        /// <summary>"FSR 3.1.5", "FSR 3.1.2 (DLL)", or null without a dispatch in the window.</summary>
        internal static string FsrInUse(HookFidelityFxProviderVersion version, ulong nowTickMs)
        {
            if (!IsInUse(version, nowTickMs) || string.IsNullOrEmpty(version.Name)) return null;
            switch (version.Source)
            {
                case HookFidelityFxVersionSource.Context:
                    return Fsr + " " + version.Name;
                case HookFidelityFxVersionSource.LibraryDefault:
                    return Fsr + " " + version.Name + LibraryDefaultSuffix;
                default:
                    return null;
            }
        }

        /// <summary>
        /// The version of the frame generation the hook reports as switched on. Null while it is
        /// off or unknown: a loaded runtime that generates no frames names nothing.
        /// </summary>
        internal static string FrameGeneration(int technology, int activity,
            HookFidelityFxProviderVersion fsrFrameGeneration, ulong nowTickMs,
            RuntimeFileVersion? dlssFrameGeneration, RuntimeFileVersion? xessFrameGeneration)
        {
            if (activity != ActivityActive) return null;
            switch (technology)
            {
                case TechnologyFsr:
                    return FsrInUse(fsrFrameGeneration, nowTickMs);
                case TechnologyDlss:
                    return Named(Dlss, dlssFrameGeneration);
                case TechnologyXess:
                    return Named(Xess, xessFrameGeneration);
                default:
                    return null;
            }
        }

        private static string Named(string technology, RuntimeFileVersion? version)
            => version == null || version.Value.IsEmpty
                ? null : technology + " " + version.Value.ToDisplayString();

        /// <summary>
        /// How far a measured factor may be from a whole one, as a share of it, and still be
        /// shown as that whole factor: the counters of both sides are read a frame apart.
        /// </summary>
        internal const double WholeFactorTolerance = 0.06;

        /// <summary>
        /// "2x", "3x", "4x" for a measured frame generation factor, "1x" while frame generation
        /// is switched on but every displayed frame is a rendered one. A factor between two whole
        /// ones (a dynamic factor, or the seconds after a switch) keeps one decimal: "2.5x".
        /// </summary>
        internal static string FrameGenerationFactor(double factor)
        {
            if (double.IsNaN(factor) || double.IsInfinity(factor) || factor <= 0) return null;
            // Fewer displayed than rendered frames multiply nothing.
            if (factor < 1) factor = 1;
            double whole = Math.Round(factor);
            return Math.Abs(factor - whole) <= whole * WholeFactorTolerance
                ? whole.ToString("0", CultureInfo.InvariantCulture) + "x"
                : factor.ToString("0.0", CultureInfo.InvariantCulture) + "x";
        }
    }

    /// <summary>
    /// Reads what the in-game hook knows about the upscaler and the frame generation of a
    /// process and names their versions and the factor of the frame generation. The hook reports
    /// the upscalers with their versions; the state here are the module versions of the DLSS and
    /// XeSS frame generation libraries, which are scanned in the background, and the last frame
    /// counts the factor is measured from.
    /// </summary>
    public sealed class UpscalingTechnologyVersionService : IUpscalingTechnologyVersionService
    {
        private readonly FrameGenerationRuntimeModuleVersions _moduleVersions =
            new FrameGenerationRuntimeModuleVersions();
        private readonly object _factorLock = new object();
        private readonly FrameGenerationFactorTracker _factorTracker =
            new FrameGenerationFactorTracker();
        private int _factorProcessId;

        /// <inheritdoc />
        public string GetUpscalerVersion(int processId)
        {
            // Two blocks of the hook: the FSR context the game dispatches, and the DLSS and XeSS
            // libraries it evaluates. A hook from before either one simply lacks that mapping.
            bool fsrKnown = HookFidelityFxVersionProbe.TryRead(processId,
                out HookFidelityFxVersions fsr);
            bool usesKnown = HookUpscalerUseProbe.TryRead(processId, out HookUpscalerUses uses);
            if (!fsrKnown && !usesKnown) return null;
            // The hook stamps with GetTickCount64, which counts sleep time;
            // Environment.TickCount64 does not since .NET 11.
            return UpscalingVersionText.Upscaler(fsr.Upscaler, uses,
                HookStatusProbe.CurrentTickCount);
        }

        /// <inheritdoc />
        public string GetFrameGenerationVersion(int processId)
        {
            // Nothing is read for a frame generation that is switched off: a process without
            // one is never scanned for its libraries.
            if (!TryReadFrameGeneration(processId, out NativeHookStatusSnapshot native,
                    out ulong now) ||
                native.FgActivity != UpscalingVersionText.ActivityActive)
                return null;

            HookFidelityFxVersions fsr = default;
            RuntimeFileVersion? dlss = null, xess = null;
            switch (native.FgTechnology)
            {
                case UpscalingVersionText.TechnologyFsr:
                    HookFidelityFxVersionProbe.TryRead(processId, out fsr);
                    break;
                case UpscalingVersionText.TechnologyDlss:
                    dlss = _moduleVersions.GetVersion(processId,
                        EFrameGenerationRuntime.DlssFrameGeneration);
                    break;
                case UpscalingVersionText.TechnologyXess:
                    xess = _moduleVersions.GetVersion(processId,
                        EFrameGenerationRuntime.XessFrameGeneration);
                    break;
            }
            return UpscalingVersionText.FrameGeneration(native.FgTechnology, native.FgActivity,
                fsr.FrameGeneration, now, dlss, xess);
        }

        /// <inheritdoc />
        public string GetFrameGenerationFactor(int processId)
        {
            // Measured while frame generation is on, and while the hook cannot tell: it learns
            // the state from a control call, and a game that switched its frame generation on
            // before the hook attached may never make one again. Only "off" ends the measurement.
            bool measurable = TryReadFrameGeneration(processId,
                    out NativeHookStatusSnapshot native, out ulong now) &&
                native.FgActivity != UpscalingVersionText.ActivityInactive;
            HookFrameCounts counts = default;
            bool counted = measurable && HookFrameCountsProbe.TryRead(processId, out counts);

            double? factor;
            lock (_factorLock)
            {
                // A measurement belongs to one process and one stretch of frame generation: what
                // was counted while it was off must not reach into the next span.
                if (!counted || processId != _factorProcessId)
                {
                    _factorTracker.Reset();
                    _factorProcessId = counted ? processId : 0;
                }
                if (!counted) return null;
                factor = _factorTracker.Observe(counts, now, native.FgTechnology,
                    native.FgActivity);
            }
            return factor == null ? null : UpscalingVersionText.FrameGenerationFactor(factor.Value);
        }

        // The frame generation telemetry of the hook, while its heartbeat is current.
        private static bool TryReadFrameGeneration(int processId,
            out NativeHookStatusSnapshot native, out ulong now)
        {
            native = default;
            now = 0;
            if (processId <= 0 || !HookStatusProbe.TryRead(processId, out native, out _))
                return false;
            now = HookStatusProbe.CurrentTickCount;
            long heartbeatAge = HookStatusProbe.GetHeartbeatAgeMilliseconds(
                native.LastHeartbeatTickMs, now);
            return heartbeatAge >= 0 && heartbeatAge <= UpscalingVersionText.HeartbeatWindowMs;
        }
    }
}
