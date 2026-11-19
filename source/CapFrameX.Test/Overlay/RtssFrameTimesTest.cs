using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Diagnostics;
using System.Linq;

namespace CapFrameX.Test.Overlay
{
    /// <summary>
    /// The RTSS frame times a capture collects with CaptureRTSSFrameTimes: the frames of the last
    /// interval from the frame time ring of the target's RTSS application entry.
    /// </summary>
    [TestClass]
    public class RtssFrameTimesTest
    {
        // RTSS_SHARED_MEMORY_APP_ENTRY::dwStatFrameTimeBuf.
        private const int RingSize = 1024;

        [TestMethod]
        public void AProcessRtssDoesNotTrackHasNoFrameTimes()
        {
            // CapFrameX 1.8.6.2 on 2026-11-17: the empty result threw IndexOutOfRangeException on
            // the capture heartbeat's thread, which ended the app at every capture start.
            var wrapper = CreateWrapper();

            float[] frameTimes = wrapper.GetFrameTimesInterval(UnusedProcessId(), 1000);

            Assert.AreEqual(0, frameTimes.Length);
        }

        [TestMethod]
        public void TheNewestFramesOfTheIntervalComeOldestFirst()
        {
            // Slots 0 to 9 hold 1 to 10 ms; RTSS writes slot 10 next.
            var ring = new uint[RingSize];
            for (int i = 0; i < 10; i++)
                ring[i] = (uint)(1000 * (i + 1));

            // 10 + 9 ms stay below 25 ms, 8 ms more would not.
            CollectionAssert.AreEqual(new[] { 9f, 10f },
                RTSSCSharpWrapper.CollectRingFrameTimes(ring, 10, 25));
        }

        [TestMethod]
        public void AboveAThousandFpsEveryFrameCountsOnce()
        {
            // 1024 frames of 0.5 ms cover 512 ms, less than the interval. The former walk went
            // around the ring again and took every frame twice.
            var ring = Enumerable.Repeat(500u, RingSize).ToArray();

            Assert.AreEqual(RingSize, RTSSCSharpWrapper.CollectRingFrameTimes(ring, 300, 1000).Length);
        }

        [TestMethod]
        public void SlotsRtssHasNotWrittenEndTheWalk()
        {
            // RTSS has written two frames of this application so far. The former walk never
            // ended on the zeros behind them.
            var ring = new uint[RingSize];
            ring[0] = 2000;
            ring[1] = 3000;

            CollectionAssert.AreEqual(new[] { 2f, 3f },
                RTSSCSharpWrapper.CollectRingFrameTimes(ring, 2, 12));
        }

        // Never disposed: Dispose releases the RTSS OSD slot owned by "CapFrameX", which would
        // clear the overlay of a CapFrameX instance running on this machine.
        private static RTSSCSharpWrapper CreateWrapper()
            => new RTSSCSharpWrapper(exception => throw new AssertFailedException(exception.Message));

        private static int UnusedProcessId()
        {
            var running = Process.GetProcesses().Select(process => process.Id).ToHashSet();
            int pid = 0x3FFFFFFC;
            while (running.Contains(pid))
                pid -= 4;
            return pid;
        }
    }
}
