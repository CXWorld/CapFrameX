using CapFrameX.OSD.Integration;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CapFrameX.Test.Integration
{
    [TestClass]
    public class OsdOverlayBridgeFrameFilterTest
    {
        [TestMethod]
        public void TargetProcessFrameIsAccepted()
        {
            var row = new[] { "DXGI", "4217", "6.25" };

            Assert.IsTrue(PresentMonFrameFilter.IsForTargetProcess(row, 1, 4217));
        }

        [TestMethod]
        public void ForeignProcessFrameIsRejected()
        {
            var row = new[] { "DXGI", "9001", "6.25" };

            Assert.IsFalse(PresentMonFrameFilter.IsForTargetProcess(row, 1, 4217));
        }

        [TestMethod]
        public void InvalidOrUnselectedProcessFrameIsRejected()
        {
            Assert.IsFalse(PresentMonFrameFilter.IsForTargetProcess(
                new[] { "DXGI", "not-a-pid" }, 1, 4217));
            Assert.IsFalse(PresentMonFrameFilter.IsForTargetProcess(
                new[] { "DXGI", "4217" }, 1, 0));
            Assert.IsFalse(PresentMonFrameFilter.IsForTargetProcess(null, 1, 4217));
        }
    }
}
