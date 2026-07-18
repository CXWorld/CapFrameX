using CapFrameX.Statistics.NetStandard;
using CapFrameX.ViewModel.DataContext;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace CapFrameX.Test.ViewModel
{
    [TestClass]
    public class FpsGraphDataContextTest
    {
        [TestMethod]
        public void GetAlignedFinitePoints_MismatchedAndInvalidSeries_UsesTimestampIntersection()
        {
            var fpsPoints = new List<Point>
            {
                new Point(0, 60),
                new Point(1, 61),
                new Point(2, 62),
                new Point(3, 63)
            };
            var gpuPoints = new List<Point>
            {
                new Point(0, 100),
                new Point(2, double.PositiveInfinity),
                new Point(3, 103)
            };
            var method = typeof(FpsGraphDataContext).GetMethod("GetAlignedFinitePoints",
                BindingFlags.NonPublic | BindingFlags.Static);

            var result = (IList<Tuple<Point, Point>>)method.Invoke(null,
                new object[] { fpsPoints, gpuPoints });

            Assert.AreEqual(2, result.Count);
            Assert.AreEqual(0d, result[0].Item1.X);
            Assert.AreEqual(100d, result[0].Item2.Y);
            Assert.AreEqual(3d, result[1].Item1.X);
            Assert.AreEqual(103d, result[1].Item2.Y);
        }
    }
}
