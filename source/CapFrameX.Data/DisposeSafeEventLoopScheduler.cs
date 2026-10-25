using System;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;

namespace CapFrameX.Data
{
    /// <summary>
    /// An <see cref="EventLoopScheduler"/> that drops work scheduled after it was disposed
    /// instead of throwing. Disposing a subscription does not wait for an OnNext that is already
    /// running on the producer's thread, so ObserveOn can still hand that item to the scheduler
    /// its owner disposed right after the subscription. EventLoopScheduler then throws
    /// ObjectDisposedException into the producer: PresentMon's stdout reader for the capture
    /// stream, the device readers for PMD. The item belongs to a subscription that no longer
    /// exists, so dropping it loses nothing.
    /// </summary>
    internal sealed class DisposeSafeEventLoopScheduler : IScheduler, IDisposable
    {
        private readonly EventLoopScheduler _inner = new EventLoopScheduler();
        private volatile bool _disposed;

        public DateTimeOffset Now => _inner.Now;

        public IDisposable Schedule<TState>(TState state,
            Func<IScheduler, TState, IDisposable> action)
        {
            if (_disposed) return Disposable.Empty;
            try
            {
                return _inner.Schedule((Owner: this, State: state, Action: action),
                    static (_, work) => work.Action(work.Owner, work.State));
            }
            catch (ObjectDisposedException) when (_disposed)
            {
                return Disposable.Empty;
            }
        }

        public IDisposable Schedule<TState>(TState state, TimeSpan dueTime,
            Func<IScheduler, TState, IDisposable> action)
        {
            if (_disposed) return Disposable.Empty;
            try
            {
                return _inner.Schedule((Owner: this, State: state, Action: action), dueTime,
                    static (_, work) => work.Action(work.Owner, work.State));
            }
            catch (ObjectDisposedException) when (_disposed)
            {
                return Disposable.Empty;
            }
        }

        public IDisposable Schedule<TState>(TState state, DateTimeOffset dueTime,
            Func<IScheduler, TState, IDisposable> action)
            => Schedule(state, Scheduler.Normalize(dueTime - Now), action);

        public void Dispose()
        {
            // Set first: a Schedule call that loses the race to the inner Dispose sees it.
            _disposed = true;
            _inner.Dispose();
        }
    }
}
