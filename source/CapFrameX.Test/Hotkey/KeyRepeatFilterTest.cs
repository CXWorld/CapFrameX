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

            // Windows repeats between roughly 33 ms and 400 ms apart, depending on the configured
            // repeat rate. None of it may reach the action.
            Assert.IsFalse(filter.ShouldHandleKeyDown(Keys.O, 1033));
            Assert.IsFalse(filter.ShouldHandleKeyDown(Keys.O, 1533));
            Assert.IsFalse(filter.ShouldHandleKeyDown(Keys.O, 2033));
        }

        /// <summary>
        /// Before the first repeat Windows waits the keyboard delay: about 250, 500, 750 or
        /// 1000 ms (settings 0 to 3). An expiry of 700 ms let the first repeat of settings 2 and 3
        /// through as a second press, so holding a toggle hotkey past the delay flipped it back.
        /// </summary>
        [TestMethod]
        [DataRow(0)]
        [DataRow(1)]
        [DataRow(2)]
        [DataRow(3)]
        public void ShouldHandleKeyDown_FirstRepeatAfterEveryKeyboardDelayIsSuppressed(int keyboardDelaySetting)
        {
            var filter = new KeyRepeatFilter();
            long keyboardDelayMs = (keyboardDelaySetting + 1) * 250;

            Assert.IsTrue(filter.ShouldHandleKeyDown(Keys.O, 1000));
            Assert.IsFalse(filter.ShouldHandleKeyDown(Keys.O, 1000 + keyboardDelayMs));
            // The repeats after the first come at the repeat rate, at most 400 ms apart.
            Assert.IsFalse(filter.ShouldHandleKeyDown(Keys.O, 1000 + keyboardDelayMs + 400));
        }

        /// <summary>
        /// The filter stamps a key-down when the hook thread handles it, so a short stall between
        /// a press and its first repeat stretches the gap it sees beyond the keyboard delay.
        /// </summary>
        [TestMethod]
        public void ShouldHandleKeyDown_FirstRepeatAfterTheLongestDelayHandledLateIsSuppressed()
        {
            var filter = new KeyRepeatFilter();

            Assert.IsTrue(filter.ShouldHandleKeyDown(Keys.O, 1000));
            // Keyboard delay 3 (about 1 s), and the hook thread got to the repeat 300 ms late.
            Assert.IsFalse(filter.ShouldHandleKeyDown(Keys.O, 1000 + 1000 + 300));
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
