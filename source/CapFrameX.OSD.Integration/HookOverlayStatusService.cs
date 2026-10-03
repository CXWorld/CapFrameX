using System;
using System.Reactive.Subjects;
using CapFrameX.Contracts.Overlay;
using Serilog;

namespace CapFrameX.OSD.Integration
{
    public sealed class HookOverlayStatusService : IHookOverlayStatusService, IDisposable
    {
        private readonly object _gate = new object();
        private readonly object _publishGate = new object();
        private readonly BehaviorSubject<HookOverlayStatus> _statusStream;
        private HookOverlayStatus _current;

        public HookOverlayStatusService()
        {
            _current = new HookOverlayStatus(EHookOverlayStatus.Disabled,
                detail: "The in-game hook overlay is disabled.");
            _statusStream = new BehaviorSubject<HookOverlayStatus>(_current);
        }

        public HookOverlayStatus Current
        {
            get
            {
                lock (_gate) return _current;
            }
        }

        public IObservable<HookOverlayStatus> StatusStream => _statusStream;

        internal void Publish(HookOverlayStatus status)
        {
            if (status == null) return;
            lock (_publishGate)
            {
                HookOverlayStatus previous;
                lock (_gate)
                {
                    previous = _current;
                    _current = status;
                }
                if (previous.State != status.State ||
                    previous.ProcessId != status.ProcessId ||
                    !string.Equals(previous.Runtime, status.Runtime,
                        StringComparison.Ordinal) ||
                    !string.Equals(previous.Detail, status.Detail,
                        StringComparison.Ordinal))
                {
                    Log.Debug(
                        "HookOverlay: status {state} for pid {pid} runtime {runtime}: {detail}",
                        status.State, status.ProcessId,
                        string.IsNullOrWhiteSpace(status.Runtime) ? "unknown" : status.Runtime,
                        status.Detail);
                }
                _statusStream.OnNext(status);
            }
        }

        public void Dispose()
        {
            _statusStream.Dispose();
        }
    }
}
