using System;
using System.ComponentModel;

namespace CapFrameX.Contracts.Overlay
{
    public enum EHookOverlayStatus
    {
        [Description("Off")]
        Disabled,
        [Description("Waiting")]
        Waiting,
        [Description("Injecting")]
        Injecting,
        [Description("Injected")]
        Injected,
        [Description("Initializing")]
        Initializing,
        [Description("Active")]
        Active,
        [Description("Hidden")]
        Hidden,
        [Description("Idle")]
        Idle,
        [Description("Error")]
        Error,
        [Description("Fallback")]
        Fallback,
        /// <summary>
        /// Injection was deliberately not attempted and will not be retried for this process.
        /// Unlike <see cref="Waiting"/> this never resolves on its own — the game has to be
        /// restarted. Detail carries the reason.
        /// </summary>
        [Description("Blocked")]
        Blocked,
        /// <summary>
        /// The hook is injected and CapFrameX is judging the current compatibility stage against
        /// its time budget. Resolves into <see cref="Active"/>, <see cref="Fallback"/> or
        /// <see cref="RestartPending"/> within seconds.
        /// </summary>
        [Description("Probing")]
        Probing,
        /// <summary>
        /// The current stage failed and the next one needs a fresh process. The hook-free
        /// fallback serves this session; the next launch of the title starts on the next stage.
        /// </summary>
        [Description("Restart game")]
        RestartPending
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

    /// <summary>
    /// The one-word label a status state gets in the UI. Both surfaces that show the state — the
    /// status bar and the "Overlay hook status" overlay entry — read it from here, so an added or
    /// renamed state cannot end up spelled differently depending on where the user looks.
    /// </summary>
    public static class HookOverlayStatusLabel
    {
        public static string ForState(EHookOverlayStatus state)
        {
            switch (state)
            {
                case EHookOverlayStatus.Disabled: return "Off";
                case EHookOverlayStatus.Waiting: return "Waiting";
                case EHookOverlayStatus.Injecting: return "Injecting";
                case EHookOverlayStatus.Injected: return "Injected";
                case EHookOverlayStatus.Initializing: return "Initializing";
                case EHookOverlayStatus.Active: return "Active";
                case EHookOverlayStatus.Fallback: return "Fallback";
                case EHookOverlayStatus.Hidden: return "Hidden";
                case EHookOverlayStatus.Idle: return "Idle";
                case EHookOverlayStatus.Error: return "Error";
                case EHookOverlayStatus.Blocked: return "Blocked";
                case EHookOverlayStatus.Probing: return "Probing";
                case EHookOverlayStatus.RestartPending: return "Restart game";
                default: return "Waiting";
            }
        }
    }

    public interface IHookOverlayStatusService
    {
        HookOverlayStatus Current { get; }

        IObservable<HookOverlayStatus> StatusStream { get; }
    }
}
