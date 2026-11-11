using CapFrameX.ViewModel;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OxyPlot;
using OxyPlot.Series;
using System.Reflection;

namespace CapFrameX.Test.ViewModel
{
    [TestClass]
    public class ComparisonSeriesBoundsTest
    {
        [TestMethod]
        public void TryGetSeriesBounds_UsesRenderedFinitePoints()
        {
            var model = new PlotModel();
            var first = new LineSeries();
            first.Points.Add(new DataPoint(1, 10));
            first.Points.Add(new DataPoint(2, double.PositiveInfinity));
            first.Points.Add(new DataPoint(3, -5));
            var second = new LineSeries();
            second.Points.Add(new DataPoint(-2, 4));
            second.Points.Add(new DataPoint(double.NaN, 100));
            model.Series.Add(first);
            model.Series.Add(second);

            var arguments = new object[] { model, 0d, 0d, 0d, 0d, double.PositiveInfinity };

            bool result = InvokeTryGetSeriesBounds(arguments);

            Assert.IsTrue(result);
            Assert.AreEqual(-2d, (double)arguments[1]);
            Assert.AreEqual(3d, (double)arguments[2]);
            Assert.AreEqual(-5d, (double)arguments[3]);
            Assert.AreEqual(10d, (double)arguments[4]);
        }

        [TestMethod]
        public void TryGetSeriesBounds_NoFinitePoints_ReturnsFalse()
        {
            var model = new PlotModel();
            var series = new LineSeries();
            series.Points.Add(new DataPoint(double.NaN, double.NaN));
            model.Series.Add(series);
            var arguments = new object[] { model, 0d, 0d, 0d, 0d, double.PositiveInfinity };

            bool result = InvokeTryGetSeriesBounds(arguments);

            Assert.IsFalse(result);
        }

        [TestMethod]
        public void TryGetSeriesBounds_SkipsPointsBeyondTheLimit()
        {
            var model = new PlotModel();
            var series = new LineSeries();
            series.Points.Add(new DataPoint(10.1, 81.5));
            series.Points.Add(new DataPoint(22728.1, 18.5));
            model.Series.Add(series);
            var arguments = new object[] { model, 0d, 0d, 0d, 0d, 10.1 };

            bool result = InvokeTryGetSeriesBounds(arguments);

            Assert.IsTrue(result);
            Assert.AreEqual(10.1, (double)arguments[2]);
            Assert.AreEqual(81.5, (double)arguments[4]);
        }

        [TestMethod]
        public void GetReadableDistributionMaximum_KeepsTheWidestRecordWhole()
        {
            // The first record's 22.7 s stall is one frame; the second record has a real tail at 30 ms.
            var stall = new LineSeries();
            stall.Points.Add(new DataPoint(10.1, 81.5));
            stall.Points.Add(new DataPoint(22728.1, 18.5));
            var tail = new LineSeries();
            tail.Points.Add(new DataPoint(10.1, 50));
            tail.Points.Add(new DataPoint(30.1, 50));
            var model = new PlotModel();
            model.Series.Add(stall);

            Assert.AreEqual(10.1, InvokeGetReadableDistributionMaximum(model), 1e-9);

            model.Series.Add(tail);

            Assert.AreEqual(30.1, InvokeGetReadableDistributionMaximum(model), 1e-9);
        }

        [TestMethod]
        public void GetReadableDistributionMaximum_WithoutPoints_IsUnlimited()
        {
            var model = new PlotModel();
            model.Series.Add(new LineSeries());

            Assert.AreEqual(double.PositiveInfinity, InvokeGetReadableDistributionMaximum(model));
        }

        private static bool InvokeTryGetSeriesBounds(object[] arguments)
            => (bool)typeof(ComparisonViewModel).GetMethod("TryGetSeriesBounds",
                BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, arguments);

        private static double InvokeGetReadableDistributionMaximum(PlotModel model)
            => (double)typeof(ComparisonViewModel).GetMethod("GetReadableDistributionMaximum",
                BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[] { model });
    }
}
