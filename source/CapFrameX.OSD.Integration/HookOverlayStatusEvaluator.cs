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
                $"{target}: hook active, {native.MetricsEntryCount} metrics, heartbeat {heartbeatAge / 1000.0:F1} s; " +
                $"swapchain steady {native.SteadyRefcount}, release threshold {native.ReleaseThreshold}.",
                heartbeatAge, native, processId, runtime);
        }

        private static HookOverlayStatus Status(EHookOverlayStatus state, string detail,
            long heartbeatAge, NativeHookStatusSnapshot native, int processId, string runtime)
        {
            return new HookOverlayStatus(state, processId, runtime, detail, heartbeatAge,
                native.SteadyRefcount, native.ReleaseThreshold);
        }

        internal static HookOverlayStatus EvaluateVulkan(int processId, string runtime,
            VulkanActivitySnapshot native, ulong nowTickMs, bool overlayVisible)
        {
            string target = Target(processId, runtime);
            if (!native.IsLayerLoaded)
            {
                return new HookOverlayStatus(EHookOverlayStatus.Waiting, processId, runtime,
                    $"{target}: waiting for the CapFrameX Vulkan layer.");
            }

            long heartbeatAge = HookStatusProbe.GetHeartbeatAgeMilliseconds(
                native.LastVulkanPresentTickMs, nowTickMs);
            if (native.PreferredBackend == 1)
            {
                return new HookOverlayStatus(EHookOverlayStatus.Error, processId, runtime,
                    $"{target}: the Vulkan compositor failed and yielded to DXGI.",
                    heartbeatAge);
            }
            if (heartbeatAge < 0)
            {
                return new HookOverlayStatus(EHookOverlayStatus.Initializing, processId, runtime,
                    $"{target}: Vulkan layer loaded; waiting for the first vkQueuePresentKHR.",
                    heartbeatAge);
            }
            if ((ulong)heartbeatAge > HookStatusProbe.HeartbeatStaleAfterMs)
            {
                return new HookOverlayStatus(EHookOverlayStatus.Idle, processId, runtime,
                    $"{target}: no Vulkan Present for {heartbeatAge / 1000.0:F1} s; the game may be paused or minimized.",
                    heartbeatAge);
            }
            if (!overlayVisible)
            {
                return new HookOverlayStatus(EHookOverlayStatus.Hidden, processId, runtime,
                    $"{target}: Vulkan layer and Present heartbeat are live; the overlay is hidden.",
                    heartbeatAge);
            }

            return new HookOverlayStatus(EHookOverlayStatus.Active, processId, runtime,
                $"{target}: Vulkan layer active, Present heartbeat {heartbeatAge / 1000.0:F1} s.",
                heartbeatAge);
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
