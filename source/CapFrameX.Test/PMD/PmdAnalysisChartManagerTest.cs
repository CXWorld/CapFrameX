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

        [TestMethod]
        public void ResetRealTimePlotModels_LiftsPowerAxisLimitsOfPreviousSession()
        {
            var manager = new PmdAnalysisChartManager();
            manager.DrawEps12VChart(new[] { new DataPoint(0, 70) });
            manager.DrawPciExpressChart(new[] { new DataPoint(0, 70) });

            manager.ResetRealTimePlotModels();

            var cpuAxis = manager.AxisDefinitions["Y_Axis_CPU_W"];
            Assert.AreEqual(150d, cpuAxis.Maximum);
            Assert.AreEqual(double.MaxValue, cpuAxis.AbsoluteMaximum);
            var gpuAxis = manager.AxisDefinitions["Y_Axis_GPU_W"];
            Assert.AreEqual(300d, gpuAxis.Maximum);
            Assert.AreEqual(double.MaxValue, gpuAxis.AbsoluteMaximum);

            // The rendered axes show the default range, not the previous session's 80 W peak.
            SvgExporter.ExportToString(manager.Eps12VModel, 800, 400, false);
            SvgExporter.ExportToString(manager.PciExpressModel, 800, 400, false);
            Assert.AreEqual(150d, cpuAxis.ActualMaximum);
            Assert.AreEqual(300d, gpuAxis.ActualMaximum);
        }
    }
}
