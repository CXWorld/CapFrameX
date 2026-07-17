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
        Error
    }

    public sealed class HookOverlayStatus
    {
        public HookOverlayStatus(EHookOverlayStatus state, int processId = 0,
            string runtime = null, string detail = null, long heartbeatAgeMilliseconds = -1,
            int steadyRefcount = 0, int releaseThreshold = 0)
        {
            State = state;
            ProcessId = processId;
            Runtime = runtime;
            Detail = detail;
            HeartbeatAgeMilliseconds = heartbeatAgeMilliseconds;
            SteadyRefcount = steadyRefcount;
            ReleaseThreshold = releaseThreshold;
        }

        public EHookOverlayStatus State { get; }

        public int ProcessId { get; }

        public string Runtime { get; }

        public string Detail { get; }

        public long HeartbeatAgeMilliseconds { get; }

        public int SteadyRefcount { get; }

        public int ReleaseThreshold { get; }
    }

    public interface IHookOverlayStatusService
    {
        HookOverlayStatus Current { get; }

        IObservable<HookOverlayStatus> StatusStream { get; }
    }
}
