using MathNet.Numerics.Integration;
using System.Collections.Generic;

namespace CapFrameX.Statistics.NetStandard
{
    class IntervalTimeAverageFilter
    {
        private readonly double _timeInterval;

        /// <summary>
        /// Time interval average filter
        /// </summary>
        /// <param name="timeInterval">time interval in milliseconds</param>
        public IntervalTimeAverageFilter(double timeInterval = 200)
        {
            _timeInterval = timeInterval;
        }

        public IList<Point> ProcessSamples(IList<double> sequence)
        {
            int length = sequence.Count;
            IList<Point> filteredSequence = new List<Point>();

            double intervalSum = 0;
            int intervalCount = 0;
            double timeAcc = 0; 
            for (int i = 0; i < length; i++)
            {
                intervalSum += sequence[i];
                intervalCount++;

                if (intervalSum >= _timeInterval)
                {
                    timeAcc += intervalSum;
                    filteredSequence.Add(new Point(timeAcc, intervalSum / intervalCount));
                    intervalSum = 0;
                    intervalCount = 0;
                }
            }

            timeAcc += intervalSum;
            filteredSequence.Add(new Point(timeAcc, intervalSum / intervalCount));

            return filteredSequence;
        }
    }
}
