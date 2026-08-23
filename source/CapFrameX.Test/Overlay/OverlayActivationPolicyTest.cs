using CapFrameX.Overlay;
using CapFrameX.ViewModel;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CapFrameX.Test.Overlay
{
    [TestClass]
    public class OverlayActivationPolicyTest
    {
        [DataTestMethod]
        [DataRow(true, false, false, false, false)]
        [DataRow(true, true, false, false, true)]
        [DataRow(true, false, true, false, true)]
        [DataRow(true, false, false, true, true)]
        [DataRow(false, true, false, false, false)]
        public void GetInitialOverlayActiveState_RequiresAvailableRenderer(
            bool configuredActive, bool rtssInstalled, bool hookFreeEnabled,
            bool hookEnabled, bool expected)
        {
            bool actual = OverlayService.GetInitialOverlayActiveState(
                configuredActive, rtssInstalled, hookFreeEnabled, hookEnabled);

            Assert.AreEqual(expected, actual);
        }

        [DataTestMethod]
        [DataRow(false, false, false, false, true)]
        [DataRow(true, false, false, false, false)]
        [DataRow(true, true, false, false, true)]
        [DataRow(true, false, true, false, true)]
        [DataRow(true, false, false, true, true)]
        public void CanSetOverlayActive_AllowsDeactivationWithoutRenderer(
            bool requestedActive, bool rtssInstalled, bool hookFreeEnabled,
            bool hookEnabled, bool expected)
        {
            bool actual = OverlayViewModel.CanSetOverlayActive(
                requestedActive, rtssInstalled, hookFreeEnabled, hookEnabled);

            Assert.AreEqual(expected, actual);
        }
    }
}
