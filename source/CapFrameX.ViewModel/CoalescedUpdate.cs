using System;
using System.Reactive.Concurrency;

namespace CapFrameX.ViewModel
{
    /// <summary>
    /// Runs an update at most once per interval while requests keep coming, and once more after the
    /// last request. At most one update waits at a time, so a slow update never queues up behind a
    /// fast stream of requests; the update reads the current state when it runs.
    /// </summary>
    /// <remarks>
    /// Rx's Throttle does not fit a value that changes continuously, such as a dragged slider: it only
    /// passes a value through after an interval without changes, so nothing ran until the drag stopped.
    /// </remarks>
    internal sealed class CoalescedUpdate
    {
        private readonly object _gate = new object();
        private readonly IScheduler _scheduler;
        private readonly TimeSpan _interval;
        private readonly Action _update;
        private bool _isPending;

        public CoalescedUpdate(TimeSpan interval, IScheduler scheduler, Action update)
        {
            _interval = interval;
            _scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
            _update = update ?? throw new ArgumentNullException(nameof(update));
        }

        public void Request()
        {
            lock (_gate)
            {
                if (_isPending)
                    return;
                _isPending = true;
            }

            _scheduler.Schedule(_interval, () =>
            {
                // Cleared before the update runs: a request made while it runs schedules the next one.
                lock (_gate)
                    _isPending = false;
                _update();
            });
        }
    }
}
