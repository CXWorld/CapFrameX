using CapFrameX.Data.Session.Classes;
using CapFrameX.Statistics.NetStandard;
using CapFrameX.Statistics.NetStandard.Contracts;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;
using System.Linq;
using System.Threading.Tasks;

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
        public void GetFrametimeDistributionPoints_BinsValuesByTheirLowerEdge()
        {
            var session = CreateSession(
                new[] { 0d, 1d, 2d, 3d },
                new[] { 0.3d, 5d, 5.09d, 5.1d });

            var distribution = session.GetFrametimeDistributionPoints(0, 3, _options);

            CollectionAssert.AreEqual(new[] { 0.4d, 5.1d, 5.2d },
                distribution.Select(point => point.X).ToArray());
            double total = 0.3 + 5 + 5.09 + 5.1;
            Assert.AreEqual(0.3 / total * 100, distribution[0].Y, 0.000001);
            Assert.AreEqual((5 + 5.09) / total * 100, distribution[1].Y, 0.000001);
            Assert.AreEqual(5.1 / total * 100, distribution[2].Y, 0.000001);
        }

        [TestMethod]
        public void GetFrametimeDistributionPoints_HandlesLongStallInLargeCapture()
        {
            // Shape of a Division 2 capture that froze the distribution chart:
            // 227k frames with a single 22.7 s stall.
            const int frameCount = 227000;
            var times = Enumerable.Range(0, frameCount).Select(i => i * 0.01).ToArray();
            var frametimes = Enumerable.Repeat(10d, frameCount).ToArray();
            frametimes[frameCount / 2] = 22728.0369;
            var session = CreateSession(times, frametimes);

            var computation = Task.Run(() =>
                session.GetFrametimeDistributionPoints(0, times[^1], _options));

            Assert.IsTrue(computation.Wait(TimeSpan.FromSeconds(30)),
                "The distribution must not scale with the largest frametime.");

            var distribution = computation.Result;
            CollectionAssert.AreEqual(new[] { 10.1d, 22728.1d },
                distribution.Select(point => point.X).ToArray());
            double total = (frameCount - 1) * 10d + 22728.0369;
            Assert.AreEqual((frameCount - 1) * 10d / total * 100, distribution[0].Y, 0.000001);
            Assert.AreEqual(22728.0369 / total * 100, distribution[1].Y, 0.000001);
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

        [TestMethod]
        public void GetDisplayLayer_IsNullWithoutLayerData()
        {
            var session = CreateSession(new[] { 0d, 1d }, new[] { 10d, 10d });

            Assert.IsNull(session.Runs.GetDisplayLayer());

            session.Runs[0].CaptureData.LayerIndex = new[] { -1, -1 };
            Assert.IsNull(session.Runs.GetDisplayLayer());
        }

        [TestMethod]
        public void GetDisplayLayer_NamesTheOnlyLayer()
        {
            var session = CreateSession(new[] { 0d, 1d, 2d }, new[] { 10d, 10d, 10d });
            session.Runs[0].CaptureData.LayerIndex = new[] { 1, -1, 1 };

            Assert.AreEqual("Layer 1", session.Runs.GetDisplayLayer());
        }

        [TestMethod]
        public void GetDisplayLayer_ListsSeveralLayersByShare()
        {
            var session = CreateSession(new[] { 0d, 1d, 2d, 3d, 4d }, new[] { 10d, 10d, 10d, 10d, 10d });
            session.Runs[0].CaptureData.LayerIndex = new[] { 0, 1, 1, 1, -1 };

            Assert.AreEqual("Layer 1 (75%), Layer 0 (25%)", session.Runs.GetDisplayLayer());
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
