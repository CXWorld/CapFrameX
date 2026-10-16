using System;
using System.Collections.Generic;

namespace CapFrameX.Statistics.NetStandard
{
    public class SampleBasedMovingAverage
    {
        private readonly int _sampleSize;

        /// <summary>
        /// Sample based moving average
        /// </summary>
        /// <param name="sampleSize">Number of samples to be used</param>
        public SampleBasedMovingAverage(int sampleSize)
        {
            _sampleSize = sampleSize;
        }

        public IList<double> ProcessSamples(IList<double> sequence)
        {
            int length = sequence.Count;
            // The window always holds at least the current sample.
            int windowSize = Math.Max(_sampleSize, 1);

            // A running sum cannot drop a NaN or an infinity again once it took one in.
            if (!AreFinite(sequence))
                return ProcessSamplesPerWindow(sequence, windowSize);

            IList<double> filteredSequence = new List<double>(length);
            double windowSum = 0;

            for (int i = 0; i < length; i++)
            {
                windowSum += GetContribution(sequence, i);
                if (i >= windowSize)
                    windowSum -= GetContribution(sequence, i - windowSize);

                filteredSequence.Add(windowSum / Math.Min(i + 1, windowSize));
            }

            return filteredSequence;
        }

        // A sample more than three times as large as its predecessor counts with the
        // predecessor's value. That does not depend on the window, so a running sum over the
        // window replaces walking the whole window back for every sample: O(n), not O(n x window).
        private static double GetContribution(IList<double> sequence, int index)
        {
            return index > 0 && sequence[index] > sequence[index - 1] * 3
                ? sequence[index - 1]
                : sequence[index];
        }

        private static bool AreFinite(IList<double> sequence)
        {
            for (int i = 0; i < sequence.Count; i++)
            {
                if (!double.IsFinite(sequence[i]))
                    return false;
            }

            return true;
        }

        private static IList<double> ProcessSamplesPerWindow(IList<double> sequence, int windowSize)
        {
            int length = sequence.Count;
            IList<double> filteredSequence = new List<double>(length);

            for (int i = 0; i < length; i++)
            {
                int localIndex = i;
                double localSum = 0;
                int localCount = 0;
                while (localIndex >= 0)
                {
                    localSum += GetContribution(sequence, localIndex);
                    localCount++;

                    if (localCount >= windowSize)
                        break;

                    localIndex--;
                }

                filteredSequence.Add(localSum / localCount);
            }

            return filteredSequence;
        }
    }
}
