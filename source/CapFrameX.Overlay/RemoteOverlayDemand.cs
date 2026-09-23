using CapFrameX.Contracts.Overlay;
using System;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;

namespace CapFrameX.Overlay
{
    public sealed class RemoteOverlayDemand : IRemoteOverlayDemand
    {
        // Must outlast a generous poll interval: once it runs out, the next read gets the values
        // from the moment the refresh stopped.
        internal static readonly TimeSpan DefaultRequestLease = TimeSpan.FromSeconds(30);

        private readonly object _gate = new object();
        private readonly IScheduler _scheduler;
        private readonly TimeSpan _requestLease;
        private readonly BehaviorSubject<bool> _isActive = new BehaviorSubject<bool>(false);
        private readonly SerialDisposable _leaseTimer = new SerialDisposable();
        private DateTimeOffset _leaseEnd = DateTimeOffset.MinValue;
        private int _streamingClients;

        public RemoteOverlayDemand()
            : this(Scheduler.Default, DefaultRequestLease)
        {
        }

        internal RemoteOverlayDemand(IScheduler scheduler, TimeSpan requestLease)
        {
            _scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
            _requestLease = requestLease;
        }

        public bool IsActive => _isActive.Value;

        public IObservable<bool> IsActiveStream => _isActive.AsObservable();

        public void RegisterRequest()
        {
            lock (_gate)
            {
                DateTimeOffset now = _scheduler.Now;
                // A running lease already has its timer armed; the timer re-arms itself for an
                // extension, so a client polling every frame does not reschedule it every time.
                if (_leaseEnd <= now)
                    _leaseTimer.Disposable = _scheduler.Schedule(_requestLease, OnLeaseTimer);

                _leaseEnd = now + _requestLease;
                PublishLocked();
            }
        }

        public IDisposable AcquireStreamingClient()
        {
            lock (_gate)
            {
                _streamingClients++;
                PublishLocked();
            }

            return Disposable.Create(() =>
            {
                lock (_gate)
                {
                    _streamingClients--;
                    PublishLocked();
                }
            });
        }

        private void OnLeaseTimer()
        {
            lock (_gate)
            {
                TimeSpan remaining = _leaseEnd - _scheduler.Now;
                if (remaining > TimeSpan.Zero)
                {
                    _leaseTimer.Disposable = _scheduler.Schedule(remaining, OnLeaseTimer);
                    return;
                }

                PublishLocked();
            }
        }

        // Publishes under the gate so subscribers see the changes in order. They must not call
        // back into this instance synchronously.
        private void PublishLocked()
        {
            bool active = _streamingClients > 0 || _scheduler.Now < _leaseEnd;
            if (active != _isActive.Value)
                _isActive.OnNext(active);
        }
    }
}
