using System;
using System.Collections.Generic;
using System.Reactive.Concurrency;
using System.Reactive.Subjects;
using CapFrameX.Overlay;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using EntryFeedMode = CapFrameX.Overlay.OverlayService.EntryFeedMode;

namespace CapFrameX.Test.Overlay
{
    [TestClass]
    public class RemoteOverlayDemandTest
    {
        private static readonly TimeSpan Lease = TimeSpan.FromSeconds(30);

        [TestMethod]
        public void RegisterRequest_HoldsDemandForLease()
        {
            var scheduler = new HistoricalScheduler(DateTimeOffset.UtcNow);
            var demand = new RemoteOverlayDemand(scheduler, Lease);
            var states = new List<bool>();
            using var subscription = demand.IsActiveStream.Subscribe(states.Add);

            demand.RegisterRequest();
            scheduler.AdvanceBy(Lease - TimeSpan.FromSeconds(1));
            Assert.IsTrue(demand.IsActive);

            scheduler.AdvanceBy(TimeSpan.FromSeconds(1));
            Assert.IsFalse(demand.IsActive);
            CollectionAssert.AreEqual(new[] { false, true, false }, states);
        }

        [TestMethod]
        public void RegisterRequest_WithinLease_ExtendsIt()
        {
            var scheduler = new HistoricalScheduler(DateTimeOffset.UtcNow);
            var demand = new RemoteOverlayDemand(scheduler, Lease);
            var states = new List<bool>();
            using var subscription = demand.IsActiveStream.Subscribe(states.Add);

            demand.RegisterRequest();
            scheduler.AdvanceBy(TimeSpan.FromSeconds(20));
            demand.RegisterRequest();
            scheduler.AdvanceBy(TimeSpan.FromSeconds(20));
            Assert.IsTrue(demand.IsActive, "The second read must move the end of the lease.");

            scheduler.AdvanceBy(TimeSpan.FromSeconds(10));
            Assert.IsFalse(demand.IsActive);
            CollectionAssert.AreEqual(new[] { false, true, false }, states);
        }

        [TestMethod]
        public void RegisterRequest_AfterExpiry_StartsNewLease()
        {
            var scheduler = new HistoricalScheduler(DateTimeOffset.UtcNow);
            var demand = new RemoteOverlayDemand(scheduler, Lease);

            demand.RegisterRequest();
            scheduler.AdvanceBy(Lease);
            demand.RegisterRequest();
            Assert.IsTrue(demand.IsActive);

            scheduler.AdvanceBy(Lease);
            Assert.IsFalse(demand.IsActive);
        }

        [TestMethod]
        public void StreamingClient_HoldsDemandUntilReleased()
        {
            var scheduler = new HistoricalScheduler(DateTimeOffset.UtcNow);
            var demand = new RemoteOverlayDemand(scheduler, Lease);

            var first = demand.AcquireStreamingClient();
            var second = demand.AcquireStreamingClient();
            scheduler.AdvanceBy(TimeSpan.FromHours(1));
            Assert.IsTrue(demand.IsActive);

            first.Dispose();
            first.Dispose();
            Assert.IsTrue(demand.IsActive, "A repeated release must not drop another client's demand.");

            second.Dispose();
            Assert.IsFalse(demand.IsActive);
        }

        [TestMethod]
        public void StreamingClient_ReleasedDuringLease_KeepsLease()
        {
            var scheduler = new HistoricalScheduler(DateTimeOffset.UtcNow);
            var demand = new RemoteOverlayDemand(scheduler, Lease);

            var client = demand.AcquireStreamingClient();
            demand.RegisterRequest();
            client.Dispose();
            Assert.IsTrue(demand.IsActive);

            scheduler.AdvanceBy(Lease);
            Assert.IsFalse(demand.IsActive);
        }

        [TestMethod]
        public void FeedModes_RemoteDemandFeedsOnlyWhileOverlayIsOff()
        {
            using var overlay = new BehaviorSubject<bool>(false);
            using var demand = new BehaviorSubject<bool>(false);
            var modes = new List<EntryFeedMode>();
            using var subscription = OverlayService.SelectEntryFeedModes(overlay, demand).Subscribe(modes.Add);

            demand.OnNext(true);
            overlay.OnNext(true);
            overlay.OnNext(false);
            demand.OnNext(false);

            CollectionAssert.AreEqual(new[]
            {
                EntryFeedMode.Off,
                EntryFeedMode.RemoteOnly,
                EntryFeedMode.Overlay,
                EntryFeedMode.RemoteOnly,
                EntryFeedMode.Off
            }, modes);
        }

        [TestMethod]
        public void FeedModes_RepeatedOverlayValuesStillReDrive()
        {
            using var overlay = new BehaviorSubject<bool>(true);
            using var demand = new BehaviorSubject<bool>(false);
            var modes = new List<EntryFeedMode>();
            using var subscription = OverlayService.SelectEntryFeedModes(overlay, demand).Subscribe(modes.Add);

            // A renderer switch back to RTSS pushes true again to re-run the RTSS initialization.
            overlay.OnNext(true);

            CollectionAssert.AreEqual(new[] { EntryFeedMode.Overlay, EntryFeedMode.Overlay }, modes);
        }

        [TestMethod]
        public void FeedModes_DemandChangeWhileOverlayIsOn_DoesNotReDrive()
        {
            using var overlay = new BehaviorSubject<bool>(true);
            using var demand = new BehaviorSubject<bool>(false);
            var modes = new List<EntryFeedMode>();
            using var subscription = OverlayService.SelectEntryFeedModes(overlay, demand).Subscribe(modes.Add);

            demand.OnNext(true);
            demand.OnNext(false);

            CollectionAssert.AreEqual(new[] { EntryFeedMode.Overlay }, modes);
        }

        [TestMethod]
        public void FeedModes_WaitForOverlayState()
        {
            using var overlay = new Subject<bool>();
            using var demand = new BehaviorSubject<bool>(true);
            var modes = new List<EntryFeedMode>();
            using var subscription = OverlayService.SelectEntryFeedModes(overlay, demand).Subscribe(modes.Add);

            Assert.AreEqual(0, modes.Count);

            overlay.OnNext(false);
            CollectionAssert.AreEqual(new[] { EntryFeedMode.RemoteOnly }, modes);
        }
    }
}
