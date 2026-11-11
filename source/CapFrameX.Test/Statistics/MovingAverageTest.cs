using CapFrameX.Statistics.NetStandard;
using CapFrameX.Statistics.NetStandard.Contracts;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace CapFrameX.Test.Statistics
{
    /// <summary>
    /// The moving averages run in O(n). They must produce what the per-sample walk over the
    /// window produced before, which is kept below as the reference.
    /// </summary>
    [TestClass]
    public class MovingAverageTest
    {
        // A running sum adds and removes in another order than the walk, so the last bits may
        // differ; anything beyond rounding would be a different window.
        private const double RelativeTolerance = 1e-9;

        #region Sample based

        [TestMethod]
        [DataRow(1)]
        [DataRow(2)]
        [DataRow(41)]   // 60 fps: sqrt(16.7) x 10, as GetMovingAverage chooses it
        [DataRow(72)]
        [DataRow(250)]
        [DataRow(5000)] // longer than the capture
        public void SampleBased_MatchesTheReference_OnARecordedCapture(int sampleSize)
        {
            double[] frametimes = RecordedFrametimes();

            var actual = new SampleBasedMovingAverage(sampleSize).ProcessSamples(frametimes);

            AssertMatches(ReferenceSampleBased(frametimes, sampleSize), actual);
        }

        [TestMethod]
        [DataRow(1, 41)]
        [DataRow(2, 72)]
        [DataRow(3, 250)]
        public void SampleBased_MatchesTheReference_OnFrametimesWithSpikesAndHitches(int seed, int sampleSize)
        {
            double[] frametimes = SyntheticFrametimes(20_000, seed);

            var actual = new SampleBasedMovingAverage(sampleSize).ProcessSamples(frametimes);

            AssertMatches(ReferenceSampleBased(frametimes, sampleSize), actual);
        }

        [TestMethod]
        public void SampleBased_IsExact_ForIntegerSamples()
        {
            var random = new Random(7);
            double[] samples = Enumerable.Range(0, 5_000).Select(_ => (double)random.Next(0, 200)).ToArray();

            var actual = new SampleBasedMovingAverage(37).ProcessSamples(samples);

            CollectionAssert.AreEqual(ReferenceSampleBased(samples, 37).ToArray(), actual.ToArray());
        }

        [TestMethod]
        public void SampleBased_CountsASpikeWithItsPredecessor()
        {
            // 50 is more than three times 10 and counts as 10; 30 is not and counts as itself.
            var actual = new SampleBasedMovingAverage(3).ProcessSamples(new double[] { 10, 30, 10, 50, 10 });

            CollectionAssert.AreEqual(new[] { 10d, 20d, 50d / 3, 50d / 3, 10d }, actual.ToArray());
        }

        [TestMethod]
        [DataRow(0)]
        [DataRow(-5)]
        public void SampleBased_NonPositiveSampleSize_KeepsOneSample(int sampleSize)
        {
            double[] samples = { 10, 50, 20 };

            var actual = new SampleBasedMovingAverage(sampleSize).ProcessSamples(samples);

            CollectionAssert.AreEqual(new[] { 10d, 10d, 20d }, actual.ToArray());
            CollectionAssert.AreEqual(ReferenceSampleBased(samples, sampleSize).ToArray(), actual.ToArray());
        }

        [TestMethod]
        public void SampleBased_EmptyAndSingleSample()
        {
            Assert.AreEqual(0, new SampleBasedMovingAverage(41).ProcessSamples(new double[0]).Count);
            CollectionAssert.AreEqual(new[] { 16.7 },
                new SampleBasedMovingAverage(41).ProcessSamples(new[] { 16.7 }).ToArray());
        }

        [TestMethod]
        public void SampleBased_NonFiniteSamples_MatchTheReferenceExactly()
        {
            // A running sum would keep a NaN or an infinity after it left the window.
            double[] samples = WithNonFiniteValues(SyntheticFrametimes(400, 11));

            var actual = new SampleBasedMovingAverage(10).ProcessSamples(samples);

            CollectionAssert.AreEqual(ReferenceSampleBased(samples, 10).ToArray(), actual.ToArray());
            Assert.IsFalse(double.IsNaN(actual[actual.Count - 1]), "A NaN must not outlive its window.");
        }

        [TestMethod]
        public void SampleBased_ReadsEverySampleOnlyAFewTimes()
        {
            // Regression: the walk read sampleSize samples (three times each) for every sample.
            var samples = new CountingList(SyntheticFrametimes(10_000, 5));

            new SampleBasedMovingAverage(250).ProcessSamples(samples);

            Assert.IsTrue(samples.Reads <= 8L * samples.Count,
                $"{samples.Reads} reads for {samples.Count} samples");
        }

        #endregion

        #region Time based

        [TestMethod]
        [DataRow(0d)]
        [DataRow(16d)]
        [DataRow(100d)]
        [DataRow(500d)]
        [DataRow(1000d)]
        [DataRow(2000d)]
        [DataRow(1e9)] // longer than the capture
        public void TimeBased_MatchesTheReference_OnARecordedCapture(double timeWindow)
        {
            double[] frametimes = RecordedFrametimes();

            var actual = new TimeBasedMovingAverage(timeWindow).ProcessSamples(frametimes);

            AssertMatches(ReferenceTimeBased(frametimes, timeWindow), actual);
        }

        [TestMethod]
        [DataRow(1, 500d)]
        [DataRow(2, 1000d)]
        [DataRow(3, 2000d)]
        public void TimeBased_MatchesTheReference_OnFrametimesWithSpikesAndHitches(int seed, double timeWindow)
        {
            double[] frametimes = SyntheticFrametimes(20_000, seed);

            var actual = new TimeBasedMovingAverage(timeWindow).ProcessSamples(frametimes);

            AssertMatches(ReferenceTimeBased(frametimes, timeWindow), actual);
        }

        [TestMethod]
        public void TimeBased_WindowSummingExactlyToTheTime_KeepsTheReferenceWindow()
        {
            // Frames 1749..1940 of the capture sum to exactly 2000.000 ms. Summed backwards they
            // fall short by one bit, so the walk took frame 1748 in as well; the running sum
            // reaches 2000 and would have stopped one frame earlier.
            double[] frametimes = RecordedFrametimes();
            double expected = ReferenceTimeBased(frametimes, 2000)[1940];

            var actual = new TimeBasedMovingAverage(2000).ProcessSamples(frametimes);

            Assert.AreEqual(expected, actual[1940]);
            Assert.AreEqual(2010.209 / 193, actual[1940], 1e-9);
        }

        [TestMethod]
        [DataRow(1, 500d)]
        [DataRow(2, 1000d)]
        [DataRow(3, 2000d)]
        public void TimeBased_MatchesTheReference_OnFrametimesWithThreeDecimals(int seed, double timeWindow)
        {
            // Like PresentMon writes them: window sums hit the time window exactly again and again.
            var random = new Random(seed);
            double[] frametimes = Enumerable.Range(0, 50_000)
                .Select(_ => Math.Round(5 + random.NextDouble() * 20, 3))
                .ToArray();

            var actual = new TimeBasedMovingAverage(timeWindow).ProcessSamples(frametimes);

            AssertMatches(ReferenceTimeBased(frametimes, timeWindow), actual);
        }

        [TestMethod]
        public void TimeBased_IsExact_ForIntegerSamples()
        {
            var random = new Random(9);
            double[] samples = Enumerable.Range(0, 5_000).Select(_ => (double)random.Next(0, 60)).ToArray();

            var actual = new TimeBasedMovingAverage(500).ProcessSamples(samples);

            CollectionAssert.AreEqual(ReferenceTimeBased(samples, 500).ToArray(), actual.ToArray());
        }

        [TestMethod]
        public void TimeBased_UsesTheShortestWindowThatReachesTheTime()
        {
            // 10+10 reaches 20, so the window stops there; 30 alone already reaches it.
            var actual = new TimeBasedMovingAverage(20).ProcessSamples(new double[] { 10, 10, 10, 30, 5 });

            CollectionAssert.AreEqual(new[] { 10d, 10d, 10d, 30d, 17.5 }, actual.ToArray());
        }

        [TestMethod]
        public void TimeBased_WindowLongerThanTheCapture_AveragesEverySampleSoFar()
        {
            var actual = new TimeBasedMovingAverage(1000).ProcessSamples(new double[] { 10, 20, 30 });

            CollectionAssert.AreEqual(new[] { 10d, 15d, 20d }, actual.ToArray());
        }

        [TestMethod]
        [DataRow(0d)]
        [DataRow(-10d)]
        public void TimeBased_NonPositiveWindow_KeepsOneSample(double timeWindow)
        {
            double[] samples = { 10, 0, 30 };

            var actual = new TimeBasedMovingAverage(timeWindow).ProcessSamples(samples);

            CollectionAssert.AreEqual(samples, actual.ToArray());
        }

        [TestMethod]
        public void TimeBased_NaNWindow_AveragesEverySampleSoFar()
        {
            double[] samples = { 10, 20, 30 };

            var actual = new TimeBasedMovingAverage(double.NaN).ProcessSamples(samples);

            CollectionAssert.AreEqual(new[] { 10d, 15d, 20d }, actual.ToArray());
            CollectionAssert.AreEqual(ReferenceTimeBased(samples, double.NaN).ToArray(), actual.ToArray());
        }

        [TestMethod]
        public void TimeBased_ZeroSamples_MatchTheReferenceExactly()
        {
            double[] samples = { 0, 0, 0, 10, 0, 0, 5, 0, 20, 0 };

            var actual = new TimeBasedMovingAverage(5).ProcessSamples(samples);

            CollectionAssert.AreEqual(ReferenceTimeBased(samples, 5).ToArray(), actual.ToArray());
        }

        [TestMethod]
        public void TimeBased_EmptyAndSingleSample()
        {
            Assert.AreEqual(0, new TimeBasedMovingAverage(500).ProcessSamples(new double[0]).Count);
            CollectionAssert.AreEqual(new[] { 16.7 },
                new TimeBasedMovingAverage(500).ProcessSamples(new[] { 16.7 }).ToArray());
        }

        [TestMethod]
        public void TimeBased_NonFiniteSamples_MatchTheReferenceExactly()
        {
            double[] samples = WithNonFiniteValues(SyntheticFrametimes(400, 13));

            var actual = new TimeBasedMovingAverage(100).ProcessSamples(samples);

            CollectionAssert.AreEqual(ReferenceTimeBased(samples, 100).ToArray(), actual.ToArray());
        }

        [TestMethod]
        public void TimeBased_NegativeSamples_MatchTheReferenceExactly()
        {
            // A negative sample can shrink a window again, which two pointers cannot follow.
            double[] samples = { 10, 40, -35, 10, 10, -5, 30, 10 };

            var actual = new TimeBasedMovingAverage(20).ProcessSamples(samples);

            CollectionAssert.AreEqual(ReferenceTimeBased(samples, 20).ToArray(), actual.ToArray());
        }

        [TestMethod]
        public void TimeBased_ReadsEverySampleOnlyAFewTimes()
        {
            var samples = new CountingList(SyntheticFrametimes(10_000, 6));

            new TimeBasedMovingAverage(2000).ProcessSamples(samples);

            Assert.IsTrue(samples.Reads <= 8L * samples.Count,
                $"{samples.Reads} reads for {samples.Count} samples");
        }

        #endregion

        #region Metrics built on the moving averages

        [TestMethod]
        public void StutteringAndLowFpsTime_MatchTheReferenceOnARecordedCapture()
        {
            double[] frametimes = RecordedFrametimes();
            var provider = CreateProvider();
            var average = ReferenceSampleBased(frametimes,
                Convert.ToInt32(Math.Sqrt(frametimes.Average()) * 10));
            double expectedStutteringTime = Enumerable.Range(0, frametimes.Length)
                .Where(i => frametimes[i] > 2.5 * average[i]).Sum(i => frametimes[i]);
            double expectedLowFpsTime = Enumerable.Range(0, frametimes.Length)
                .Where(i => frametimes[i] <= 2.5 * average[i] && 1000 / frametimes[i] < 100)
                .Sum(i => frametimes[i]);

            Assert.AreEqual(100 * expectedStutteringTime / frametimes.Sum(),
                provider.GetStutteringTimePercentage(frametimes, 2.5));
            Assert.AreEqual(100 * expectedLowFpsTime / frametimes.Sum(),
                provider.GetLowFPSTimePercentage(frametimes, 2.5, 100));
        }

        [TestMethod]
        public void AdaptiveStandardDeviation_MatchesTheReferenceOnARecordedCapture()
        {
            double[] frametimes = RecordedFrametimes();
            var average = ReferenceTimeBased(frametimes, 500);
            double expected = Math.Sqrt(frametimes.Select((value, i) => Math.Pow(value - average[i], 2)).Sum()
                / (frametimes.Length - 1));

            double actual = CreateProvider().GetAdaptiveStandardDeviation(frametimes, 500);

            Assert.AreEqual(expected, actual, expected * RelativeTolerance);
        }

        #endregion

        #region Reference and helpers

        // SampleBasedMovingAverage.ProcessSamples before the O(n) rewrite.
        private static IList<double> ReferenceSampleBased(IList<double> sequence, int sampleSize)
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
                    if (localIndex > 0 && sequence[localIndex] > sequence[localIndex - 1] * 3)
                        localSum += sequence[localIndex - 1];
                    else
                        localSum += sequence[localIndex];

                    localCount++;

                    if (localCount >= sampleSize)
                        break;

                    localIndex--;
                }

                filteredSequence.Add(localSum / localCount);
            }

            return filteredSequence;
        }

        // TimeBasedMovingAverage.ProcessSamples before the O(n) rewrite.
        private static IList<double> ReferenceTimeBased(IList<double> sequence, double timeWindow)
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

                    if (localSum >= timeWindow)
                        break;

                    localIndex--;
                }

                filteredSequence.Add(localSum / localCount);
            }

            return filteredSequence;
        }

        private static void AssertMatches(IList<double> expected, IList<double> actual)
        {
            Assert.AreEqual(expected.Count, actual.Count);
            for (int i = 0; i < expected.Count; i++)
            {
                double tolerance = Math.Max(1, Math.Abs(expected[i])) * RelativeTolerance;
                Assert.AreEqual(expected[i], actual[i], tolerance, $"Sample {i} differs.");
            }
        }

        private static FrametimeStatisticProvider CreateProvider()
        {
            var options = new Mock<IFrametimeStatisticProviderOptions>();
            options.Setup(o => o.FpsValuesRoundingDigits).Returns(2);
            options.Setup(o => o.IntervalAverageWindowTime).Returns(500);
            return new FrametimeStatisticProvider(options.Object);
        }

        private static double[] RecordedFrametimes()
        {
            string[] lines = File.ReadAllLines(
                Path.Combine("TestRecordFiles", "OCAT-MetroExodus.exe-2019-02-20T101522.csv"));
            int column = Array.IndexOf(lines[0].Split(','), "MsBetweenPresents");
            Assert.IsTrue(column >= 0, "The capture has no MsBetweenPresents column.");
            return lines.Skip(1)
                .Where(line => line.Length > 0)
                .Select(line => double.Parse(line.Split(',')[column], CultureInfo.InvariantCulture))
                .ToArray();
        }

        // Jittered frame times at 30 to 144 fps with spikes past three times the previous
        // frame, long hitches and zero-length frames.
        private static double[] SyntheticFrametimes(int count, int seed)
        {
            var random = new Random(seed);
            var samples = new double[count];
            double baseFrametime = 16.7;
            for (int i = 0; i < count; i++)
            {
                if (i % 1_000 == 0)
                    baseFrametime = 6.9 + random.NextDouble() * 26.4;

                double sample = baseFrametime * (0.85 + random.NextDouble() * 0.3);
                int roll = random.Next(1_000);
                if (roll < 20) sample *= 3.2 + random.NextDouble() * 3;
                else if (roll < 22) sample = 250 + random.NextDouble() * 750;
                else if (roll < 25) sample = 0;
                samples[i] = sample;
            }

            return samples;
        }

        private static double[] WithNonFiniteValues(double[] samples)
        {
            samples[17] = double.NaN;
            samples[150] = double.PositiveInfinity;
            samples[151] = double.PositiveInfinity;
            samples[260] = double.NegativeInfinity;
            return samples;
        }

        private sealed class CountingList : IList<double>
        {
            private readonly IList<double> _inner;

            public CountingList(IList<double> inner) => _inner = inner;

            public long Reads { get; private set; }

            public double this[int index]
            {
                get { Reads++; return _inner[index]; }
                set => throw new NotSupportedException();
            }

            public int Count => _inner.Count;
            public bool IsReadOnly => true;
            public void Add(double item) => throw new NotSupportedException();
            public void Clear() => throw new NotSupportedException();
            public bool Contains(double item) => throw new NotSupportedException();
            public void CopyTo(double[] array, int arrayIndex) => throw new NotSupportedException();
            public IEnumerator<double> GetEnumerator() => throw new NotSupportedException();
            public int IndexOf(double item) => throw new NotSupportedException();
            public void Insert(int index, double item) => throw new NotSupportedException();
            public bool Remove(double item) => throw new NotSupportedException();
            public void RemoveAt(int index) => throw new NotSupportedException();
            IEnumerator IEnumerable.GetEnumerator() => throw new NotSupportedException();
        }

        #endregion
    }
}
