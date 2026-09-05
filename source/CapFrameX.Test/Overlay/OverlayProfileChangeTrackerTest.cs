using CapFrameX.Overlay;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CapFrameX.Test.Overlay
{
    [TestClass]
    public class OverlayProfileChangeTrackerTest
    {
        [TestMethod]
        public void MarkAndReset_TracksPendingProfileChanges()
        {
            var tracker = new OverlayProfileChangeTracker();

            Assert.IsFalse(tracker.HasPendingChanges);

            tracker.MarkPendingChanges();
            Assert.IsTrue(tracker.HasPendingChanges);

            tracker.ResetPendingChanges();
            Assert.IsFalse(tracker.HasPendingChanges);
        }
    }
}
