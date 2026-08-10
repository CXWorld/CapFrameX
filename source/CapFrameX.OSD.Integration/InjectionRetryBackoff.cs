using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace CapFrameX.OSD.Integration
{
    /// <summary>
    /// Tracks an exponential retry delay per target PID. Callers are responsible for
    /// synchronizing access to an instance.
    /// </summary>
    internal sealed class InjectionRetryBackoff
    {
        private const int MaximumFailureCount = 6;

        private readonly Dictionary<int, RetryState> _states =
            new Dictionary<int, RetryState>();
        private readonly Func<long> _timestampProvider;
        private readonly long _timestampFrequency;

        internal InjectionRetryBackoff()
            : this(Stopwatch.GetTimestamp, Stopwatch.Frequency)
        {
        }

        internal InjectionRetryBackoff(Func<long> timestampProvider, long timestampFrequency)
        {
            _timestampProvider = timestampProvider ??
                throw new ArgumentNullException(nameof(timestampProvider));
            if (timestampFrequency <= 0)
                throw new ArgumentOutOfRangeException(nameof(timestampFrequency));

            _timestampFrequency = timestampFrequency;
        }

        internal bool IsBlocked(int pid)
        {
            return _states.TryGetValue(pid, out RetryState state) &&
                _timestampProvider() < state.RetryAt;
        }

        internal TimeSpan RecordFailure(int pid)
        {
            if (pid <= 0) throw new ArgumentOutOfRangeException(nameof(pid));

            if (!_states.TryGetValue(pid, out RetryState state))
            {
                state = new RetryState();
                _states.Add(pid, state);
            }

            state.FailureCount = Math.Min(state.FailureCount + 1, MaximumFailureCount);
            TimeSpan delay = GetDelay(state.FailureCount);
            long delayTicks = Math.Max(1L,
                (long)Math.Ceiling(delay.TotalSeconds * _timestampFrequency));
            long now = _timestampProvider();
            state.RetryAt = now > long.MaxValue - delayTicks
                ? long.MaxValue
                : now + delayTicks;

            return delay;
        }

        internal void Reset(int pid)
        {
            if (pid > 0) _states.Remove(pid);
        }

        internal void Prune(Func<int, bool> isProcessAlive)
        {
            if (isProcessAlive == null) throw new ArgumentNullException(nameof(isProcessAlive));

            var stalePids = new List<int>();
            foreach (int pid in _states.Keys)
            {
                if (!isProcessAlive(pid)) stalePids.Add(pid);
            }

            foreach (int pid in stalePids)
                _states.Remove(pid);
        }

        internal static TimeSpan GetDelay(int failureCount)
        {
            if (failureCount <= 0) throw new ArgumentOutOfRangeException(nameof(failureCount));
            if (failureCount >= MaximumFailureCount) return TimeSpan.FromSeconds(30);

            return TimeSpan.FromSeconds(1 << (failureCount - 1));
        }

        private sealed class RetryState
        {
            internal int FailureCount { get; set; }
            internal long RetryAt { get; set; }
        }
    }
}
