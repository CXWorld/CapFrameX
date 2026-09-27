using CapFrameX.PresentMonInterface;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CapFrameX.Test.PresentMonInterface
{
    [TestClass]
    public class CaptureServiceConfigurationTest
    {
        [TestMethod]
        public void ParseVersionFromAppName_BundledName_ReturnsVersionWithoutArchitecture()
        {
            Assert.AreEqual("2.6.0", CaptureServiceConfiguration.ParseVersionFromAppName("PresentMon-2.6.0-x64"));
        }

        [TestMethod]
        public void ParseVersionFromAppName_NoVersion_ReturnsNull()
        {
            Assert.IsNull(CaptureServiceConfiguration.ParseVersionFromAppName("PresentMon-x64"));
            Assert.IsNull(CaptureServiceConfiguration.ParseVersionFromAppName(null));
        }
    }
}
