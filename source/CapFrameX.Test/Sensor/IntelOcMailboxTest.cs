using LibreHardwareMonitor.PawnIo;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CapFrameX.Test.Sensor
{
    [TestClass]
    public class IntelOcMailboxTest
    {
        [TestMethod]
        public void TryDecodeNguMhz_ArrowLakeSSentinel_UsesStockRatio()
        {
            bool success = IntelOcMailbox.TryDecodeNguMhz(0x3F30, 0, 26, out uint mhz);

            Assert.IsTrue(success);
            Assert.AreEqual(2600U, mhz);
        }

        [TestMethod]
        public void TryDecodeNguMhz_SentinelWithoutFallback_SuppressesClock()
        {
            bool success = IntelOcMailbox.TryDecodeNguMhz(0x3F30, 0, null, out uint mhz);

            Assert.IsFalse(success);
            Assert.AreEqual(0U, mhz);
        }

        [TestMethod]
        public void TryDecodeNguMhz_ValidatedRatioBelowCandidate_UsesValidatedRatio()
        {
            bool success = IntelOcMailbox.TryDecodeNguMhz(0x3F30, 26, null, out uint mhz);

            Assert.IsTrue(success);
            Assert.AreEqual(2600U, mhz);
        }

        [TestMethod]
        public void TryDecodeNguMhz_ValidCandidateWithoutLimit_PreservesCandidate()
        {
            bool success = IntelOcMailbox.TryDecodeNguMhz(0x2000, 0, null, out uint mhz);

            Assert.IsTrue(success);
            Assert.AreEqual(3200U, mhz);
        }
    }
}
