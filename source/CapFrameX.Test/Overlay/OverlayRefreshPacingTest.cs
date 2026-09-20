using CapFrameX.Overlay;
using CapFrameX.Sensor;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Reactive.Subjects;
using System.Threading;

namespace CapFrameX.Test.Overlay
{
    [TestClass]
    public class OverlayRefreshPacingTest
    {
        [TestMethod]
        public void RefreshContinuesWithStaleSnapshotWhileSensorPollIsBlocked()
        {
            using var snapshots = new Subject<int>();
            using var refreshTicks = new Subject<long>();
            using var pollStarted = new ManualResetEventSlim(false);
            using var releasePoll = new ManualResetEventSlim(false);
            using var snapshotPublished = new ManualResetEventSlim(false);
            var renderedValues = new List<int>();

            using var refreshSubscription = OverlayService.RefreshFromLatest(
                    snapshots,
                    refreshTicks,
                    initialValue: 17)
                .Subscribe(renderedValues.Add);

            using var poller = new SingleFlightSensorPoller<int>(
                _ =>
                {
                    pollStarted.Set();
                    if (!releasePoll.Wait(TimeSpan.FromSeconds(5)))
                        throw new TimeoutException("The test did not release the simulated sensor poll.");
                    return 42;
                },
                value =>
                {
                    snapshots.OnNext(value);
                    snapshotPublished.Set();
                },
                exception => Assert.Fail(exception.ToString()),
                "Overlay refresh pacing test",
                ThreadPriority.BelowNormal);

            Assert.IsTrue(poller.TryRequest(forcePoll: false));
            Assert.IsTrue(pollStarted.Wait(TimeSpan.FromSeconds(5)));

            refreshTicks.OnNext(0);
            refreshTicks.OnNext(1);
            refreshTicks.OnNext(2);
            CollectionAssert.AreEqual(new[] { 17, 17, 17 }, renderedValues,
                "Overlay ticks must keep rendering the last completed value during hardware I/O.");

            releasePoll.Set();
            Assert.IsTrue(snapshotPublished.Wait(TimeSpan.FromSeconds(5)));
            refreshTicks.OnNext(3);
            CollectionAssert.AreEqual(new[] { 17, 17, 17, 42 }, renderedValues);
        }

        [TestMethod]
        public void RefreshReusesLatestSnapshotUntilANewerOneCompletes()
        {
            using var snapshots = new Subject<int>();
            using var refreshTicks = new Subject<long>();
            var renderedValues = new List<int>();

            using var subscription = OverlayService.RefreshFromLatest(
                    snapshots,
                    refreshTicks,
                    initialValue: -1)
                .Subscribe(renderedValues.Add);

            refreshTicks.OnNext(0);
            snapshots.OnNext(10);
            refreshTicks.OnNext(1);
            refreshTicks.OnNext(2);
            snapshots.OnNext(20);
            refreshTicks.OnNext(3);

            CollectionAssert.AreEqual(new[] { -1, 10, 10, 20 }, renderedValues);
        }
    }
}
