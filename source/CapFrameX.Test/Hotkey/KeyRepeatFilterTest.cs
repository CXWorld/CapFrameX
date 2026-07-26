using System.Windows.Forms;
using CapFrameX.Hotkey;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CapFrameX.Test.Hotkey
{
    [TestClass]
    public class KeyRepeatFilterTest
    {
        [TestMethod]
        public void ShouldHandleKeyDown_FirstPressIsHandled()
        {
            var filter = new KeyRepeatFilter();

            Assert.IsTrue(filter.ShouldHandleKeyDown(Keys.F12, 1000));
        }

        /// <summary>
        /// The regression this filter exists for: a global hook sees every auto-repeat, so holding
        /// a toggle hotkey used to flip it an even number of times and look like a hotkey that
        /// never fired.
        /// </summary>
        [TestMethod]
        public void ShouldHandleKeyDown_AutoRepeatIsSuppressed()
        {
            var filter = new KeyRepeatFilter();

            Assert.IsTrue(filter.ShouldHandleKeyDown(Keys.O, 1000));

            // Windows repeats between roughly 33 ms and 500 ms apart, depending on the configured
            // repeat rate. None of it may reach the action.
            Assert.IsFalse(filter.ShouldHandleKeyDown(Keys.O, 1033));
            Assert.IsFalse(filter.ShouldHandleKeyDown(Keys.O, 1533));
            Assert.IsFalse(filter.ShouldHandleKeyDown(Keys.O, 2033));
        }

        [TestMethod]
        public void ShouldHandleKeyDown_ReleaseAllowsAnImmediateSecondPress()
        {
            var filter = new KeyRepeatFilter();

            Assert.IsTrue(filter.ShouldHandleKeyDown(Keys.F12, 1000));
            filter.OnKeyUp(Keys.F12);

            // Deliberate double presses stay possible: only the key-up-free repeat stream is
            // suppressed, not a genuine second press within the expiry window.
            Assert.IsTrue(filter.ShouldHandleKeyDown(Keys.F12, 1050));
        }

        [TestMethod]
        public void ShouldHandleKeyDown_RecoversWhenTheKeyUpNeverArrived()
        {
            var filter = new KeyRepeatFilter();

            Assert.IsTrue(filter.ShouldHandleKeyDown(Keys.F12, 1000));

            // A key-up can be missed (released while the hook was being re-armed). The expiry is
            // the fallback that keeps a lost key-up from disabling the hotkey for good.
            Assert.IsTrue(filter.ShouldHandleKeyDown(Keys.F12, 1000 + KeyRepeatFilter.HeldExpiryMs));
        }

        [TestMethod]
        public void ShouldHandleKeyDown_KeysAreTrackedIndependently()
        {
            var filter = new KeyRepeatFilter();

            Assert.IsTrue(filter.ShouldHandleKeyDown(Keys.F12, 1000));
            Assert.IsTrue(filter.ShouldHandleKeyDown(Keys.O, 1010));
            Assert.IsFalse(filter.ShouldHandleKeyDown(Keys.F12, 1020));
            Assert.IsFalse(filter.ShouldHandleKeyDown(Keys.O, 1030));
        }
    }
}
