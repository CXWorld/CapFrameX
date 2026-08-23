using CapFrameX.ApiInterface;
using CapFrameX.PresentMonInterface;
using CapFrameX.Statistics.NetStandard.Contracts;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace CapFrameX.Test.ApiInterface
{
    [TestClass]
    public class MetricsControllerTest
    {
        [TestMethod]
        public void GetOsd_WithBusyMetricNames_ReturnsValuesInRequestedOrder()
        {
            var metricService = new Mock<IOnlineMetricService>();
            metricService.Setup(x => x.GetOnlineFpsMetricValue(EMetric.Average)).Returns(120);
            metricService.Setup(x => x.GetOnlineGpuActiveTimeMetricValue(EMetric.GpuActiveAverage)).Returns(8.5);
            metricService.Setup(x => x.GetOnlineGpuActiveTimeDeviationMetricValue()).Returns(14);
            metricService.Setup(x => x.GetOnlineCpuActiveTimeMetricValue(EMetric.CpuActiveAverage)).Returns(3.25);
            var controller = new MetricsController(metricService.Object);

            var metrics = controller.GetOsd("Average, gpubusy , GPUBusyDeviation, cpubusy");

            CollectionAssert.AreEqual(new[] { 120.0, 8.5, 14.0, 3.25 }, metrics);
        }

        [TestMethod]
        public void GetOsd_WithActiveMetricEnumNames_UsesActiveTimeSources()
        {
            var metricService = new Mock<IOnlineMetricService>();
            metricService.Setup(x => x.GetOnlineGpuActiveTimeMetricValue(EMetric.GpuActiveAverage)).Returns(8.5);
            metricService.Setup(x => x.GetOnlineGpuActiveTimeMetricValue(EMetric.GpuActiveP1)).Returns(12.5);
            metricService.Setup(x => x.GetOnlineGpuActiveTimeMetricValue(EMetric.GpuActiveOnePercentLowAverage)).Returns(13.5);
            metricService.Setup(x => x.GetOnlineCpuActiveTimeMetricValue(EMetric.CpuActiveAverage)).Returns(3.25);
            var controller = new MetricsController(metricService.Object);

            var metrics = controller.GetOsd(
                "GpuActiveAverage,GpuActiveP1,GpuActiveOnePercentLowAverage,CpuActiveAverage");

            CollectionAssert.AreEqual(new[] { 8.5, 12.5, 13.5, 3.25 }, metrics);
        }
    }
}
