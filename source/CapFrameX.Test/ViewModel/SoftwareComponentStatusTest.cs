using CapFrameX.Overlay;
using CapFrameX.ViewModel.SubModels;
using LibreHardwareMonitor.PawnIo;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CapFrameX.Test.ViewModel
{
    [TestClass]
    public class SoftwareComponentStatusTest
    {
        [TestMethod]
        public void FromPawnIo_Running_ShowsVersionAsRunning()
        {
            var status = SoftwareComponentStatus.FromPawnIo(new PawnIoDriverStatus(PawnIoDriverState.Running, "2.2.0"));

            Assert.AreEqual("2.2.0", status.Version);
            Assert.AreEqual("Running", status.State);
            Assert.AreEqual("#4CAF50", status.StatusColor);
        }

        [TestMethod]
        public void FromPawnIo_Blocked_IsRedAndKeepsTheRefusedVersion()
        {
            var status = SoftwareComponentStatus.FromPawnIo(new PawnIoDriverStatus(PawnIoDriverState.Blocked, "2.2.0"));

            Assert.AreEqual("2.2.0", status.Version);
            Assert.AreEqual("Blocked by Windows", status.State);
            Assert.AreEqual("#F44336", status.StatusColor);
        }

        [TestMethod]
        public void FromPawnIo_NotInstalled_ShowsPlaceholderVersion()
        {
            var status = SoftwareComponentStatus.FromPawnIo(new PawnIoDriverStatus(PawnIoDriverState.NotInstalled, null));

            Assert.AreEqual("–", status.Version);
            Assert.AreEqual("Not installed", status.State);
        }

        [TestMethod]
        public void FromPawnIo_NoStatus_IsUnknown()
        {
            Assert.AreEqual("Unknown", SoftwareComponentStatus.FromPawnIo(null).State);
        }

        [TestMethod]
        public void ForPresentMon_MissingExecutable_IsRed()
        {
            var status = SoftwareComponentStatus.ForPresentMon("2.6.0", isPresent: false);

            Assert.AreEqual("Missing", status.State);
            Assert.AreEqual("#F44336", status.StatusColor);
        }

        [TestMethod]
        public void ForRtss_NoVersion_IsNotInstalled()
        {
            var status = SoftwareComponentStatus.ForRtss(null);

            Assert.AreEqual("–", status.Version);
            Assert.AreEqual("Not installed", status.State);
        }

        [TestMethod]
        public void ForRtss_Version_IsInstalled()
        {
            var status = SoftwareComponentStatus.ForRtss("7.3.5.28314");

            Assert.AreEqual("7.3.5.28314", status.Version);
            Assert.AreEqual("Installed", status.State);
        }

        [TestMethod]
        public void FromVulkanLayer_Registered_ShowsManifestVersionAndPaths()
        {
            var status = SoftwareComponentStatus.FromVulkanLayer(new VulkanLayerRegistrationStatus(
                VulkanLayerRegistrationState.Registered, "1", @"x64: C:\cfx\cfx_osd_vklayer_v1.json"));

            Assert.AreEqual("v1", status.Version);
            Assert.AreEqual("Registered", status.State);
            StringAssert.Contains(status.ToolTip, @"x64: C:\cfx\cfx_osd_vklayer_v1.json");
        }

        [TestMethod]
        public void FromVulkanLayer_Conflicting_IsRed()
        {
            var status = SoftwareComponentStatus.FromVulkanLayer(new VulkanLayerRegistrationStatus(
                VulkanLayerRegistrationState.Conflicting, "1", null));

            Assert.AreEqual("Conflicting registration", status.State);
            Assert.AreEqual("#F44336", status.StatusColor);
        }
    }
}
