using System;
using System.Collections.Generic;

namespace CapFrameX.Statistics.NetStandard
{
    public class TimeBasedMovingAverage
    {
        // How close a window sum may come to the time window before the running sum no longer
        // decides alone. The walk's own rounding stays far below that for any window shorter
        // than a million samples.
        private const double TieTolerance = 1e-9;

        private readonly double _timeWindow;

        /// <summary>
        /// Time based moving average
        /// </summary>
        /// <param name="timeWindow">Time window in milliseconds</param>
        public TimeBasedMovingAverage(double timeWindow)
        {
            _timeWindow = timeWindow;
        }

        public IList<double> ProcessSamples(IList<double> sequence)
        {
            // The window is the shortest run of samples ending at the current one whose sum
            // reaches the time window, or every sample so far. With non-negative finite samples
            // its start never moves back, so two pointers find it in O(n) instead of walking the
            // window back for every sample. Any other input keeps that walk.
            if (!AreNonNegativeAndFinite(sequence))
                return ProcessSamplesPerWindow(sequence);

            int length = sequence.Count;
            IList<double> filteredSequence = new List<double>(length);
            int windowStart = 0;
            double windowSum = 0;

            for (int i = 0; i < length; i++)
            {
                windowSum += sequence[i];

                while (windowStart < i && windowSum - sequence[windowStart] >= _timeWindow)
                {
                    windowSum -= sequence[windowStart];
                    windowStart++;
                }

                // The walk sums backwards from the current sample, the running sum adds and
                // removes, so their last bits differ. Frame times with three decimals often sum
                // to exactly the time window; there only the walk decides as it always did.
                bool nearTimeWindow = IsNearTimeWindow(windowSum)
                    || (windowStart < i && IsNearTimeWindow(windowSum - sequence[windowStart]));

                filteredSequence.Add(nearTimeWindow
                    ? AverageOfWindowEndingAt(sequence, i)
                    : windowSum / (i - windowStart + 1));
            }

            return filteredSequence;
        }

        private bool IsNearTimeWindow(double sum)
        {
            return double.IsFinite(_timeWindow)
                && Math.Abs(sum - _timeWindow) <= TieTolerance * Math.Max(1, Math.Abs(_timeWindow));
        }

        private static bool AreNonNegativeAndFinite(IList<double> sequence)
        {
            for (int i = 0; i < sequence.Count; i++)
            {
                if (!(sequence[i] >= 0) || double.IsPositiveInfinity(sequence[i]))
                    return false;
            }

            return true;
        }

        private IList<double> ProcessSamplesPerWindow(IList<double> sequence)
        {
            int length = sequence.Count;
            IList<double> filteredSequence = new List<double>(length);

            for (int i = 0; i < length; i++)
                filteredSequence.Add(AverageOfWindowEndingAt(sequence, i));

            return filteredSequence;
        }

        private double AverageOfWindowEndingAt(IList<double> sequence, int index)
        {
            int localIndex = index;
            double localSum = 0;
            int localCount = 0;
            while (localIndex >= 0)
            {
                localSum += sequence[localIndex];
                localCount++;

                if (localSum >= _timeWindow)
                    break;

                localIndex--;
            }

            return localSum / localCount;
        }
    }
}
