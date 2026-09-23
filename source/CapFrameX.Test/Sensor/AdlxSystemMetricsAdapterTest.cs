using LibreHardwareMonitor.Hardware.Gpu;
using LibreHardwareMonitor.Interop;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CapFrameX.Test.Sensor
{
    [TestClass]
    public class AdlxSystemMetricsAdapterTest
    {
        private const uint Undefined = (uint)ADLX.GpuType.Undefined;
        private const uint Integrated = (uint)ADLX.GpuType.Integrated;
        private const uint Discrete = (uint)ADLX.GpuType.Discrete;

        [TestMethod]
        public void SmartShiftLaptop_ReportsOnDiscreteGpu()
        {
            Assert.AreEqual(1, AmdGpuGroup.GetSystemMetricsAdapter(new[] { Integrated, Discrete }));
        }

        [TestMethod]
        public void SeveralDiscreteGpus_ReportsOnFirst()
        {
            Assert.AreEqual(0, AmdGpuGroup.GetSystemMetricsAdapter(new[] { Discrete, Discrete }));
        }

        [TestMethod]
        public void NoDiscreteGpu_ReportsOnFirstAdapter()
        {
            Assert.AreEqual(0, AmdGpuGroup.GetSystemMetricsAdapter(new[] { Integrated }));
            Assert.AreEqual(0, AmdGpuGroup.GetSystemMetricsAdapter(new[] { Undefined, Integrated }));
        }

        [TestMethod]
        public void NoAdapters_ReportsNowhere()
        {
            Assert.AreEqual(-1, AmdGpuGroup.GetSystemMetricsAdapter(new uint[0]));
        }
    }
}
