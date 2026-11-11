using System;
using System.Collections.Generic;

namespace CapFrameX.Statistics.NetStandard
{
    /// <summary>
    /// The mean of the samples of the last time window, kept up to date as samples arrive. Like
    /// <see cref="TimeBasedMovingAverage"/> it keeps a running sum and only ever moves the
    /// window start forward, so each sample is added once and removed once: O(1) amortized.
    /// </summary>
    public sealed class SlidingWindowAverage
    {
        private readonly Queue<(double Time, double Value)> _samples = new Queue<(double Time, double Value)>();
        private double _sum;
        private double _lastTime = double.NegativeInfinity;

        /// <summary>
        /// Creates an empty average over the given window.
        /// </summary>
        /// <param name="windowMilliseconds">Length of the window in milliseconds</param>
        public SlidingWindowAverage(double windowMilliseconds)
        {
            if (!(windowMilliseconds > 0) || double.IsInfinity(windowMilliseconds))
                throw new ArgumentOutOfRangeException(nameof(windowMilliseconds));
            WindowMilliseconds = windowMilliseconds;
        }

        /// <summary>Length of the window in milliseconds.</summary>
        public double WindowMilliseconds { get; }

        /// <summary>Number of samples in the window as of the latest sample.</summary>
        public int Count => _samples.Count;

        /// <summary>
        /// Adds a sample taken at <paramref name="timeMilliseconds"/>. A missing (non-finite)
        /// reading is skipped. A clock that steps back starts a new window.
        /// </summary>
        public void Add(double timeMilliseconds, double value)
        {
            if (!double.IsFinite(timeMilliseconds) || !double.IsFinite(value))
                return;
            if (timeMilliseconds < _lastTime)
                Clear();

            _lastTime = timeMilliseconds;
            _samples.Enqueue((timeMilliseconds, value));
            _sum += value;
            Trim(timeMilliseconds);
        }

        /// <summary>
        /// The mean of the samples newer than one window before <paramref name="nowMilliseconds"/>,
        /// or null when there are none.
        /// </summary>
        public double? Average(double nowMilliseconds)
        {
            Trim(nowMilliseconds);
            return _samples.Count > 0 ? _sum / _samples.Count : (double?)null;
        }

        /// <summary>Drops every sample.</summary>
        public void Clear()
        {
            _samples.Clear();
            _sum = 0;
            _lastTime = double.NegativeInfinity;
        }

        private void Trim(double nowMilliseconds)
        {
            while (_samples.Count > 0 && nowMilliseconds - _samples.Peek().Time >= WindowMilliseconds)
                _sum -= _samples.Dequeue().Value;

            // An empty window starts from an exact zero, so rounding of the running sum cannot
            // build up over a long session.
            if (_samples.Count == 0)
                _sum = 0;
        }
    }
}
