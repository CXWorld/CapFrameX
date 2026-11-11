using CapFrameX.Contracts.Configuration;
using CapFrameX.Data;
using CapFrameX.Data.Session.Classes;
using CapFrameX.Data.Session.Contracts;
using CapFrameX.Statistics.NetStandard;
using CapFrameX.Statistics.NetStandard.Contracts;
using CapFrameX.Test.Mocks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CapFrameX.Test.Data
{
    /// <summary>
    /// The analysis views ask the record data server for the same time window several times per
    /// refresh. It builds each window once per session, time window and outlier method.
    /// </summary>
    [TestClass]
    public class LocalRecordDataServerTest
    {
        private Mock<IAppConfiguration> _configuration;
        private LocalRecordDataServer _server;

        [TestInitialize]
        public void Initialize()
        {
            _configuration = new Mock<IAppConfiguration>();
            _configuration.SetupAllProperties();
            _server = new LocalRecordDataServer(_configuration.Object);
        }

        [TestMethod]
        public void RepeatedRequests_BuildTheWindowOnce()
        {
            var (session, data, reference) = CreateSession(seed: 1);
            Select(session, 0, 5);

            var first = _server.GetFrametimeTimeWindow();
            var second = _server.GetFrametimeTimeWindow();
            var third = _server.GetFrametimeTimeWindow();

            Assert.AreEqual(1, data.FrametimeReads);
            var expected = reference.GetFrametimeTimeWindow(0, 5, _configuration.Object);
            CollectionAssert.AreEqual(expected.ToArray(), first.ToArray());
            CollectionAssert.AreEqual(expected.ToArray(), second.ToArray());
            CollectionAssert.AreEqual(expected.ToArray(), third.ToArray());
        }

        [TestMethod]
        public void EveryWindow_MatchesTheSessionExtension()
        {
            var (session, _, reference) = CreateSession(seed: 2);
            Select(session, 1, 4);
            var options = _configuration.Object;

            CollectionAssert.AreEqual(reference.GetDisplayChangeTimeWindow(1, 5, options).ToArray(),
                _server.GetDisplayChangeTimeWindow().ToArray());
            CollectionAssert.AreEqual(reference.GetGpuActiveTimeTimeWindow(1, 5, options).ToArray(),
                _server.GetGpuActiveTimeTimeWindow().ToArray());
            CollectionAssert.AreEqual(reference.GetAnimationErrorTimeWindow(1, 5).ToArray(),
                _server.GetAnimationErrorTimeWindow().ToArray());
            AssertPoints(reference.GetFrametimePointsTimeWindow(1, 5, options), _server.GetFrametimePointTimeWindow());
            AssertPoints(reference.GetGpuActiveTimePointsTimeWindow(1, 5, options), _server.GetGpuActiveTimePointTimeWindow());
            AssertPoints(reference.GetCpuActiveTimePointsTimeWindow(1, 5, options), _server.GetCpuActiveTimePointTimeWindow());
            AssertPoints(reference.GetFrametimeDistributionPoints(1, 5, options), _server.GetDistributionPointTimeWindow());
            Assert.AreEqual(reference.GetGpuActiveDeviationPercentage(1, 5, options), _server.GetGpuActiveDeviationPercentage());
        }

        [TestMethod]
        public void EachCaller_GetsItsOwnCopy()
        {
            var (session, _, _) = CreateSession(seed: 3);
            Select(session, 0, 5);
            var expected = _server.GetFrametimeTimeWindow().ToArray();

            var window = _server.GetFrametimeTimeWindow();
            window[0] = -1;
            window.RemoveAt(window.Count - 1);
            var points = _server.GetFrametimePointTimeWindow();
            points[0].Y = -1;

            CollectionAssert.AreEqual(expected, _server.GetFrametimeTimeWindow().ToArray());
            Assert.AreNotEqual(-1, _server.GetFrametimePointTimeWindow()[0].Y);
            Assert.AreNotSame(points[1], _server.GetFrametimePointTimeWindow()[1]);
        }

        [TestMethod]
        public void AnotherTimeWindow_IsBuiltAndTheFirstStaysKept()
        {
            var (session, data, reference) = CreateSession(seed: 4);
            Select(session, 0, 5);
            var first = _server.GetFrametimeTimeWindow();

            _server.SetTimeWindow(2, 3);
            var second = _server.GetFrametimeTimeWindow();
            Assert.AreEqual(2, data.FrametimeReads);
            CollectionAssert.AreEqual(reference.GetFrametimeTimeWindow(2, 5, _configuration.Object).ToArray(), second.ToArray());

            _server.SetTimeWindow(0, 5);
            CollectionAssert.AreEqual(first.ToArray(), _server.GetFrametimeTimeWindow().ToArray());
            Assert.AreEqual(2, data.FrametimeReads);
        }

        [TestMethod]
        public void AnotherOutlierMethod_IsBuiltSeparately()
        {
            var (session, data, reference) = CreateSession(seed: 5);
            Select(session, 0, 5);
            _server.GetFrametimeTimeWindow();

            _server.RemoveOutlierMethod = ERemoveOutlierMethod.DeciPercentile;
            var adjusted = _server.GetFrametimeTimeWindow();

            Assert.AreEqual(2, data.FrametimeReads);
            CollectionAssert.AreEqual(reference.GetFrametimeTimeWindow(0, 5, _configuration.Object,
                ERemoveOutlierMethod.DeciPercentile).ToArray(), adjusted.ToArray());
        }

        [TestMethod]
        public void NewSession_DropsTheWindowsOfThePreviousOne()
        {
            var (first, firstData, _) = CreateSession(seed: 6);
            var (second, secondData, secondReference) = CreateSession(seed: 7);
            Select(first, 0, 5);
            _server.GetFrametimeTimeWindow();

            _server.CurrentSession = second;
            var window = _server.GetFrametimeTimeWindow();
            _server.CurrentSession = first;
            _server.GetFrametimeTimeWindow();

            Assert.AreEqual(1, secondData.FrametimeReads);
            CollectionAssert.AreEqual(secondReference.GetFrametimeTimeWindow(0, 5, _configuration.Object).ToArray(), window.ToArray());
            Assert.AreEqual(2, firstData.FrametimeReads, "Switching back must not reuse a dropped window.");
        }

        [TestMethod]
        public void SameSessionSetAgain_KeepsItsWindows()
        {
            var (session, data, _) = CreateSession(seed: 8);
            Select(session, 0, 5);
            _server.GetFrametimeTimeWindow();

            _server.CurrentSession = session;
            _server.GetFrametimeTimeWindow();

            Assert.AreEqual(1, data.FrametimeReads);
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void FpsWindow_ReusesTheTimingWindow(bool useDisplayChangeMetrics)
        {
            var (session, data, _) = CreateSession(seed: 9);
            _configuration.Object.UseDisplayChangeMetrics = useDisplayChangeMetrics;
            Select(session, 0, 5);
            var timings = useDisplayChangeMetrics ? _server.GetDisplayChangeTimeWindow() : _server.GetFrametimeTimeWindow();

            var fps = _server.GetFpsTimeWindow();

            CollectionAssert.AreEqual(timings.Select(timing => 1000 / timing).ToArray(), fps.ToArray());
            Assert.AreEqual(useDisplayChangeMetrics ? 0 : 1, data.FrametimeReads);
            Assert.AreEqual(useDisplayChangeMetrics ? 1 : 0, data.DisplayChangeReads);
        }

        [TestMethod]
        public void GpuActiveDeviation_IgnoresTheOutlierMethodAndReusesItsWindows()
        {
            var (session, data, reference) = CreateSession(seed: 10);
            Select(session, 0, 5);
            _server.RemoveOutlierMethod = ERemoveOutlierMethod.DeciPercentile;

            double first = _server.GetGpuActiveDeviationPercentage();
            double second = _server.GetGpuActiveDeviationPercentage();

            Assert.AreEqual(reference.GetGpuActiveDeviationPercentage(0, 5, _configuration.Object,
                ERemoveOutlierMethod.DeciPercentile), first);
            Assert.AreEqual(first, second);
            Assert.AreEqual(1, data.FrametimeReads);
        }

        [TestMethod]
        public void Cache_KeepsOnlyTheMostRecentWindows()
        {
            var (session, data, _) = CreateSession(seed: 11);
            Select(session, 0, 5);
            for (int i = 0; i <= LocalRecordDataServer.MaxCachedWindows; i++)
            {
                _server.SetTimeWindow(i * 0.1, 1);
                _server.GetFrametimeTimeWindow();
            }
            int reads = data.FrametimeReads;

            _server.SetTimeWindow(LocalRecordDataServer.MaxCachedWindows * 0.1, 1);
            _server.GetFrametimeTimeWindow();
            Assert.AreEqual(reads, data.FrametimeReads, "The latest window is kept.");

            _server.SetTimeWindow(0, 1);
            _server.GetFrametimeTimeWindow();
            Assert.AreEqual(reads + 1, data.FrametimeReads, "The oldest window was dropped.");
        }

        [TestMethod]
        public async Task ConcurrentRequests_AllGetTheSameWindow()
        {
            var (session, _, reference) = CreateSession(seed: 12);
            Select(session, 0, 5);
            var expected = reference.GetFrametimeTimeWindow(0, 5, _configuration.Object).ToArray();

            var windows = await Task.WhenAll(Enumerable.Range(0, 32)
                .Select(_ => Task.Run(() => _server.GetFrametimeTimeWindow())));

            foreach (var window in windows)
                CollectionAssert.AreEqual(expected, window.ToArray());
        }

        [TestMethod]
        public void WithoutSession_NothingIsBuilt()
        {
            Assert.IsNull(_server.GetFrametimeTimeWindow());
            Assert.IsNull(_server.GetFrametimePointTimeWindow());
            Assert.IsNull(_server.GetFpsTimeWindow());
            Assert.AreEqual(0d, _server.GetGpuActiveDeviationPercentage());
        }

        private void Select(ISession session, double currentTime, double windowLength)
        {
            _server.CurrentSession = session;
            _server.SetTimeWindow(currentTime, windowLength);
        }

        // The session the server reads, its counting capture data, and a session over the same
        // data that the tests compute their expected windows from without being counted.
        private static (ISession Session, CountingCaptureData Data, ISession Reference) CreateSession(int seed)
        {
            var random = new Random(seed);
            int count = 400;
            double[] frametimes = Enumerable.Range(0, count)
                .Select(_ => random.Next(10) == 0 ? 80 + random.NextDouble() * 40 : 10 + random.NextDouble() * 10)
                .ToArray();
            double time = 0;
            var captureData = new SessionCaptureData(count)
            {
                MsBetweenPresents = frametimes,
                MsBetweenDisplayChange = frametimes.Select(frametime => frametime + 0.5).ToArray(),
                GpuActive = frametimes.Select(frametime => frametime * 0.8).ToArray(),
                CpuActive = frametimes.Select(frametime => frametime * 0.6).ToArray(),
                AnimationError = frametimes.Select(frametime => frametime - 15).ToArray(),
                TimeInSeconds = frametimes.Select(frametime => (time += frametime) / 1000).ToArray(),
            };
            var data = new CountingCaptureData(captureData);
            var session = new Session();
            session.Runs.Add(new SessionRun { CaptureData = data });
            var reference = new Session();
            reference.Runs.Add(new SessionRun { CaptureData = captureData });
            return (session, data, reference);
        }

        private static void AssertPoints(IList<Point> expected, IList<Point> actual)
        {
            Assert.AreEqual(expected.Count, actual.Count);
            for (int i = 0; i < expected.Count; i++)
            {
                Assert.AreEqual(expected[i].X, actual[i].X);
                Assert.AreEqual(expected[i].Y, actual[i].Y);
            }
        }
    }
}
