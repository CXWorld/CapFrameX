using System;
using System.Reactive.Subjects;
using CapFrameX.Contracts.Overlay;

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
                lock (_gate) _current = status;
                _statusStream.OnNext(status);
            }
        }

        public void Dispose()
        {
            _statusStream.Dispose();
        }
    }
}
