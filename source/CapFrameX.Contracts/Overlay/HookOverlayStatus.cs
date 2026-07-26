using System;

namespace CapFrameX.Contracts.Overlay
{
    public enum EHookOverlayStatus
    {
        Disabled,
        Waiting,
        Injecting,
        Injected,
        Initializing,
        Active,
        Hidden,
        Idle,
        Error,
        Fallback
    }

    public sealed class HookOverlayStatus
    {
        public HookOverlayStatus(EHookOverlayStatus state, int processId = 0,
            string runtime = null, string detail = null, long heartbeatAgeMilliseconds = -1,
            int steadyRefcount = 0, int releaseThreshold = 0, string renderResolution = null,
            string renderApi = null)
        {
            RenderApi = renderApi;
            State = state;
            ProcessId = processId;
            Runtime = runtime;
            Detail = detail;
            HeartbeatAgeMilliseconds = heartbeatAgeMilliseconds;
            SteadyRefcount = steadyRefcount;
            ReleaseThreshold = releaseThreshold;
            RenderResolution = renderResolution;
        }

        public EHookOverlayStatus State { get; }

        public int ProcessId { get; }

        public string Runtime { get; }

        public string Detail { get; }

        public long HeartbeatAgeMilliseconds { get; }

        public int SteadyRefcount { get; }

        public int ReleaseThreshold { get; }

        /// <summary>
        /// Backbuffer extent the in-game hook presents into, as "WxH", or null while unknown.
        /// This is the only render-resolution source that does not depend on RTSS, which is not
        /// started at all when the in-game overlay renders — see the capture file's
        /// ResolutionInfo.
        /// </summary>
        public string RenderResolution { get; }

        /// <summary>
        /// Graphics API the game presents with — "DX11", "DX12" or "Vulkan", matching the
        /// spelling RTSS uses so both can feed the capture file's ApiInfo. Null while unknown.
        /// </summary>
        public string RenderApi { get; }
    }

    public interface IHookOverlayStatusService
    {
        HookOverlayStatus Current { get; }

        IObservable<HookOverlayStatus> StatusStream { get; }
    }
}
