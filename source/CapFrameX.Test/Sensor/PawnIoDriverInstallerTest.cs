using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CapFrameX.Test.Sensor
{
    [TestClass]
    public class PawnIoDriverInstallerTest
    {
        private const string WindowsDirectory = @"C:\Windows";

        [TestMethod]
        public void ResolveServiceImagePath_SystemRootPrefix_MapsIntoWindowsDirectory()
        {
            string path = DriverInstaller.ResolveServiceImagePath(
                @"\SystemRoot\System32\DriverStore\FileRepository\pawnio.inf_amd64_a72a2f969b8b7496\PawnIO.sys", WindowsDirectory);

            Assert.AreEqual(@"C:\Windows\System32\DriverStore\FileRepository\pawnio.inf_amd64_a72a2f969b8b7496\PawnIO.sys", path);
        }

        [TestMethod]
        public void ResolveServiceImagePath_DosDevicesPrefix_IsStripped()
        {
            string path = DriverInstaller.ResolveServiceImagePath(@"\??\E:\CapFrameX\PawnIo\PawnIO.sys", WindowsDirectory);

            Assert.AreEqual(@"E:\CapFrameX\PawnIo\PawnIO.sys", path);
        }

        [TestMethod]
        public void ResolveServiceImagePath_RelativePath_IsRelativeToWindowsDirectory()
        {
            string path = DriverInstaller.ResolveServiceImagePath(@"System32\drivers\PawnIO.sys", WindowsDirectory);

            Assert.AreEqual(@"C:\Windows\System32\drivers\PawnIO.sys", path);
        }

        [TestMethod]
        public void ResolveServiceImagePath_QuotedAbsolutePath_IsUnquoted()
        {
            string path = DriverInstaller.ResolveServiceImagePath("\"C:\\Program Files\\PawnIO\\PawnIO.sys\"", WindowsDirectory);

            Assert.AreEqual(@"C:\Program Files\PawnIO\PawnIO.sys", path);
        }

        [TestMethod]
        public void ResolveServiceImagePath_Missing_ReturnsNull()
        {
            Assert.IsNull(DriverInstaller.ResolveServiceImagePath(null, WindowsDirectory));
            Assert.IsNull(DriverInstaller.ResolveServiceImagePath("  ", WindowsDirectory));
        }

        [TestMethod]
        public void FindPublishedInf_OemEntry_IsReturned()
        {
            Assert.AreEqual("oem50.inf", DriverInstaller.FindPublishedInf(new[] { "oem50.inf" }));
        }

        [TestMethod]
        public void FindPublishedInf_InboxEntriesOnly_ReturnsNull()
        {
            Assert.IsNull(DriverInstaller.FindPublishedInf(new[] { "machine.inf" }));
            Assert.IsNull(DriverInstaller.FindPublishedInf(null));
        }
    }
}
