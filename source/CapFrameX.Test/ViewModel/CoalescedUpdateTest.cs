using CapFrameX.ViewModel;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;

namespace CapFrameX.Test.ViewModel
{
    /// <summary>
    /// The real time range update of the Comparison page: a dragged range slider changes its values
    /// with every mouse move, and the charts have to follow while the drag goes on.
    /// </summary>
    [TestClass]
    public class CoalescedUpdateTest
    {
        private static readonly TimeSpan Interval = TimeSpan.FromMilliseconds(75);
        private static readonly TimeSpan MouseMove = TimeSpan.FromMilliseconds(16);

        [TestMethod]
        public void ContinuousRequests_UpdateWhileTheyGoOn()
        {
            var scheduler = new HistoricalScheduler();
            int updates = 0;
            var update = new CoalescedUpdate(Interval, scheduler, () => updates++);

            // A one second drag.
            for (int move = 0; move < 60; move++)
            {
                update.Request();
                scheduler.AdvanceBy(MouseMove);
            }

            Assert.IsTrue(updates >= 10, $"only {updates} updates during the drag");
        }

        [TestMethod]
        public void Throttle_DrewNothingDuringTheSameDrag()
        {
            // The pipeline this replaces, for comparison.
            var scheduler = new HistoricalScheduler();
            var requests = new Subject<Unit>();
            int updates = 0;
            requests.Throttle(Interval, scheduler).Subscribe(_ => updates++);

            for (int move = 0; move < 60; move++)
            {
                requests.OnNext(Unit.Default);
                scheduler.AdvanceBy(MouseMove);
            }

            Assert.AreEqual(0, updates);
        }

        [TestMethod]
        public void BurstOfRequests_UpdatesOnce()
        {
            var scheduler = new HistoricalScheduler();
            int updates = 0;
            var update = new CoalescedUpdate(Interval, scheduler, () => updates++);

            for (int request = 0; request < 50; request++)
                update.Request();
            scheduler.AdvanceBy(TimeSpan.FromSeconds(1));

            Assert.AreEqual(1, updates);
        }

        [TestMethod]
        public void LastRequest_IsAlwaysFollowedByAnUpdate()
        {
            var scheduler = new HistoricalScheduler();
            int updates = 0;
            var update = new CoalescedUpdate(Interval, scheduler, () => updates++);

            update.Request();
            scheduler.AdvanceBy(Interval);
            Assert.AreEqual(1, updates);

            // The final position of a drag, after the last update ran.
            update.Request();
            scheduler.AdvanceBy(Interval - TimeSpan.FromMilliseconds(1));
            Assert.AreEqual(1, updates);
            scheduler.AdvanceBy(TimeSpan.FromMilliseconds(1));
            Assert.AreEqual(2, updates);
        }

        [TestMethod]
        public void RequestDuringTheUpdate_SchedulesTheNextOne()
        {
            var scheduler = new HistoricalScheduler();
            int updates = 0;
            CoalescedUpdate update = null;
            update = new CoalescedUpdate(Interval, scheduler, () =>
            {
                // The value changes again while the charts are being drawn.
                if (++updates == 1)
                    update.Request();
            });

            update.Request();
            scheduler.AdvanceBy(TimeSpan.FromSeconds(1));

            Assert.AreEqual(2, updates);
        }

        [TestMethod]
        public void NoRequest_NoUpdate()
        {
            var scheduler = new HistoricalScheduler();
            int updates = 0;
            _ = new CoalescedUpdate(Interval, scheduler, () => updates++);

            scheduler.AdvanceBy(TimeSpan.FromSeconds(10));

            Assert.AreEqual(0, updates);
        }
    }
}
