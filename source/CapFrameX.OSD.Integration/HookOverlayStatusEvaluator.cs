using System;
using CapFrameX.Contracts.Overlay;

namespace CapFrameX.OSD.Integration
{
    /// <summary>What the status needs to know about a running compatibility probe.</summary>
    internal readonly struct HookProbeStatusView
    {
        internal HookProbeStatusView(bool observing, int stageNumber, int stageCount,
            string stageName, long remainingMs)
        {
            Observing = observing;
            StageNumber = stageNumber;
            StageCount = stageCount;
            StageName = stageName;
            RemainingMs = remainingMs;
        }

        internal bool Observing { get; }
        internal int StageNumber { get; }
        internal int StageCount { get; }
        internal string StageName { get; }
        internal long RemainingMs { get; }
    }

    internal static class HookOverlayStatusEvaluator
    {
        /// <summary>
        /// While a stage is under observation, a not-yet-active native status reads as
        /// <see cref="EHookOverlayStatus.Probing"/> with the stage and its remaining budget.
        /// An active or errored status is left alone: the first is the answer, the second is
        /// not a routing question.
        /// </summary>
        internal static HookOverlayStatus WithProbe(HookOverlayStatus native,
            in HookProbeStatusView probe)
        {
            if (native == null || !probe.Observing ||
                native.State == EHookOverlayStatus.Active ||
                native.State == EHookOverlayStatus.Error)
                return native;
            string remaining = probe.RemainingMs > 0
                ? $"{Math.Ceiling(probe.RemainingMs / 1000.0):0} s left"
                : "budget exhausted";
            return new HookOverlayStatus(EHookOverlayStatus.Probing, native.ProcessId,
                native.Runtime,
                $"{native.Detail} Probing compatibility stage {probe.StageNumber}/{probe.StageCount}: {probe.StageName} ({remaining}).",
                native.HeartbeatAgeMilliseconds, native.SteadyRefcount,
                native.ReleaseThreshold, native.RenderResolution, native.RenderApi);
        }

        internal static HookOverlayStatus EvaluateNative(int processId, string runtime,
            NativeHookStatusSnapshot native, ulong nowTickMs)
        {
            string target = Target(processId, runtime);
            var flags = native.Flags;
            long heartbeatAge = HookStatusProbe.GetHeartbeatAgeMilliseconds(
                native.LastHeartbeatTickMs, nowTickMs);

            if ((flags & NativeHookStatusFlags.Error) != 0)
            {
                return Status(EHookOverlayStatus.Error,
                    $"{target}: {DescribeError(native.LastError)}", heartbeatAge, native,
                    processId, runtime);
            }
            if ((flags & NativeHookStatusFlags.EarlyInjectionRequired) != 0)
            {
                return Status(EHookOverlayStatus.Initializing,
                    $"{target}: XeSS-FG owns the presenting swapchain, but its initialization queue was created before the hook attached; restart the game while CapFrameX remains open.",
                    heartbeatAge, native, processId, runtime);
            }
            if ((flags & NativeHookStatusFlags.ForeignPresenter) != 0)
            {
                string technology = DescribeFrameGenerationTechnology(native.FgTechnology);
                string runtimeText = technology == null
                    ? "a frame-generation runtime"
                    : $"a frame-generation runtime ({technology})";
                return Status(EHookOverlayStatus.Initializing,
                    $"{target}: {runtimeText} is presenting; the in-game overlay stands down.",
                    heartbeatAge, native, processId, runtime);
            }
            if ((flags & NativeHookStatusFlags.HooksArmed) == 0)
            {
                // A version-2 hook names the InstallHooks step it is in. If that step never
                // changes again, the install stopped there; the reason belongs in the status
                // rather than only in the opt-in native file log.
                string phase = DescribeInstallPhase(native.InstallPhase, native.InstallDetail);
                return Status(EHookOverlayStatus.Initializing,
                    phase == null
                        ? $"{target}: hook loaded; installing DXGI hooks."
                        : $"{target}: hook loaded; installing DXGI hooks (phase {phase}).",
                    heartbeatAge, native, processId, runtime);
            }
            if ((flags & NativeHookStatusFlags.PresentSeen) == 0 || heartbeatAge < 0)
            {
                return Status(EHookOverlayStatus.Waiting,
                    $"{target}: hook armed; waiting for the first DXGI Present.", heartbeatAge,
                    native, processId, runtime);
            }
            if ((ulong)heartbeatAge > HookStatusProbe.HeartbeatStaleAfterMs)
            {
                return Status(EHookOverlayStatus.Idle,
                    $"{target}: no DXGI Present for {heartbeatAge / 1000.0:F1} s; the game may be paused or minimized.",
                    heartbeatAge, native, processId, runtime);
            }
            if ((flags & NativeHookStatusFlags.Dormant) != 0)
            {
                return Status(EHookOverlayStatus.Idle,
                    $"{target}: hook is live but the CapFrameX host is dormant.", heartbeatAge,
                    native, processId, runtime);
            }
            if ((flags & NativeHookStatusFlags.Visible) == 0)
            {
                return Status(EHookOverlayStatus.Hidden,
                    AppendDeclineReason(
                        $"{target}: hook and Present heartbeat are live; rendering is hidden or suppressed.",
                        native.LastDeclineReason, ignoreHidden: true),
                    heartbeatAge, native, processId, runtime);
            }

            var ready = NativeHookStatusFlags.RendererReady |
                        NativeHookStatusFlags.MetricsConnected |
                        NativeHookStatusFlags.Rendered;
            if ((flags & ready) != ready)
            {
                return Status(EHookOverlayStatus.Initializing,
                    AppendDeclineReason(
                        $"{target}: Present is live; waiting for renderer resources and metrics.",
                        native.LastDeclineReason, ignoreHidden: false),
                    heartbeatAge, native, processId, runtime);
            }

            return Status(EHookOverlayStatus.Active,
                $"{target}: hook active, {native.MetricsEntryCount} metrics, heartbeat {heartbeatAge / 1000.0:F1} s.",
                heartbeatAge, native, processId, runtime);
        }

        private static HookOverlayStatus Status(EHookOverlayStatus state, string detail,
            long heartbeatAge, NativeHookStatusSnapshot native, int processId, string runtime)
        {
            return new HookOverlayStatus(state, processId, runtime, detail, heartbeatAge,
                native.SteadyRefcount, native.ReleaseThreshold,
                FormatResolution(native.ResolutionX, native.ResolutionY),
                FormatApi(native.Api));
        }

        /// <summary>
        /// Names the InstallHooks step a version-2 hook last published. Null for a version-1
        /// hook (phase None), so the plain text stays unchanged for it.
        /// </summary>
        internal static string DescribeInstallPhase(NativeHookInstallPhase phase, int detail)
        {
            if (phase == NativeHookInstallPhase.None) return null;
            string text = phase == NativeHookInstallPhase.Unknown
                ? "unknown"
                : phase.ToString();
            if (phase == NativeHookInstallPhase.FidelityFxExports && detail != 0)
            {
                int module = detail >> 8;
                int export = detail & 0xFF;
                text += export == 0
                    ? $", module {module}"
                    : $", module {module}, export {export}";
            }
            return text;
        }

        /// <summary>
        /// Human-readable form of the hook's last decline reason. Null for None, so a caller can
        /// append it only when there is something to say.
        /// </summary>
        internal static string DescribeDeclineReason(NativeHookDeclineReason reason)
        {
            switch (reason)
            {
                case NativeHookDeclineReason.None: return null;
                case NativeHookDeclineReason.ExternalMutation:
                    return "a vendor swapchain mutation is in flight";
                case NativeHookDeclineReason.Dormant:
                    return "the CapFrameX host is dormant";
                case NativeHookDeclineReason.FidelityFxOwnsPresentation:
                    return "a FidelityFX replacement swapchain owns presentation";
                case NativeHookDeclineReason.StreamlineBlocksNative:
                    return "Streamline owns the D3D12 presentation contract";
                case NativeHookDeclineReason.XeFgProxyNoQueue:
                    return "the XeSS-FG proxy has no authoritative queue";
                case NativeHookDeclineReason.ForeignNativePresent:
                    return "a frame-generation runtime issued the native Present";
                case NativeHookDeclineReason.FgAuthoritativeNoRoute:
                    return "frame generation is active without an admitted route";
                case NativeHookDeclineReason.FgStandDown:
                    return "the in-game renderer stood down for a frame-generation runtime";
                case NativeHookDeclineReason.OwnerDeferral:
                    return "the previous resource owner has not released the swapchain";
                case NativeHookDeclineReason.Hidden:
                    return "rendering is hidden";
                case NativeHookDeclineReason.StreamlineNoCreationQueue:
                    return "the Streamline proxy exposed no creation queue";
                case NativeHookDeclineReason.XeFgIndeterminateNoDraw:
                    return "the indeterminate XeSS-FG route produced no draw";
                case NativeHookDeclineReason.D3D12NoQueue:
                    return "no compatible D3D12 command queue was observed";
                case NativeHookDeclineReason.D3D12DeviceMismatch:
                    return "the observed D3D12 queue belongs to another device";
                case NativeHookDeclineReason.D3D12TransitionDeferred:
                    return "a D3D12 queue transition is settling";
                default:
                    return $"native decline reason {(int)reason}";
            }
        }

        /// <summary>FrameGenerationTechnology as the OSD spells it; null while unknown.</summary>
        internal static string DescribeFrameGenerationTechnology(int technology)
        {
            switch (technology)
            {
                case 1: return "DLSS-FG";
                case 2: return "XeSS-FG";
                case 3: return "FSR-FG";
                default: return null;
            }
        }

        private static string AppendDeclineReason(string detail, NativeHookDeclineReason reason,
            bool ignoreHidden)
        {
            if (reason == NativeHookDeclineReason.None ||
                (ignoreHidden && reason == NativeHookDeclineReason.Hidden))
                return detail;
            string text = DescribeDeclineReason(reason);
            return text == null ? detail : $"{detail} Last decline: {text}.";
        }

        /// <summary>
        /// The hook's proven device type, spelled the way RTSS spells it so both can feed the
        /// capture file's ApiInfo. Null while unknown — a caller must be able to tell "not
        /// determined yet" from an answer.
        /// </summary>
        internal static string FormatApi(NativeHookApi api)
        {
            switch (api)
            {
                case NativeHookApi.D3D11: return "DX11";
                case NativeHookApi.D3D12: return "DX12";
                default: return null;
            }
        }

        /// <summary>
        /// The hook's swapchain extent as "WxH", matching the format RTSS reports so both can
        /// feed the capture file's ResolutionInfo. Null while the extent is unknown — a caller
        /// must be able to tell "not measured yet" from a value.
        /// </summary>
        internal static string FormatResolution(int width, int height)
            => width > 0 && height > 0 ? $"{width}x{height}" : null;

        internal static HookOverlayStatus EvaluateVulkan(int processId, string runtime,
            VulkanActivitySnapshot native, ulong nowTickMs, bool overlayVisible)
        {
            string target = Target(processId, runtime);
            if (!native.IsLayerLoaded)
            {
                return new HookOverlayStatus(EHookOverlayStatus.Waiting, processId, runtime,
                    $"{target}: waiting for the CapFrameX Vulkan layer.");
            }

            // Past this point the layer is loaded into the process, which settles the API the
            // same way the DXGI renderer's proven device type does.
            long heartbeatAge = HookStatusProbe.GetHeartbeatAgeMilliseconds(
                native.LastVulkanPresentTickMs, nowTickMs);
            string resolution = FormatResolution(native.ResolutionX, native.ResolutionY);
            if (native.PreferredBackend == 1)
            {
                return VulkanStatus(EHookOverlayStatus.Error, processId, runtime,
                    $"{target}: the Vulkan compositor failed and yielded to DXGI.",
                    heartbeatAge, resolution);
            }
            if (heartbeatAge < 0)
            {
                return VulkanStatus(EHookOverlayStatus.Initializing, processId, runtime,
                    $"{target}: Vulkan layer loaded; waiting for the first vkQueuePresentKHR.",
                    heartbeatAge, resolution);
            }
            if ((ulong)heartbeatAge > HookStatusProbe.HeartbeatStaleAfterMs)
            {
                return VulkanStatus(EHookOverlayStatus.Idle, processId, runtime,
                    $"{target}: no Vulkan Present for {heartbeatAge / 1000.0:F1} s; the game may be paused or minimized.",
                    heartbeatAge, resolution);
            }
            if (!overlayVisible)
            {
                return VulkanStatus(EHookOverlayStatus.Hidden, processId, runtime,
                    $"{target}: Vulkan layer and Present heartbeat are live; the overlay is hidden.",
                    heartbeatAge, resolution);
            }

            // A loaded layer with a live heartbeat is not the same as an overlay on screen. The
            // compositor passes a queue family it cannot serve straight through, which used to be
            // reported as Active: presents kept arriving, nothing failed, and nothing was drawn.
            // This is not Error — the layer is healthy and the next present may composite again,
            // which is what a title does when it moves between menu and gameplay.
            if (native.CompositeState == VulkanCompositeState.UnsupportedQueueFamily)
            {
                return VulkanStatus(EHookOverlayStatus.Fallback, processId, runtime,
                    $"{target}: the game presents from a queue family the Vulkan compositor cannot draw on; the hook-free overlay serves it.",
                    heartbeatAge, resolution);
            }

            return VulkanStatus(EHookOverlayStatus.Active, processId, runtime,
                $"{target}: Vulkan layer active, Present heartbeat {heartbeatAge / 1000.0:F1} s.",
                heartbeatAge, resolution);
        }

        // The V1 status contract retains two legacy DXGI refcount fields. Vulkan and the
        // release-independent DXGI lifecycle both report zero there.
        private static HookOverlayStatus VulkanStatus(EHookOverlayStatus state, int processId,
            string runtime, string detail, long heartbeatAge, string renderResolution)
        {
            return new HookOverlayStatus(state, processId, runtime, detail, heartbeatAge,
                steadyRefcount: 0, releaseThreshold: 0, renderResolution: renderResolution,
                renderApi: "Vulkan");
        }

        private static string Target(int processId, string runtime)
        {
            return $"PID {processId}, {(!string.IsNullOrWhiteSpace(runtime) ? runtime : "DXGI")}";
        }

        private static string DescribeError(int error)
        {
            switch (error)
            {
                case 1: return "DXGI hook installation failed";
                case 2: return "OSD renderer creation failed";
                default: return $"native hook error {error}";
            }
        }
    }
}
