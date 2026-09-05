using CapFrameX.Data.Session.Classes;
using CapFrameX.Statistics.NetStandard;
using CapFrameX.Statistics.NetStandard.Contracts;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;
using System.Linq;

namespace CapFrameX.Test.Statistics
{
    [TestClass]
    public class SessionExtensionsTest
    {
        private IFrametimeStatisticProviderOptions _options;

        [TestInitialize]
        public void Setup()
        {
            var optionsMock = new Mock<IFrametimeStatisticProviderOptions>();
            optionsMock.Setup(options => options.FpsValuesRoundingDigits).Returns(2);
            optionsMock.Setup(options => options.IntervalAverageWindowTime).Returns(500);
            _options = optionsMock.Object;
        }

        [TestMethod]
        public void GetFrametimePointsTimeWindow_OutlierRemovalPreservesTimestamps()
        {
            var session = CreateSession(
                new[] { 0d, 1d, 2d, 3d, 4d },
                new[] { 10d, 10000d, 20d, 30d, 40d });

            var points = session.GetFrametimePointsTimeWindow(0, 4, _options,
                ERemoveOutlierMethod.DeciPercentile);

            CollectionAssert.AreEqual(new[] { 0d, 2d, 3d, 4d },
                points.Select(point => point.X).ToArray());
            CollectionAssert.AreEqual(new[] { 10d, 20d, 30d, 40d },
                points.Select(point => point.Y).ToArray());
        }

        [TestMethod]
        public void GetFrametimeTimeWindow_RejectsInvalidTimingSamples()
        {
            var session = CreateSession(
                new[] { 0d, 1d, 2d, 3d, 4d, 5d },
                new[] { 10d, 0d, double.NaN, double.PositiveInfinity, -1d, 20d });

            var frametimes = session.GetFrametimeTimeWindow(0, 5, _options);

            CollectionAssert.AreEqual(new[] { 10d, 20d }, frametimes.ToArray());
        }

        [TestMethod]
        public void GetActiveTimeWindows_PreserveZeroAndRejectInvalidSamples()
        {
            var session = CreateSession(
                new[] { 0d, 1d, 2d, 3d },
                new[] { 10d, 10d, 10d, 10d });
            session.Runs[0].CaptureData.GpuActive = new[] { 0d, -1d, double.NaN, 5d };
            session.Runs[0].CaptureData.CpuActive = new[] { 0d, double.PositiveInfinity, -2d, 4d };

            var gpuValues = session.GetGpuActiveTimeTimeWindow(0, 3, _options);
            var cpuPoints = session.GetCpuActiveTimePointsTimeWindow(0, 3, _options);

            CollectionAssert.AreEqual(new[] { 0d, 5d }, gpuValues.ToArray());
            CollectionAssert.AreEqual(new[] { 0d, 4d }, cpuPoints.Select(point => point.Y).ToArray());
        }

        [TestMethod]
        public void GetDisplayTimeDistributionPoints_UsesDisplayChangeSamples()
        {
            var session = CreateSession(
                new[] { 0d, 1d, 2d },
                new[] { 10d, 10d, 10d });
            session.Runs[0].CaptureData.MsBetweenDisplayChange = new[] { 5.01d, 5.09d, 30d };

            var distribution = session.GetDisplayTimeDistributionPoints(0, 2, _options);

            Assert.AreEqual(2, distribution.Count);
            Assert.AreEqual((5.01 + 5.09) / 40.1 * 100, distribution[0].Y, 0.000001);
            Assert.AreEqual(30 / 40.1 * 100, distribution[1].Y, 0.000001);
        }

        [TestMethod]
        public void GetAnimationErrorTimeWindow_RejectsNonFiniteSamples()
        {
            var session = CreateSession(
                new[] { 0d, 1d, 2d },
                new[] { 10d, 10d, 10d });
            session.Runs[0].CaptureData.AnimationError = new[]
                { double.NaN, double.PositiveInfinity, -2d };

            var values = session.GetAnimationErrorTimeWindow(0, 2);

            CollectionAssert.AreEqual(new[] { -2d }, values.ToArray());
        }

        private static Session CreateSession(double[] times, double[] frametimes)
        {
            var captureData = new SessionCaptureData(times.Length)
            {
                TimeInSeconds = times,
                MsBetweenPresents = frametimes,
                MsBetweenDisplayChange = frametimes.ToArray(),
                GpuActive = frametimes.ToArray(),
                CpuActive = frametimes.ToArray()
            };
            var run = new SessionRun { CaptureData = captureData };
            var session = new Session();
            session.Runs.Add(run);
            return session;
        }
    }
}
