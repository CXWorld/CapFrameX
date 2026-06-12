using LibreHardwareMonitor.PawnIo;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Reflection;

namespace CapFrameX.Test.Sensor
{
    [TestClass]
    public class IntelOobmsmPlatformTest
    {
        [TestMethod]
        public void GetPlatform_ArrowLakeDesktop_IsNotOobmsmCandidate()
        {
            Assert.AreEqual(IntelOobmsm.Platform.Arl, GetPlatform(0x6, 0xB5));
            Assert.AreEqual(IntelOobmsm.Platform.Arl, GetPlatform(0x6, 0xC5));
            Assert.AreEqual(IntelOobmsm.Platform.None, GetPlatform(0x6, 0xC6));
        }

        [TestMethod]
        public void GetPlatform_PantherLake_RemainsOobmsmCandidate()
        {
            Assert.AreEqual(IntelOobmsm.Platform.Ptl, GetPlatform(0x6, 0xCC));
        }

        private static IntelOobmsm.Platform GetPlatform(uint family, uint model)
        {
            MethodInfo method = typeof(IntelOobmsm).GetMethod("GetPlatform", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(method);
            return (IntelOobmsm.Platform)method.Invoke(null, new object[] { family, model });
        }
    }
}
