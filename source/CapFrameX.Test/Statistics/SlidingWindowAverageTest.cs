using CapFrameX.Statistics.NetStandard;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CapFrameX.Test.Statistics
{
    [TestClass]
    public class SlidingWindowAverageTest
    {
        [TestMethod]
        public void Average_CoversTheSamplesOfTheLastWindow()
        {
            var average = new SlidingWindowAverage(1000);
            average.Add(0, 10);
            average.Add(250, 20);
            average.Add(500, 30);

            Assert.AreEqual(20, average.Average(500).Value, 1e-12);
            // At 1000 ms the first sample is exactly one window old and has left the window.
            Assert.AreEqual(25, average.Average(1000).Value, 1e-12);
            Assert.AreEqual(30, average.Average(1400).Value, 1e-12);
            Assert.IsNull(average.Average(1500), "A window without samples has no average.");
        }

        [TestMethod]
        public void Add_SkipsMissingReadings()
        {
            var average = new SlidingWindowAverage(1000);
            average.Add(0, 40);
            average.Add(100, double.NaN);
            average.Add(200, double.PositiveInfinity);
            average.Add(300, 60);

            Assert.AreEqual(2, average.Count);
            Assert.AreEqual(50, average.Average(300).Value, 1e-12);
        }

        [TestMethod]
        public void Add_StartsANewWindowWhenTheClockStepsBack()
        {
            var average = new SlidingWindowAverage(1000);
            average.Add(5000, 90);
            average.Add(100, 10);

            Assert.AreEqual(1, average.Count);
            Assert.AreEqual(10, average.Average(100).Value, 1e-12);
        }

        [TestMethod]
        [DataRow(0.0)]
        [DataRow(-1.0)]
        [DataRow(double.NaN)]
        [DataRow(double.PositiveInfinity)]
        public void Constructor_RejectsAnUnusableWindow(double window)
        {
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new SlidingWindowAverage(window));
        }

        [TestMethod]
        public void Average_MatchesTheMeanOfEveryWindowOverALongSession()
        {
            // Four sensor polls per second with jitter over ten minutes, averaged over 60 s.
            var random = new Random(466);
            var samples = new List<(double Time, double Value)>();
            double time = 0;
            for (int i = 0; i < 2400; i++)
            {
                time += 200 + random.NextDouble() * 100;
                samples.Add((time, random.NextDouble() * 100));
            }

            var average = new SlidingWindowAverage(60000);
            for (int i = 0; i < samples.Count; i++)
            {
                average.Add(samples[i].Time, samples[i].Value);
                double now = samples[i].Time;
                var window = samples.Take(i + 1).Where(sample => now - sample.Time < 60000).ToArray();
                Assert.AreEqual(window.Average(sample => sample.Value), average.Average(now).Value, 1e-9, "Sample " + i);
                Assert.AreEqual(window.Length, average.Count, "Sample " + i);
            }
        }
    }
}
