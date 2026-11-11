using System;
using System.Collections.Generic;
using System.Linq;
using CapFrameX.Contracts.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CapFrameX.Test.Configuration
{
    [TestClass]
    public class OsdPixelOrbitTest
    {
        [TestMethod]
        [DataRow(1)]
        [DataRow(6)]
        [DataRow(7)]
        public void OneCycleVisitsEverySquarePositionOnceInOnePixelSteps(int radius)
        {
            const int interval = 180;
            int positions = (radius + 1) * (radius + 1);
            var walk = Enumerable.Range(0, positions)
                .Select(step => OsdPixelOrbit.GetOffset(interval, radius, step * interval * 1000L)).ToArray();

            Assert.AreEqual(positions, walk.Distinct().Count(), "Every offset in the square must be used once per cycle.");
            Assert.IsTrue(walk.All(offset => offset.X >= 0 && offset.X <= radius && offset.Y >= 0 && offset.Y <= radius),
                "Offsets never cross the configured margin and never exceed the radius.");
            for (int i = 1; i < walk.Length; i++)
                Assert.AreEqual(1, Math.Abs(walk[i].X - walk[i - 1].X) + Math.Abs(walk[i].Y - walk[i - 1].Y),
                    "Consecutive steps must be one pixel apart.");
            Assert.AreEqual((0, 0), walk[0]);
            Assert.AreEqual((0, 0), OsdPixelOrbit.GetOffset(interval, radius, positions * interval * 1000L),
                "The walk starts over after one full cycle.");
        }

        [TestMethod]
        public void OffsetChangesOnlyAtIntervalBoundaries()
        {
            Assert.AreEqual((0, 0), OsdPixelOrbit.GetOffset(60, 6, 0));
            Assert.AreEqual((0, 0), OsdPixelOrbit.GetOffset(60, 6, 59_999));
            Assert.AreEqual((1, 0), OsdPixelOrbit.GetOffset(60, 6, 60_000));
            // The second row runs back from the right edge.
            Assert.AreEqual((6, 1), OsdPixelOrbit.GetOffset(60, 6, 7 * 60_000L));
            Assert.AreEqual((5, 1), OsdPixelOrbit.GetOffset(60, 6, 8 * 60_000L));
        }

        [TestMethod]
        public void ParametersAreClampedToTheSupportedRange()
        {
            Assert.AreEqual(OsdPixelOrbit.MinimumIntervalSeconds, OsdPixelOrbit.ClampInterval(0));
            Assert.AreEqual(OsdPixelOrbit.MaximumIntervalSeconds, OsdPixelOrbit.ClampInterval(int.MaxValue));
            Assert.AreEqual(OsdPixelOrbit.MinimumRadius, OsdPixelOrbit.ClampRadius(-4));
            Assert.AreEqual(OsdPixelOrbit.MaximumRadius, OsdPixelOrbit.ClampRadius(1000));
            // An invalid interval must not divide by zero; a negative clock reads as zero.
            Assert.AreEqual((0, 0), OsdPixelOrbit.GetOffset(0, 0, -5));
            var largest = new HashSet<(int, int)>(Enumerable.Range(0, 2000)
                .Select(step => OsdPixelOrbit.GetOffset(10, 1000, step * 10_000L)));
            Assert.IsTrue(largest.All(offset => offset.Item1 <= OsdPixelOrbit.MaximumRadius && offset.Item2 <= OsdPixelOrbit.MaximumRadius));
        }
    }
}
