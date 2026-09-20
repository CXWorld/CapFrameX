using CapFrameX.OSD.Integration;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CapFrameX.Test.Integration
{
    [TestClass]
    public class HookOverlayManagerPollingTest
    {
        [TestMethod]
        public void CreateProcessStartTraceQuery_UsesValidEventProjection()
        {
            var query = HookOverlayManager.CreateProcessStartTraceQuery();

            Assert.AreEqual("Win32_ProcessStartTrace", query.EventClassName);
            Assert.AreEqual(HookOverlayManager.ProcessStartTraceQuery.ToUpperInvariant(),
                query.QueryString.ToUpperInvariant());
        }

        [TestMethod]
        public void ShouldContinueFastEarlyInjectionProbe_StopsAtIdleLimit()
        {
            int limit = HookOverlayManager.EarlyInjectionIdleTicksBeforeStandDown;

            Assert.IsTrue(HookOverlayManager.ShouldContinueFastEarlyInjectionProbe(
                sawCandidate: false, idleTicks: limit - 1));
            Assert.IsFalse(HookOverlayManager.ShouldContinueFastEarlyInjectionProbe(
                sawCandidate: false, idleTicks: limit));
            Assert.IsTrue(HookOverlayManager.ShouldContinueFastEarlyInjectionProbe(
                sawCandidate: true, idleTicks: limit));
        }
    }
}
