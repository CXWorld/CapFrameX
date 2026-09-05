using System;
using CapFrameX.Contracts.Overlay;

namespace CapFrameX.OSD.Integration
{
    internal static class HookOverlayStatusEvaluator
    {
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
                return Status(EHookOverlayStatus.Initializing,
                    $"{target}: a frame-generation runtime is presenting; the in-game overlay stands down.",
                    heartbeatAge, native, processId, runtime);
            }
            if ((flags & NativeHookStatusFlags.HooksArmed) == 0)
            {
                return Status(EHookOverlayStatus.Initializing,
                    $"{target}: hook loaded; installing DXGI hooks.", heartbeatAge, native,
                    processId, runtime);
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
                    $"{target}: hook and Present heartbeat are live; rendering is hidden or suppressed.",
                    heartbeatAge, native, processId, runtime);
            }

            var ready = NativeHookStatusFlags.RendererReady |
                        NativeHookStatusFlags.MetricsConnected |
                        NativeHookStatusFlags.Rendered;
            if ((flags & ready) != ready)
            {
                return Status(EHookOverlayStatus.Initializing,
                    $"{target}: Present is live; waiting for renderer resources and metrics.",
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
