using LibreHardwareMonitor.PawnIo;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CapFrameX.Test.Sensor
{
    [TestClass]
    public class IntelImcTest
    {
        [DataTestMethod]
        [DataRow(0x00000020U, IntelImc.ImcGear.Gear1)]
        [DataRow(0x00000120U, IntelImc.ImcGear.Gear1)]
        [DataRow(0x00001020U, IntelImc.ImcGear.Gear2)]
        [DataRow(0x00001120U, IntelImc.ImcGear.Gear2)]
        [DataRow(0x00002020U, IntelImc.ImcGear.Gear4)]
        [DataRow(0x00002120U, IntelImc.ImcGear.Gear4)]
        public void TryDecodeAdlRplGear_UsesGearFieldIndependentlyOfReferenceClock(
            uint raw, IntelImc.ImcGear expectedGear)
        {
            // MC_BIOS_DATA: GEAR is bits 13:12; the 133/100 MHz reference
            // selector is bits 11:8. A 100 MHz reference must not imply Gear2.
            bool success = IntelImc.TryDecodeAdlRplGear(raw, out var gear);

            Assert.IsTrue(success);
            Assert.AreEqual(expectedGear, gear);
        }

        [DataTestMethod]
        [DataRow(0x00003020U)]
        [DataRow(0x00003120U)]
        public void TryDecodeAdlRplGear_ReservedGearDoesNotProduceAReading(uint raw)
        {
            bool success = IntelImc.TryDecodeAdlRplGear(raw, out var gear);

            Assert.IsFalse(success);
            Assert.AreEqual(IntelImc.ImcGear.Unknown, gear);
        }

        [TestMethod]
        public void TryDecodeAdlRplGear_IgnoresVoltageAndCurrentFields()
        {
            bool success = IntelImc.TryDecodeAdlRplGear(0x79E00120U, out var gear);

            Assert.IsTrue(success);
            Assert.AreEqual(IntelImc.ImcGear.Gear1, gear);
        }
    }
}
