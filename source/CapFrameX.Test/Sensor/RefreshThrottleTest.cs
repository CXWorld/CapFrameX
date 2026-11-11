using System;
using System.Collections.Generic;
using System.Linq;
using LibreHardwareMonitor.Hardware;
using LibreHardwareMonitor.Hardware.Storage;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CapFrameX.Test.Sensor
{
    [TestClass]
    public class RefreshThrottleTest
    {
        // Deterministic timer jitter of the sensor poll, in milliseconds.
        private static readonly int[] PollJitter = { -12, 9, -3, 15, -15, 4, 0, -7, 11, -1 };

        [TestMethod]
        public void FirstCallReadsAtOnce()
        {
            var throttle = new RefreshThrottle(TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(1), () => 123456);

            Assert.IsTrue(throttle.TryBegin());
            Assert.IsFalse(throttle.TryBegin());
        }

        [TestMethod]
        public void ReadsOncePerIntervalDespitePollJitter()
        {
            long now = 0;
            var throttle = new RefreshThrottle(TimeSpan.FromSeconds(2), clockMilliseconds: () => now);

            var reads = new List<long>();
            for (int poll = 0; poll < 80; poll++)
            {
                now = 250L * poll + (poll == 0 ? 0 : PollJitter[poll % PollJitter.Length]);
                if (throttle.TryBegin())
                    reads.Add(now);
            }

            // One read per 2 s grid point, each in the first poll at or after it: jitter neither
            // stretches the interval nor accumulates.
            Assert.AreEqual(10, reads.Count);
            for (int i = 0; i < reads.Count; i++)
            {
                Assert.IsTrue(reads[i] >= 2000L * i, $"read {i} at {reads[i]} ms came early");
                Assert.IsTrue(reads[i] <= 2000L * i + 250 + 15, $"read {i} at {reads[i]} ms came late");
            }
        }

        [TestMethod]
        public void PhaseKeepsDevicesOutOfEachOthersPolls()
        {
            long now = 0;
            var first = new RefreshThrottle(TimeSpan.FromSeconds(2), TimeSpan.Zero, () => now);
            var second = new RefreshThrottle(TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(1), () => now);

            var firstReads = new List<long>();
            var secondReads = new List<long>();
            for (now = 0; now <= 10000; now += 250)
            {
                if (first.TryBegin())
                    firstReads.Add(now);
                if (second.TryBegin())
                    secondReads.Add(now);
            }

            CollectionAssert.AreEqual(new long[] { 0, 2000, 4000, 6000, 8000, 10000 }, firstReads);
            CollectionAssert.AreEqual(new long[] { 0, 3000, 5000, 7000, 9000 }, secondReads);
            CollectionAssert.AreEqual(new long[] { 0 }, firstReads.Intersect(secondReads).ToArray());
        }

        [TestMethod]
        public void GapRestartsTheGridFromNow()
        {
            long now = 0;
            var throttle = new RefreshThrottle(TimeSpan.FromSeconds(2), clockMilliseconds: () => now);

            Assert.IsTrue(throttle.TryBegin());

            // Nobody polled for a while: read at once, then continue 2 s from there.
            now = 7300;
            Assert.IsTrue(throttle.TryBegin());

            now = 9000;
            Assert.IsFalse(throttle.TryBegin());

            now = 9300;
            Assert.IsTrue(throttle.TryBegin());
        }

        [TestMethod]
        public void ZeroIntervalReadsOnEveryCall()
        {
            var throttle = new RefreshThrottle(TimeSpan.Zero, clockMilliseconds: () => 0);

            for (int i = 0; i < 5; i++)
                Assert.IsTrue(throttle.TryBegin());
        }

        [TestMethod]
        public void IntervalChangeAppliesFromTheNextCall()
        {
            long now = 0;
            var throttle = new RefreshThrottle(TimeSpan.FromSeconds(10), clockMilliseconds: () => now);

            Assert.IsTrue(throttle.TryBegin());

            now = 250;
            Assert.IsFalse(throttle.TryBegin());

            throttle.Interval = TimeSpan.Zero;
            Assert.IsTrue(throttle.TryBegin());
        }

        [TestMethod]
        public void AtaSmartReadsAreThrottledToTenSecondsByDefault()
        {
            Assert.AreEqual(TimeSpan.FromSeconds(10), AtaStorage.ThrottleInterval);
        }
    }
}
