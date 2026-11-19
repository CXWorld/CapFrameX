using CapFrameX.PresentMonInterface;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CapFrameX.Test.PresentMonInterface
{
    /// <summary>
    /// The runtime HookOverlayManager and PresentMonOverlayMetrics take for the target process:
    /// PresentMon reports one per frame row, and a process may present through two swapchains.
    /// </summary>
    [TestClass]
    public class PresentRuntimeTrackerTest
    {
        private const long Second = PresentRuntimeTracker.BucketMilliseconds;

        [TestMethod]
        public void Observe_TakesTheFirstRuntimeAtOnce()
        {
            var tracker = new PresentRuntimeTracker();

            Assert.IsNull(tracker.Runtime);
            Assert.AreEqual(PresentRuntimeObservation.Changed, tracker.Observe("DXGI", 0));
            Assert.AreEqual("DXGI", tracker.Runtime);
        }

        [TestMethod]
        public void Observe_IgnoresEmptyAndErrorRuntimes()
        {
            var tracker = new PresentRuntimeTracker();

            foreach (string invalid in new[] { null, "", "   ", "<error>" })
                Assert.AreEqual(PresentRuntimeObservation.Unchanged, tracker.Observe(invalid, 0),
                    $"'{invalid}' is no runtime");

            Assert.IsNull(tracker.Runtime);
        }

        [TestMethod]
        public void Observe_KeepsTheRuntimeWhileASecondSwapchainPresentsLessOften()
        {
            // CS2 on 2026-11-17 delivered rows of a DXGI and an Other swapchain interleaved, which
            // flipped the runtime in bursts of up to 26 per second.
            var tracker = new PresentRuntimeTracker();
            tracker.Observe("DXGI", 0);

            for (int frame = 1; frame <= 20; frame++)
            {
                long now = frame * 16;

                Assert.AreEqual(PresentRuntimeObservation.Unchanged, tracker.Observe("DXGI", now),
                    "a DXGI row keeps the runtime");
                Assert.AreEqual(frame == 1
                        ? PresentRuntimeObservation.AnotherRuntimeSeen
                        : PresentRuntimeObservation.Unchanged,
                    tracker.Observe("Other", now),
                    "an interleaved Other row does not rename the runtime");
                Assert.AreEqual("DXGI", tracker.Runtime);
            }
        }

        [TestMethod]
        public void Observe_ReportsAnotherRuntimeOnlyOnce()
        {
            var tracker = new PresentRuntimeTracker();
            tracker.Observe("DXGI", 0);

            Assert.AreEqual(PresentRuntimeObservation.AnotherRuntimeSeen, tracker.Observe("Other", 10));
            Assert.AreEqual(PresentRuntimeObservation.Unchanged, tracker.Observe("Other", 20));
            Assert.AreEqual(PresentRuntimeObservation.Unchanged, tracker.Observe("DXGI", 30));
            Assert.AreEqual(PresentRuntimeObservation.Unchanged, tracker.Observe("Other", 40));
        }

        [TestMethod]
        public void Observe_SwitchesWhenTheOtherRuntimeClearlyDominates()
        {
            // The game really moved to another swapchain: within the window the new runtime has
            // more than twice the rows of the old one.
            var tracker = new PresentRuntimeTracker();
            tracker.Observe("DXGI", 0);
            tracker.Observe("DXGI", 16);

            PresentRuntimeObservation last = PresentRuntimeObservation.Unchanged;
            long now = 32;
            int frames = 0;
            while (last != PresentRuntimeObservation.Changed && frames < 10)
            {
                last = tracker.Observe("Other", now);
                now += 16;
                frames++;
            }

            Assert.AreEqual(PresentRuntimeObservation.Changed, last);
            Assert.AreEqual("Other", tracker.Runtime);
            // Two DXGI rows need more than four Other rows inside the window.
            Assert.AreEqual(5, frames);
        }

        [TestMethod]
        public void Observe_SwitchesOnceTheOldRuntimeLeftTheWindow()
        {
            // The game stopped presenting through DXGI: its rows age out of the two-second window
            // and the first Other row after that is the only runtime left.
            var tracker = new PresentRuntimeTracker();
            for (int frame = 0; frame < 60; frame++)
                tracker.Observe("DXGI", frame * 16);

            long later = 60 * 16 + 2 * Second + 1;

            Assert.AreEqual(PresentRuntimeObservation.Changed, tracker.Observe("Other", later));
            Assert.AreEqual("Other", tracker.Runtime);
        }

        [TestMethod]
        public void Observe_CountsTheCurrentRuntimeBesideTwoOthers()
        {
            // PresentMon knows three runtimes. A second that begins with the two others must
            // still count the current one, or its count is zero and one row takes over.
            var tracker = new PresentRuntimeTracker();
            tracker.Observe("DXGI", 0);

            for (int second = 1; second <= 3; second++)
            {
                long start = second * Second;
                tracker.Observe("Other", start);
                tracker.Observe("D3D9", start + 1);
                for (int frame = 0; frame < 10; frame++)
                    tracker.Observe("DXGI", start + 2 + frame);

                Assert.AreEqual("DXGI", tracker.Runtime, $"second {second}");
            }
        }

        [TestMethod]
        public void Observe_ComparesRuntimesWithoutCase()
        {
            var tracker = new PresentRuntimeTracker();
            tracker.Observe("DXGI", 0);

            Assert.AreEqual(PresentRuntimeObservation.Unchanged, tracker.Observe("dxgi", 16));
            Assert.AreEqual("DXGI", tracker.Runtime);
        }

        [TestMethod]
        public void Reset_ForgetsTheRuntimeAndTheSightings()
        {
            var tracker = new PresentRuntimeTracker();
            tracker.Observe("DXGI", 0);
            tracker.Observe("Other", 16);

            tracker.Reset();

            Assert.IsNull(tracker.Runtime);
            // After a reset every runtime is a first sighting again.
            Assert.AreEqual(PresentRuntimeObservation.Changed, tracker.Observe("Other", 32));
            Assert.AreEqual(PresentRuntimeObservation.AnotherRuntimeSeen, tracker.Observe("DXGI", 48));
        }

        [TestMethod]
        public void Observe_DropsRowsThatArriveAfterTheWindowMovedOn()
        {
            // A row with an older timestamp than the window must not resurrect its runtime.
            var tracker = new PresentRuntimeTracker();
            tracker.Observe("DXGI", 5 * Second);
            tracker.Observe("Other", 5 * Second + 16);
            tracker.Observe("Other", 5 * Second + 32);
            tracker.Observe("Other", 5 * Second + 48);
            Assert.AreEqual("Other", tracker.Runtime);

            Assert.AreEqual(PresentRuntimeObservation.Unchanged, tracker.Observe("DXGI", 1 * Second));
            Assert.AreEqual("Other", tracker.Runtime);
        }
    }
}
