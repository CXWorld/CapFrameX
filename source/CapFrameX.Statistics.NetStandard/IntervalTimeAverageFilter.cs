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
        public IntervalTimeAverageFilter(double timeInterval = 500)
        {
            // Milliseconds to seconds
            _timeInterval = timeInterval/ 1000;
        }

        public IList<Point> ProcessSamples(IList<Point> sequence, double startTime, double endTime, double maxRecordingTime)
        {
            int length = sequence.Count;
            IList<Point> filteredSequence = new List<Point>();

            double intervalSumX = 0;
            double intervalSumY = 0;
            int intervalCount = 0;
            double timeAcc = 0;
            for (int i = 0; i < length; i++)
            {
                intervalSumX += sequence[i].X;
                intervalSumY += sequence[i].Y;
                intervalCount++;

                if (intervalSumX >= _timeInterval)
                {
                    timeAcc += intervalSumX;
                    filteredSequence.Add(new Point(timeAcc, intervalSumY / intervalCount));
                    intervalSumX = 0;
                    intervalSumY = 0;
                    intervalCount = 0;
                }
            }

            if (intervalCount > 0)
            {
                timeAcc += intervalSumX;
                filteredSequence.Add(new Point(timeAcc, intervalSumY / intervalCount));
            }

            if (filteredSequence[0].X < startTime || endTime < maxRecordingTime)
            {
                IList<Point> filteredSequenceCopy = new List<Point>(filteredSequence);

                for (int i = 0; i < filteredSequenceCopy.Count; i++)
                {
                    if (filteredSequenceCopy[i].X < startTime)
                        filteredSequence.Remove(filteredSequence[0]);
                    else if (filteredSequenceCopy[i].X > endTime)
                        filteredSequence.Remove(filteredSequence[filteredSequence.Count - 1]);
                }
            }

            return filteredSequence;
        }
    }
}
