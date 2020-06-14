using System.Collections.Generic;

namespace CapFrameX.Statistics.NetStandard
{
    public class TimeBasedMovingAverage
    {
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

                    if (localSum >= _timeWindow)
                        break;

                    localIndex--;
                }

                filteredSequence.Add(localSum / localCount);
            }

            return filteredSequence;
        }
    }
}
