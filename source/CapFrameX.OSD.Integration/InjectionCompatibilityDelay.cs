using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace CapFrameX.OSD.Integration
{
    /// <summary>
    /// Starts each configured compatibility delay once per PID. A failed injection retry waits
    /// only for the remainder of the original window instead of applying the full delay again.
    /// Callers synchronize access to an instance.
    /// </summary>
    internal sealed class InjectionCompatibilityDelay
    {
        private readonly Dictionary<int, long> _deadlines = new Dictionary<int, long>();
        private readonly Func<long> _timestampProvider;
        private readonly long _timestampFrequency;

        internal InjectionCompatibilityDelay()
            : this(Stopwatch.GetTimestamp, Stopwatch.Frequency)
        {
        }

        internal InjectionCompatibilityDelay(Func<long> timestampProvider,
            long timestampFrequency)
        {
            _timestampProvider = timestampProvider ??
                throw new ArgumentNullException(nameof(timestampProvider));
            if (timestampFrequency <= 0)
                throw new ArgumentOutOfRangeException(nameof(timestampFrequency));
            _timestampFrequency = timestampFrequency;
        }

        internal TimeSpan GetRemainingDelay(int processId, TimeSpan configuredDelay)
        {
            if (processId <= 0) throw new ArgumentOutOfRangeException(nameof(processId));
            if (configuredDelay <= TimeSpan.Zero) return TimeSpan.Zero;

            long now = _timestampProvider();
            if (!_deadlines.TryGetValue(processId, out long deadline))
            {
                long delayTicks = Math.Max(1L, (long)Math.Ceiling(
                    configuredDelay.TotalSeconds * _timestampFrequency));
                deadline = now > long.MaxValue - delayTicks
                    ? long.MaxValue
                    : now + delayTicks;
                _deadlines.Add(processId, deadline);
            }

            if (now >= deadline) return TimeSpan.Zero;
            return TimeSpan.FromSeconds((double)(deadline - now) / _timestampFrequency);
        }

        internal void Reset(int processId)
        {
            if (processId > 0) _deadlines.Remove(processId);
        }

        internal void Prune(Func<int, bool> isProcessAlive)
        {
            if (isProcessAlive == null) throw new ArgumentNullException(nameof(isProcessAlive));
            var stalePids = new List<int>();
            foreach (int processId in _deadlines.Keys)
            {
                if (!isProcessAlive(processId)) stalePids.Add(processId);
            }
            foreach (int processId in stalePids)
                _deadlines.Remove(processId);
        }
    }
}
