using CapFrameX.Contracts.Localization;
using CapFrameX.Overlay;
using CapFrameX.ViewModel;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CapFrameX.Test.Overlay
{
    /// <summary>
    /// The hook-free overlay is a topmost window: without an MPO plane for the game on its display,
    /// DWM composes the game while the window is visible. The Overlay tab warns about it.
    /// </summary>
    [TestClass]
    public class MpoSupportTest
    {
        private const string Display = @"\\.\DISPLAY1";

        [TestMethod]
        public void TheReportedRadeonCapsHaveNoPlaneForAGame()
        {
            // cfx_osd.log of the user report (RX 9070 XT): "MaxPlanes=1 MaxRGBPlanes=2".
            var mpo = new MpoSupportInfo(Display, true, 1, 2, 2, null);

            Assert.AreEqual(MpoSupportStatus.NoOverlayPlane, mpo.Status);
            Assert.AreEqual(0, mpo.RgbOverlayPlanes);
            Assert.IsTrue(mpo.WarnsHookFree);
        }

        [TestMethod]
        public void AnRgbPlaneBesideTheBasePlaneIsSupported()
        {
            var mpo = new MpoSupportInfo(Display, true, 4, 2, 2, null);

            Assert.AreEqual(MpoSupportStatus.Supported, mpo.Status);
            Assert.AreEqual(1, mpo.RgbOverlayPlanes);
            Assert.IsFalse(mpo.WarnsHookFree);
        }

        [TestMethod]
        public void AVideoOnlyOverlayPlaneDoesNotServeAGame()
        {
            var mpo = new MpoSupportInfo(Display, true, 2, 1, 2, null);

            Assert.AreEqual(MpoSupportStatus.NoOverlayPlane, mpo.Status);
            Assert.IsTrue(mpo.WarnsHookFree);
        }

        [TestMethod]
        public void OverlayTestModeFiveSwitchesMpoOffWhateverTheDriverReports()
        {
            Assert.AreEqual(MpoSupportStatus.SwitchedOff, new MpoSupportInfo(Display, true, 4, 4, 2, 5).Status);
            Assert.AreEqual(MpoSupportStatus.SwitchedOff, new MpoSupportInfo(Display, false, 0, 0, 0, 5).Status);
            Assert.AreEqual(MpoSupportStatus.Supported, new MpoSupportInfo(Display, true, 4, 4, 2, 0).Status,
                "only 5 switches MPO off");
        }

        [TestMethod]
        public void UnreportedCapsDoNotWarn()
        {
            var mpo = new MpoSupportInfo(Display, false, 0, 0, 0, null, "D3DKMTGetMultiPlaneOverlayCaps failed (0xC00000BB)");

            Assert.AreEqual(MpoSupportStatus.Unknown, mpo.Status);
            Assert.IsFalse(mpo.WarnsHookFree);
            Assert.AreEqual(string.Empty, OverlayViewModel.GetHookFreeMpoWarning(mpo));
            Assert.AreEqual(string.Empty, OverlayViewModel.GetHookFreeMpoWarning(null));
        }

        [TestMethod]
        public void TheLogLineNamesDisplayPlanesAndTestMode()
        {
            Assert.AreEqual(@"\\.\DISPLAY1: NoOverlayPlane, planes 1 (RGB 2, YUV 2), OverlayTestMode not set",
                new MpoSupportInfo(Display, true, 1, 2, 2, null).ToString());
            Assert.AreEqual(@"\\.\DISPLAY1: SwitchedOff, planes unknown (no answer), OverlayTestMode 5",
                new MpoSupportInfo(Display, false, 0, 0, 0, 5).ToString());
        }

        [TestMethod]
        public void TheWarningNamesTheCauseAndTheConsequence()
        {
            string consequence = CxLang.T("OverlayView_HookFreeMpoConsequence");

            Assert.AreEqual(CxLang.T("OverlayView_HookFreeMpoMissing") + " " + consequence,
                OverlayViewModel.GetHookFreeMpoWarning(new MpoSupportInfo(Display, true, 1, 2, 2, null)));
            Assert.AreEqual(CxLang.T("OverlayView_HookFreeMpoSwitchedOff") + " " + consequence,
                OverlayViewModel.GetHookFreeMpoWarning(new MpoSupportInfo(Display, true, 4, 4, 2, 5)));
            Assert.AreEqual(string.Empty,
                OverlayViewModel.GetHookFreeMpoWarning(new MpoSupportInfo(Display, true, 4, 2, 2, null)));
        }

        [TestMethod]
        public void AnUnknownDisplayIsReportedNotThrown()
        {
            var mpo = MpoSupportProbe.Query(@"\\.\DISPLAY99");

            Assert.IsFalse(mpo.CapsKnown);
            StringAssert.Contains(mpo.Error, "D3DKMTOpenAdapterFromGdiDisplayName");
        }
    }
}
