using System;
using System.Collections.Generic;
using System.Reactive.Subjects;
using CapFrameX.OSD.Integration;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CapFrameX.Test.Integration
{
    [TestClass]
    public class OverlayPreviewFrameFeedTest
    {
        [TestMethod]
        public void TargetChangesResetHistoryAndRejectForeignAndLateRows()
        {
            using var frames = new Subject<string[]>();
            using var targets = new BehaviorSubject<int>(42);
            var output = new List<OverlayPreviewFrame>();
            using var subscription = OverlayPreviewFrameFeed.Observe(frames, targets, 0, 2, 3, () => 1)
                .Subscribe(output.Add);
            frames.OnNext(new[] { "5", "100", "9", "9" });
            frames.OnNext(new[] { "42", "100", "10", "11" });
            frames.OnNext(new[] { "42", "99", "12", "12" });
            targets.OnNext(7);
            frames.OnNext(new[] { "42", "110", "10", "11" });
            frames.OnNext(new[] { "7", "5", "8", "0" });

            Assert.AreEqual(4, output.Count);
            Assert.IsTrue(output[0].IsReset);
            Assert.AreEqual(100d, output[1].TimeMs);
            Assert.AreEqual(11d, output[1].DisplayTimeMs);
            Assert.IsTrue(output[2].IsReset);
            Assert.AreEqual(5d, output[3].TimeMs);
        }

        [TestMethod]
        public void InvalidNumbersAreDroppedAndDisplayOnlySamplesAreRetained()
        {
            using var frames = new Subject<string[]>();
            using var targets = new BehaviorSubject<int>(42);
            var output = new List<OverlayPreviewFrame>();
            using var subscription = OverlayPreviewFrameFeed.Observe(frames, targets, 0, 2, 3, () => 1)
                .Subscribe(output.Add);
            frames.OnNext(null);
            frames.OnNext(new[] { "42" });
            frames.OnNext(new[] { "42", "Infinity", "10", "10" });
            frames.OnNext(new[] { "42", "10", "NaN", "Infinity" });
            frames.OnNext(new[] { "42", "10", "10000", "-1" });
            frames.OnNext(new[] { "42", "10", "0", "8.5" });

            Assert.AreEqual(2, output.Count);
            Assert.AreEqual(0d, output[1].FrametimeMs);
            Assert.AreEqual(8.5, output[1].DisplayTimeMs);
        }

        [TestMethod]
        public void UnsubscribeStopsFrameAndTargetObservers()
        {
            using var frames = new Subject<string[]>();
            using var targets = new Subject<int>();
            var subscription = OverlayPreviewFrameFeed.Observe(frames, targets, 0, 2, 3, () => 1)
                .Subscribe(_ => { });
            Assert.IsTrue(frames.HasObservers);
            Assert.IsTrue(targets.HasObservers);
            subscription.Dispose();
            Assert.IsFalse(frames.HasObservers);
            Assert.IsFalse(targets.HasObservers);
        }
    }
}
