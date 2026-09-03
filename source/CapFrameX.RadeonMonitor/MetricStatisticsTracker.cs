namespace CapFrameX.RadeonMonitor
{
    internal sealed class MetricStatisticsTracker
    {
        private readonly Dictionary<MetricKey, RunningStatistics> statistics = new();

        public IReadOnlyList<MetricReading> Update(IReadOnlyList<MetricReading> samples)
        {
            ArgumentNullException.ThrowIfNull(samples);

            List<MetricReading> result = new(samples.Count);
            foreach (MetricReading sample in samples)
            {
                MetricKey key = new(sample.Group, sample.Name);
                if (sample.NumericValue is not double numericValue || !double.IsFinite(numericValue))
                {
                    result.Add(statistics.TryGetValue(key, out RunningStatistics? existingStatistics)
                        ? ApplyStatistics(sample, existingStatistics)
                        : sample);
                    continue;
                }

                if (!statistics.TryGetValue(key, out RunningStatistics? runningStatistics))
                {
                    runningStatistics = new RunningStatistics(numericValue);
                    statistics.Add(key, runningStatistics);
                }
                else
                {
                    runningStatistics.Add(numericValue);
                }

                result.Add(ApplyStatistics(sample, runningStatistics));
            }

            return result;
        }

        private static MetricReading ApplyStatistics(
            MetricReading sample,
            RunningStatistics runningStatistics)
        {
            return sample with
            {
                MinimumValue = sample.FormatStatisticValue(runningStatistics.Minimum, isAverage: false),
                MaximumValue = sample.FormatStatisticValue(runningStatistics.Maximum, isAverage: false),
                AverageValue = sample.FormatStatisticValue(runningStatistics.Average, isAverage: true)
            };
        }

        public void Reset()
        {
            statistics.Clear();
        }

        private readonly record struct MetricKey(string Group, string Name);

        private sealed class RunningStatistics
        {
            private long sampleCount = 1;

            public RunningStatistics(double value)
            {
                Minimum = value;
                Maximum = value;
                Average = value;
            }

            public double Minimum { get; private set; }

            public double Maximum { get; private set; }

            public double Average { get; private set; }

            public void Add(double value)
            {
                sampleCount++;
                Minimum = Math.Min(Minimum, value);
                Maximum = Math.Max(Maximum, value);
                Average += (value - Average) / sampleCount;
            }
        }
    }
}
