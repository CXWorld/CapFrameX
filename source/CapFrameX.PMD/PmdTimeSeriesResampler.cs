using System;
using System.Collections.Generic;

namespace CapFrameX.PMD
{
    /// <summary>
    /// Puts PMD samples taken at irregular times onto the regular grid a capture file stores.
    /// </summary>
    /// <remarks>
    /// A capture file stores PMD values without times: value i belongs to i * SampleTime after the
    /// first frame. No PMD device delivers exactly one sample per SampleTime (a Powenetics unit sends
    /// 979 instead of 1000 packets/s, BENCHLAB requests take longer than their interval), so storing
    /// the samples as they came stretches or compresses the series against the frame times.
    ///
    /// Each sample stands for the span halfway to its neighbours; the first and the last sample
    /// extend without limit. A grid cell is centred on its grid time and takes the time-weighted mean
    /// of the samples it overlaps, which keeps the average power.
    /// </remarks>
    public static class PmdTimeSeriesResampler
    {
        /// <summary>
        /// Resamples a series onto <paramref name="count"/> cells centred on
        /// <paramref name="gridStart"/> + i * <paramref name="step"/>.
        /// </summary>
        /// <param name="times">Sample times in seconds, not decreasing.</param>
        /// <param name="values">Sample values, one per time.</param>
        /// <param name="gridStart">Time of grid value 0 in seconds.</param>
        /// <param name="step">Grid step in seconds.</param>
        /// <param name="count">Number of grid values.</param>
        /// <returns>The grid values; empty when there are no samples.</returns>
        public static float[] Resample(IReadOnlyList<double> times, IReadOnlyList<float> values,
            double gridStart, double step, int count)
        {
            if (times == null) throw new ArgumentNullException(nameof(times));
            if (values == null) throw new ArgumentNullException(nameof(values));
            if (times.Count != values.Count)
                throw new ArgumentException("Every sample needs a time.", nameof(values));
            if (!(step > 0))
                throw new ArgumentOutOfRangeException(nameof(step));

            int sampleCount = times.Count;
            if (sampleCount == 0 || count <= 0)
                return Array.Empty<float>();

            var result = new float[count];
            int first = 0;

            for (int cell = 0; cell < count; cell++)
            {
                double cellStart = gridStart + (cell - 0.5) * step;
                double cellEnd = cellStart + step;

                while (first < sampleCount - 1 && SpanEnd(times, first) <= cellStart)
                    first++;

                double sum = 0;
                double weight = 0;

                for (int i = first; i < sampleCount; i++)
                {
                    double overlapStart = i == 0 ? cellStart : Math.Max(cellStart, SpanEnd(times, i - 1));
                    double overlapEnd = i == sampleCount - 1 ? cellEnd : Math.Min(cellEnd, SpanEnd(times, i));

                    if (overlapEnd > overlapStart)
                    {
                        sum += values[i] * (overlapEnd - overlapStart);
                        weight += overlapEnd - overlapStart;
                    }

                    if (i == sampleCount - 1 || SpanEnd(times, i) >= cellEnd)
                        break;
                }

                result[cell] = weight > 0 ? (float)(sum / weight) : values[first];
            }

            return result;
        }

        // End of the span of sample i, halfway to sample i + 1.
        private static double SpanEnd(IReadOnlyList<double> times, int i) => 0.5 * (times[i] + times[i + 1]);
    }
}
