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
            IList<double> filteredSequence = new List<double>(length);

            for (int i = 0; i < length; i++)
            {
                int localIndex = i;
                double localSum = 0;
                int localCount = 0;
                while (localIndex >= 0)
                {
                    localSum += sequence[localIndex];
                    localCount++;

                    if (localCount >= _sampleSize)
                        break;

                    localIndex--;
                }

                filteredSequence.Add(localSum / localCount);
            }

            return filteredSequence;
        }
    }
}
