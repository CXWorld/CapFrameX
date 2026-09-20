using CapFrameX.Statistics.PlotBuilder;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OxyPlot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CapFrameX.Test.Statistics
{
    [TestClass]
    public class LineSeriesDecimationTest
    {
        [TestMethod]
        public void DecimateScreenPoints_PreservesEndpointsAndPixelExtrema()
        {
            var input = new List<ScreenPoint>
            {
                new ScreenPoint(0.1, 5),
                new ScreenPoint(0.2, 10),
                new ScreenPoint(0.3, -20),
                new ScreenPoint(0.4, 30),
                new ScreenPoint(1.1, 8),
                new ScreenPoint(1.2, -4),
                new ScreenPoint(2.1, 7)
            };
            var output = new List<ScreenPoint>();

            LineSeries.DecimateScreenPoints(input, output);

            Assert.AreEqual(input.First(), output.First());
            Assert.AreEqual(input.Last(), output.Last());
            Assert.IsTrue(output.Contains(new ScreenPoint(0.3, -20)));
            Assert.IsTrue(output.Contains(new ScreenPoint(0.4, 30)));
            Assert.IsTrue(output.Contains(new ScreenPoint(1.2, -4)));
            Assert.IsTrue(output.Count < input.Count);
        }

        [TestMethod]
        public void DecimateScreenPoints_TwoPoints_PreservesBoth()
        {
            var input = new List<ScreenPoint>
            {
                new ScreenPoint(1, 10),
                new ScreenPoint(2, 20)
            };
            var output = new List<ScreenPoint>();

            LineSeries.DecimateScreenPoints(input, output);

            CollectionAssert.AreEqual(input, output);
        }

        [TestMethod]
        public void DecimateScreenPoints_Gap_PreservesSeparatedRunsAndExtrema()
        {
            var gap = new ScreenPoint(double.NaN, double.NaN);
            var input = new List<ScreenPoint>
            {
                new ScreenPoint(0.1, 0),
                new ScreenPoint(0.2, -10),
                new ScreenPoint(0.3, 20),
                new ScreenPoint(1.1, 1),
                gap,
                new ScreenPoint(2.1, 2),
                new ScreenPoint(2.2, -30),
                new ScreenPoint(2.3, 40),
                new ScreenPoint(3.1, 3)
            };
            var output = new List<ScreenPoint>();

            LineSeries.DecimateScreenPoints(input, output);

            int gapIndex = output.FindIndex(point => double.IsNaN(point.X) && double.IsNaN(point.Y));
            Assert.IsTrue(gapIndex > 0 && gapIndex < output.Count - 1);
            Assert.IsTrue(output.Take(gapIndex).Contains(new ScreenPoint(0.2, -10)));
            Assert.IsTrue(output.Take(gapIndex).Contains(new ScreenPoint(0.3, 20)));
            Assert.IsTrue(output.Skip(gapIndex + 1).Contains(new ScreenPoint(2.2, -30)));
            Assert.IsTrue(output.Skip(gapIndex + 1).Contains(new ScreenPoint(2.3, 40)));
        }
    }
}
