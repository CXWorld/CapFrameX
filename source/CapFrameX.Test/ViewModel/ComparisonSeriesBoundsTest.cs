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

            var method = typeof(ComparisonViewModel).GetMethod("TryGetSeriesBounds",
                BindingFlags.NonPublic | BindingFlags.Static);
            var arguments = new object[] { model, 0d, 0d, 0d, 0d };

            bool result = (bool)method.Invoke(null, arguments);

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
            var method = typeof(ComparisonViewModel).GetMethod("TryGetSeriesBounds",
                BindingFlags.NonPublic | BindingFlags.Static);
            var arguments = new object[] { model, 0d, 0d, 0d, 0d };

            bool result = (bool)method.Invoke(null, arguments);

            Assert.IsFalse(result);
        }
    }
}
