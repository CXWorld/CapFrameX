using CapFrameX.Contracts.Data;
using CapFrameX.ViewModel.SubModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CapFrameX.Test.ViewModel
{
    [TestClass]
    public class PlatformSecurityStatusTest
    {
        private const string Green = "#4CAF50";
        private const string Orange = "#FF9800";

        [TestMethod]
        public void SecureBoot_On_IsGreen()
        {
            var status = PlatformSecurityStatus.ForSecureBoot(ESystemInfoTertiaryStatus.Enabled);

            Assert.AreEqual("On", status.State);
            Assert.AreEqual(Green, status.StatusColor);
        }

        [TestMethod]
        public void TestSigning_On_IsOrange()
        {
            Assert.AreEqual(Orange, PlatformSecurityStatus.ForTestSigning(ESystemInfoTertiaryStatus.Enabled).StatusColor);
        }

        [TestMethod]
        public void Vbs_Running_IsOrange()
        {
            var status = PlatformSecurityStatus.ForVirtualizationBasedSecurity(ESystemInfoTertiaryStatus.Enabled);

            Assert.AreEqual("Running", status.State);
            Assert.AreEqual(Orange, status.StatusColor);
        }

        [TestMethod]
        public void MemoryIntegrity_Off_IsGreen()
        {
            Assert.AreEqual(Green, PlatformSecurityStatus.ForMemoryIntegrity(ESystemInfoTertiaryStatus.Disabled).StatusColor);
        }

        [TestMethod]
        public void Error_IsUnknown()
        {
            Assert.AreSame(PlatformSecurityStatus.Unknown, PlatformSecurityStatus.ForSecureBoot(ESystemInfoTertiaryStatus.Error));
        }

        [TestMethod]
        public void SameState_ReturnsSharedInstance()
        {
            // The Info tab refreshes these every second; a shared instance keeps SetProperty quiet.
            Assert.AreSame(PlatformSecurityStatus.ForMemoryIntegrity(ESystemInfoTertiaryStatus.Enabled),
                PlatformSecurityStatus.ForMemoryIntegrity(ESystemInfoTertiaryStatus.Enabled));
        }
    }
}
