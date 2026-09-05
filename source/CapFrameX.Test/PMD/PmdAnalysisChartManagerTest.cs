using CapFrameX.PMD;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OxyPlot;

namespace CapFrameX.Test.PMD
{
    [TestClass]
    public class PmdAnalysisChartManagerTest
    {
        [TestMethod]
        public void DrawEps12VChart_ZeroPowerUsesValidAxisRange()
        {
            var manager = new PmdAnalysisChartManager();

            manager.DrawEps12VChart(new[] { new DataPoint(0, 0) });

            var axis = manager.AxisDefinitions["Y_Axis_CPU_W"];
            Assert.AreEqual(150d, axis.Maximum);
            Assert.AreEqual(150d, axis.AbsoluteMaximum);
            Assert.IsTrue(axis.AbsoluteMaximum > axis.AbsoluteMinimum);
        }

        [TestMethod]
        public void DrawPciExpressChart_ZeroPowerUsesValidAxisRange()
        {
            var manager = new PmdAnalysisChartManager();

            manager.DrawPciExpressChart(new[] { new DataPoint(0, 0) });

            var axis = manager.AxisDefinitions["Y_Axis_GPU_W"];
            Assert.AreEqual(300d, axis.Maximum);
            Assert.AreEqual(300d, axis.AbsoluteMaximum);
            Assert.IsTrue(axis.AbsoluteMaximum > axis.AbsoluteMinimum);
        }
    }
}
