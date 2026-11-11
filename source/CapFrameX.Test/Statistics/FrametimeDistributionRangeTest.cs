using CapFrameX.Data.Session.Classes;
using CapFrameX.Statistics.NetStandard;
using CapFrameX.Statistics.NetStandard.Contracts;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System.Collections.Generic;
using System.Linq;

namespace CapFrameX.Test.Statistics
{
    [TestClass]
    public class FrametimeDistributionRangeTest
    {
        [TestMethod]
        public void GetReadableMaximum_KeepsTheLastPointWithoutExtremeFrames()
        {
            var points = GetDistribution(Frames(9990, 10d).Concat(Frames(10, 30d)));

            Assert.AreEqual(30.1, FrametimeDistributionRange.GetReadableMaximum(points), 1e-9);
        }

        [TestMethod]
        public void GetReadableMaximum_KeepsATailWithinFiveTimesTheBody()
        {
            // The 49.9 ms frame ends up in the bin up to 50.0 ms, just within five times the
            // 10.1 ms bin that ends the body.
            var points = GetDistribution(Frames(10000, 10d).Concat(Frames(1, 49.9d)));

            Assert.AreEqual(50.0, FrametimeDistributionRange.GetReadableMaximum(points), 1e-9);
        }

        [TestMethod]
        public void GetReadableMaximum_LeavesALongStallOutside()
        {
            // The 22.7 s stall is almost a fifth of this capture's time, but a single frame.
            var points = GetDistribution(Frames(10000, 10d).Concat(Frames(5, 12d)).Concat(Frames(1, 22728.0369d)));

            Assert.AreEqual(12.1, FrametimeDistributionRange.GetReadableMaximum(points), 1e-9);
            Assert.AreEqual(22728.1, points.Last().X, 1e-9);
        }

        [TestMethod]
        public void GetReadableMaximum_ShowsEverythingForShortCaptures()
        {
            // With fewer than 1000 frames the slowest 0.1% are the slowest frame itself.
            var points = GetDistribution(Frames(500, 10d).Concat(Frames(1, 400d)));

            Assert.AreEqual(400.1, FrametimeDistributionRange.GetReadableMaximum(points), 1e-9);
        }

        [TestMethod]
        public void GetReadableMaximum_IsNaNWithoutPoints()
        {
            Assert.IsTrue(double.IsNaN(FrametimeDistributionRange.GetReadableMaximum(new List<Point>())));
            Assert.IsTrue(double.IsNaN(FrametimeDistributionRange.GetReadableMaximum(null)));
        }

        private static IEnumerable<double> Frames(int count, double frametime)
            => Enumerable.Repeat(frametime, count);

        private static IList<Point> GetDistribution(IEnumerable<double> frametimes)
        {
            var values = frametimes.ToArray();
            var captureData = new SessionCaptureData(values.Length)
            {
                TimeInSeconds = values.Select((_, index) => index * 0.01).ToArray(),
                MsBetweenPresents = values
            };
            var session = new Session();
            session.Runs.Add(new SessionRun { CaptureData = captureData });

            var options = new Mock<IFrametimeStatisticProviderOptions>();
            options.Setup(option => option.FpsValuesRoundingDigits).Returns(2);
            options.Setup(option => option.IntervalAverageWindowTime).Returns(500);

            return session.GetFrametimeDistributionPoints(0, double.MaxValue, options.Object);
        }
    }
}
