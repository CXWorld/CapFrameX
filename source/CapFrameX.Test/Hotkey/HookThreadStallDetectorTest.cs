using CapFrameX.Hotkey;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CapFrameX.Test.Hotkey
{
    [TestClass]
    public class HookThreadStallDetectorTest
    {
        [TestMethod]
        public void Heartbeat_FirstBeatIsNeverAStall()
        {
            var detector = new HookThreadStallDetector();

            Assert.AreEqual(0, detector.Heartbeat(1000));
        }

        [TestMethod]
        public void Heartbeat_RegularCadenceIsNotAStall()
        {
            var detector = new HookThreadStallDetector();
            detector.Heartbeat(1000);

            // A 50 ms timer lands anywhere between its interval and a few scheduler quanta late.
            Assert.AreEqual(0, detector.Heartbeat(1050));
            Assert.AreEqual(0, detector.Heartbeat(1116));
            Assert.AreEqual(0, detector.Heartbeat(1180));
        }

        /// <summary>
        /// The regression this exists for: a hook thread that was starved past
        /// LowLevelHooksTimeout has lost its hook, and the periodic re-arm alone left the
        /// hotkeys dead for up to a minute.
        /// </summary>
        [TestMethod]
        public void Heartbeat_GapPastTheThresholdReportsItsLength()
        {
            var detector = new HookThreadStallDetector();
            detector.Heartbeat(1000);
            detector.Heartbeat(1050);

            Assert.AreEqual(400, detector.Heartbeat(1450));
        }

        [TestMethod]
        public void Heartbeat_SustainedStarvationRearmsOncePerEpisode()
        {
            var detector = new HookThreadStallDetector();
            detector.Heartbeat(1000);

            Assert.AreEqual(300, detector.Heartbeat(1300));
            // Every later heartbeat that is itself late must not re-arm again right away.
            Assert.AreEqual(0, detector.Heartbeat(1600));
            Assert.AreEqual(0, detector.Heartbeat(2900));
            // Once the rate limit has elapsed a new stall counts as a new episode.
            // 4300 ms: the rate limit (2000 ms since the re-arm at 1300) has elapsed, and the gap
            // to the previous heartbeat at 2900 is what gets reported.
            Assert.AreEqual(1400, detector.Heartbeat(1300 + HookThreadStallDetector.MinimumRearmIntervalMs + 1000));
        }

        [TestMethod]
        public void NotifyRearmed_ThePeriodicRearmCountsAgainstTheRateLimit()
        {
            var detector = new HookThreadStallDetector();
            detector.Heartbeat(1000);
            detector.NotifyRearmed(1050);

            // The hook was just replaced; a stall observed immediately after needs no second one.
            Assert.AreEqual(0, detector.Heartbeat(1400));
        }
    }

    [TestClass]
    public class HotkeyLatencyTest
    {
        [TestMethod]
        public void ElapsedMs_PlainDifference()
        {
            Assert.AreEqual(47, HotkeyLatency.ElapsedMs(1000, 1047));
        }

        [TestMethod]
        public void ElapsedMs_SurvivesTheTickCountWrap()
        {
            // GetTickCount wraps after 49.7 days; a keypress stamped just before the wrap and
            // dispatched just after must still read as a few milliseconds.
            Assert.AreEqual(30, HotkeyLatency.ElapsedMs(int.MaxValue - 10, int.MinValue + 19));
        }

        [TestMethod]
        public void ElapsedMs_GranularityJitterReadsAsZero()
        {
            Assert.AreEqual(0, HotkeyLatency.ElapsedMs(1016, 1000));
        }
    }
}
