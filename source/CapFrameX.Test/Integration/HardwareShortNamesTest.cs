using CapFrameX.OSD.Integration;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CapFrameX.Test.Integration
{
    [TestClass]
    public class HardwareShortNamesTest
    {
        [TestMethod]
        [DataRow("NVIDIA GeForce RTX 5070 Ti", "RTX 5070 Ti")]
        [DataRow("NVIDIA GeForce GTX 1080 Ti", "GTX 1080 Ti")]
        [DataRow("NVIDIA TITAN RTX", "TITAN RTX")]
        [DataRow("AMD Radeon RX 9070 XT", "RX 9070 XT")]
        [DataRow("AMD Radeon(TM) 890M Graphics", "Radeon 890M")]
        [DataRow("AMD Radeon Pro W7900", "Radeon Pro W7900")]
        [DataRow("Intel(R) Arc(TM) 140V GPU (16GB)", "Arc 140V")]
        [DataRow("Intel(R) Arc(TM) B580 Graphics", "Arc B580")]
        [DataRow("Intel(R) Arc(TM) B390 GPU", "Arc B390")]
        [DataRow("NVIDIA GeForce RTX 4070 SUPER", "RTX 4070 SUPER")]
        [DataRow("Intel(R) Arc(TM) Graphics", "Arc Graphics")]
        [DataRow("Intel(R) UHD Graphics 770", "UHD 770")]
        [DataRow("Intel(R) Iris(R) Xe Graphics", "Iris Xe")]
        [DataRow("Microsoft Basic Render Driver", "Microsoft Basic Render Driver")]
        [DataRow("My GPU", "My GPU")]
        public void GpuNamesKeepOnlyTheModel(string name, string expected)
            => Assert.AreEqual(expected, HardwareShortNames.Gpu(name));

        [TestMethod]
        [DataRow("Intel Core i5-14600K", "i5-14600K")]
        [DataRow("13th Gen Intel(R) Core(TM) i7-13700K", "i7-13700K")]
        [DataRow("Intel(R) Core(TM) i9-9900K CPU @ 3.60GHz", "i9-9900K")]
        [DataRow("Intel Core Ultra 7 258V", "Ultra 7 258V")]
        [DataRow("Intel(R) Core(TM) Ultra 9 285K", "Ultra 9 285K")]
        [DataRow("Intel(R) Core(TM) Ultra X7 358H", "Ultra X7 358H")]
        [DataRow("Intel(R) Core(TM) 7 150U", "Core 7 150U")]
        [DataRow("Intel(R) Xeon(R) W-3175X CPU @ 3.10GHz", "Xeon W-3175X")]
        [DataRow("AMD Ryzen 7 9800X3D 8-Core Processor", "R7 9800X3D")]
        [DataRow("AMD Ryzen 9 5950X 16-Core Processor", "R9 5950X")]
        [DataRow("AMD Ryzen 7 5700G with Radeon Graphics", "R7 5700G")]
        [DataRow("AMD Ryzen AI 9 HX 370 w/ Radeon 890M", "AI 9 HX 370")]
        [DataRow("AMD Ryzen Threadripper PRO 7995WX 96-Cores", "Ryzen Threadripper PRO 7995WX")]
        [DataRow("My CPU", "My CPU")]
        public void CpuNamesKeepOnlyTheModel(string name, string expected)
            => Assert.AreEqual(expected, HardwareShortNames.Cpu(name));

        [TestMethod]
        public void MissingNamesStayMissing()
        {
            Assert.IsNull(HardwareShortNames.Gpu(null));
            Assert.AreEqual("", HardwareShortNames.Cpu(""));
        }
    }
}
