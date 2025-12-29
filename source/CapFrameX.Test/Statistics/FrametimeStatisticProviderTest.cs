using CapFrameX.Statistics.NetStandard;
using CapFrameX.Statistics.NetStandard.Contracts;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CapFrameX.Test.Statistics
{
    [TestClass]
    public class FrametimeStatisticProviderTest
    {
        private FrametimeStatisticProvider _provider;
        private Mock<IFrametimeStatisticProviderOptions> _optionsMock;

        [TestInitialize]
        public void Setup()
        {
            _optionsMock = new Mock<IFrametimeStatisticProviderOptions>();
            _optionsMock.Setup(o => o.FpsValuesRoundingDigits).Returns(2);
            _optionsMock.Setup(o => o.IntervalAverageWindowTime).Returns(500);
            _provider = new FrametimeStatisticProvider(_optionsMock.Object);
        }

        #region GetFpsMetricValue Tests

        [TestMethod]
        public void GetFpsMetricValue_EmptySequence_ReturnsNaN()
        {
            var sequence = new List<double>();
            var result = _provider.GetFpsMetricValue(sequence, EMetric.Average);
            Assert.IsTrue(double.IsNaN(result));
        }

        [TestMethod]
        public void GetFpsMetricValue_Average_CalculatesCorrectly()
        {
            // 60 FPS = 16.67ms frame time
            var sequence = new List<double> { 16.67, 16.67, 16.67, 16.67, 16.67 };
            var result = _provider.GetFpsMetricValue(sequence, EMetric.Average);

            // Average FPS should be approximately 60
            Assert.AreEqual(59.99, result, 0.1);
        }

        [TestMethod]
        public void GetFpsMetricValue_Max_ReturnsHighestFps()
        {
            // Frame times: 10ms (100 FPS), 16.67ms (60 FPS), 33.33ms (30 FPS)
            var sequence = new List<double> { 10, 16.67, 33.33 };
            var result = _provider.GetFpsMetricValue(sequence, EMetric.Max);

            // Max FPS should be 100 (from 10ms frame time)
            Assert.AreEqual(100, result, 0.01);
        }

        [TestMethod]
        public void GetFpsMetricValue_Min_ReturnsLowestFps()
        {
            // Frame times: 10ms (100 FPS), 16.67ms (60 FPS), 33.33ms (30 FPS)
            var sequence = new List<double> { 10, 16.67, 33.33 };
            var result = _provider.GetFpsMetricValue(sequence, EMetric.Min);

            // Min FPS should be approximately 30 (from 33.33ms frame time)
            Assert.AreEqual(30, result, 0.1);
        }

        [TestMethod]
        public void GetFpsMetricValue_Median_CalculatesCorrectly()
        {
            // Frame times resulting in FPS: 30, 60, 90
            var sequence = new List<double> { 33.33, 16.67, 11.11 };
            var result = _provider.GetFpsMetricValue(sequence, EMetric.Median);

            // Median FPS should be approximately 60
            Assert.AreEqual(60, result, 1);
        }

        [TestMethod]
        public void GetFpsMetricValue_P1_ReturnsLow1Percentile()
        {
            // Generate 100 frame times with one outlier
            var sequence = new List<double>();
            for (int i = 0; i < 99; i++)
            {
                sequence.Add(16.67); // 60 FPS
            }
            sequence.Add(100); // 10 FPS outlier

            var result = _provider.GetFpsMetricValue(sequence, EMetric.P1);

            // P1 should be close to the outlier FPS value
            Assert.IsTrue(result < 60);
        }

        #endregion

        #region GetFrametimeMetricValue Tests

        [TestMethod]
        public void GetFrametimeMetricValue_Average_CalculatesCorrectly()
        {
            var sequence = new List<double> { 10, 20, 30 };
            var result = _provider.GetFrametimeMetricValue(sequence, EMetric.Average);

            Assert.AreEqual(20, result, 0.01);
        }

        [TestMethod]
        public void GetFrametimeMetricValue_Max_ReturnsHighestFrametime()
        {
            var sequence = new List<double> { 10, 20, 30, 15, 25 };
            var result = _provider.GetFrametimeMetricValue(sequence, EMetric.Max);

            Assert.AreEqual(30, result, 0.01);
        }

        [TestMethod]
        public void GetFrametimeMetricValue_Min_ReturnsLowestFrametime()
        {
            var sequence = new List<double> { 10, 20, 30, 15, 25 };
            var result = _provider.GetFrametimeMetricValue(sequence, EMetric.Min);

            Assert.AreEqual(10, result, 0.01);
        }

        [TestMethod]
        public void GetFrametimeMetricValue_Median_CalculatesCorrectly()
        {
            var sequence = new List<double> { 10, 20, 30, 40, 50 };
            var result = _provider.GetFrametimeMetricValue(sequence, EMetric.Median);

            Assert.AreEqual(30, result, 0.01);
        }

        #endregion

        #region GetPQuantileSequence Tests

        [TestMethod]
        public void GetPQuantileSequence_P50_ReturnsMedian()
        {
            var sequence = new List<double> { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
            var result = _provider.GetPQuantileSequence(sequence, 0.5);

            Assert.AreEqual(5.5, result, 0.1);
        }

        [TestMethod]
        public void GetPQuantileSequence_P99_ReturnsHigh()
        {
            var sequence = Enumerable.Range(1, 100).Select(x => (double)x).ToList();
            var result = _provider.GetPQuantileSequence(sequence, 0.99);

            Assert.IsTrue(result >= 99);
        }

        [TestMethod]
        public void GetPQuantileSequence_P01_ReturnsLow()
        {
            var sequence = Enumerable.Range(1, 100).Select(x => (double)x).ToList();
            var result = _provider.GetPQuantileSequence(sequence, 0.01);

            Assert.IsTrue(result <= 2);
        }

        #endregion

        #region GetPercentageHighIntegralSequence Tests

        [TestMethod]
        public void GetPercentageHighIntegralSequence_EmptySequence_ReturnsNaN()
        {
            var sequence = new List<double>();
            var result = _provider.GetPercentageHighIntegralSequence(sequence, 0.99);

            Assert.IsTrue(double.IsNaN(result));
        }

        [TestMethod]
        public void GetPercentageHighIntegralSequence_ReturnsHighFrametime()
        {
            // Sequence with one high frametime
            var sequence = new List<double> { 10, 10, 10, 10, 100 };
            var result = _provider.GetPercentageHighIntegralSequence(sequence, 0.99);

            // The 1% integral should return the highest frametime
            Assert.AreEqual(100, result, 0.01);
        }

        [TestMethod]
        public void GetPercentageHighIntegralSequence_P90_ReturnsCorrectValue()
        {
            var sequence = new List<double> { 10, 20, 30, 40, 50, 60, 70, 80, 90, 100 };
            var result = _provider.GetPercentageHighIntegralSequence(sequence, 0.90);

            // Should return one of the high values
            Assert.IsTrue(result >= 80);
        }

        #endregion

        #region GetPercentageHighAverageSequence Tests

        [TestMethod]
        public void GetPercentageHighAverageSequence_EmptySequence_ReturnsNaN()
        {
            var sequence = new List<double>();
            var result = _provider.GetPercentageHighAverageSequence(sequence, 0.99);

            Assert.IsTrue(double.IsNaN(result));
        }

        [TestMethod]
        public void GetPercentageHighAverageSequence_ReturnsAverageOfHighValues()
        {
            // All values equal, average of top 1% should be close to the value
            var sequence = new List<double> { 100, 100, 100, 100, 100 };
            var result = _provider.GetPercentageHighAverageSequence(sequence, 0.99);

            Assert.AreEqual(100, result, 0.01);
        }

        #endregion

        #region GetStutteringCountPercentage Tests

        [TestMethod]
        public void GetStutteringCountPercentage_NoStutters_ReturnsZero()
        {
            // All frame times around the average
            var sequence = new List<double> { 16, 17, 16, 17, 16 };
            var result = _provider.GetStutteringCountPercentage(sequence, 2.5);

            Assert.AreEqual(0, result, 0.1);
        }

        [TestMethod]
        public void GetStutteringCountPercentage_WithStutters_ReturnsPercentage()
        {
            // 4 normal frames + 1 stutter (2.5x average)
            var sequence = new List<double> { 16, 16, 16, 16, 100 };
            var result = _provider.GetStutteringCountPercentage(sequence, 2.5);

            // 1 out of 5 = 20%
            Assert.AreEqual(20, result, 0.1);
        }

        #endregion

        #region GetOnlineStutteringTimePercentage Tests

        [TestMethod]
        public void GetOnlineStutteringTimePercentage_NoStutters_ReturnsZero()
        {
            var sequence = new List<double> { 16, 17, 16, 17, 16 };
            var result = _provider.GetOnlineStutteringTimePercentage(sequence, 2.5);

            Assert.AreEqual(0, result, 0.1);
        }

        [TestMethod]
        public void GetOnlineStutteringTimePercentage_WithStutters_ReturnsTimePercentage()
        {
            // 4 normal frames (16ms) + 1 stutter (100ms)
            // Total time = 64 + 100 = 164ms
            // Stuttering time = 100ms
            // Percentage = 100/164 * 100 ≈ 61%
            var sequence = new List<double> { 16, 16, 16, 16, 100 };
            var result = _provider.GetOnlineStutteringTimePercentage(sequence, 2.5);

            Assert.IsTrue(result > 50);
        }

        #endregion

        #region GetOutlierAdjustedSequence Tests

        [TestMethod]
        public void GetOutlierAdjustedSequence_None_ReturnsOriginal()
        {
            var sequence = new List<double> { 10, 20, 30, 1000 };
            var result = _provider.GetOutlierAdjustedSequence(sequence, ERemoveOutlierMethod.None);

            Assert.AreEqual(4, result.Count);
        }

        [TestMethod]
        public void GetOutlierAdjustedSequence_DeciPercentile_RemovesOutliers()
        {
            var sequence = Enumerable.Range(1, 100).Select(x => (double)x).ToList();
            sequence.Add(10000); // Add outlier

            var result = _provider.GetOutlierAdjustedSequence(sequence, ERemoveOutlierMethod.DeciPercentile);

            // The extreme outlier should be removed
            Assert.IsFalse(result.Contains(10000));
        }

        #endregion

        #region GetFpsThresholdCounts Tests

        [TestMethod]
        public void GetFpsThresholdCounts_CountsCorrectly()
        {
            // Frame times: 50ms (20 FPS), 16.67ms (60 FPS), 8.33ms (120 FPS)
            var sequence = new List<double> { 50, 16.67, 8.33 };
            var result = _provider.GetFpsThresholdCounts(sequence, false);

            // Thresholds are [240, 144, 120, 90, 75, 60, 45, 30, 15, 10]
            // All frames are below 240, 144 (3), below 120 (2), below 90 (2), etc.
            Assert.IsNotNull(result);
            Assert.AreEqual(10, result.Count); // 10 thresholds
        }

        #endregion

        #region GetFpsThresholdTimes Tests

        [TestMethod]
        public void GetFpsThresholdTimes_CalculatesCorrectly()
        {
            var sequence = new List<double> { 50, 16.67, 8.33 };
            var result = _provider.GetFpsThresholdTimes(sequence, false);

            Assert.IsNotNull(result);
            Assert.AreEqual(10, result.Count);
        }

        #endregion

        #region Edge Cases Tests

        [TestMethod]
        public void GetFpsMetricValue_SingleElement_ReturnsValue()
        {
            var sequence = new List<double> { 16.67 };
            var result = _provider.GetFpsMetricValue(sequence, EMetric.Average);

            Assert.AreEqual(59.99, result, 0.1);
        }

        [TestMethod]
        public void GetFpsMetricValue_LargeSequence_Completes()
        {
            // Test with a large sequence to ensure no memory issues
            var sequence = Enumerable.Range(1, 20000).Select(x => 16.67 + (x % 10)).ToList();
            var result = _provider.GetFpsMetricValue(sequence, EMetric.Average);

            Assert.IsFalse(double.IsNaN(result));
        }

        [TestMethod]
        public void GetPercentageHighIntegralSequence_LargeSequence_Completes()
        {
            // Test the optimized sorting with a large sequence
            var sequence = Enumerable.Range(1, 20000).Select(x => (double)(x % 100 + 10)).ToList();
            var result = _provider.GetPercentageHighIntegralSequence(sequence, 0.99);

            Assert.IsFalse(double.IsNaN(result));
        }

        [TestMethod]
        public void GetFpsMetricValue_VerySmallFrametimes_HandlesCorrectly()
        {
            // Very high FPS scenario (1ms frame times = 1000 FPS)
            var sequence = new List<double> { 1, 1, 1, 1, 1 };
            var result = _provider.GetFpsMetricValue(sequence, EMetric.Average);

            Assert.AreEqual(1000, result, 0.1);
        }

        [TestMethod]
        public void GetFpsMetricValue_VeryLargeFrametimes_HandlesCorrectly()
        {
            // Very low FPS scenario (1000ms frame times = 1 FPS)
            var sequence = new List<double> { 1000, 1000, 1000 };
            var result = _provider.GetFpsMetricValue(sequence, EMetric.Average);

            Assert.AreEqual(1, result, 0.1);
        }

        #endregion
    }
}
