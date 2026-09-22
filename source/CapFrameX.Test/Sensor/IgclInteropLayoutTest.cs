using System.Runtime.InteropServices;

using LibreHardwareMonitor.Hardware.Gpu;
using LibreHardwareMonitor.Interop;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CapFrameX.Test.Sensor
{
    [TestClass]
    public class IgclInteropLayoutTest
    {
        // Must match the static_asserts in CapFrameX.IGCL/IGCLManager.h.
        [TestMethod]
        public void TelemetryData_MatchesNativeLayout()
        {
            Assert.AreEqual(16, Marshal.SizeOf<IgclTelemetryItem>());
            Assert.AreEqual(40, Marshal.SizeOf<IgclPsuRail>());
            Assert.AreEqual(648, Marshal.SizeOf<IgclTelemetryData>());
            Assert.AreEqual(384, (int)Marshal.OffsetOf<IgclTelemetryData>(nameof(IgclTelemetryData.fan2Speed)));
            Assert.AreEqual(448, (int)Marshal.OffsetOf<IgclTelemetryData>(nameof(IgclTelemetryData.psu1)));
        }

        [TestMethod]
        public void PsuRailName_SingleConnectorOfEachType_IsNotNumbered()
        {
            IgclPsuRail[] rails =
            {
                Rail(IGCL.CTL_PSU_TYPE_PSU_PCIE),
                Rail(IGCL.CTL_PSU_TYPE_PSU_8PIN),
                Rail(IGCL.CTL_PSU_TYPE_PSU_6PIN),
                default,
                default
            };

            Assert.AreEqual("GPU PCIe Slot", IntelGclGpu.GetPsuRailName(0, rails));
            Assert.AreEqual("GPU 8-Pin", IntelGclGpu.GetPsuRailName(1, rails));
            Assert.AreEqual("GPU 6-Pin", IntelGclGpu.GetPsuRailName(2, rails));
        }

        [TestMethod]
        public void PsuRailName_SeveralConnectorsOfOneType_AreNumberedInRailOrder()
        {
            IgclPsuRail[] rails =
            {
                Rail(IGCL.CTL_PSU_TYPE_PSU_PCIE),
                Rail(IGCL.CTL_PSU_TYPE_PSU_8PIN),
                Rail(IGCL.CTL_PSU_TYPE_PSU_8PIN),
                default,
                default
            };

            Assert.AreEqual("GPU PCIe Slot", IntelGclGpu.GetPsuRailName(0, rails));
            Assert.AreEqual("GPU 8-Pin 1", IntelGclGpu.GetPsuRailName(1, rails));
            Assert.AreEqual("GPU 8-Pin 2", IntelGclGpu.GetPsuRailName(2, rails));
        }

        [TestMethod]
        public void PsuRailName_UnreportedRailOfSameType_DoesNotCount()
        {
            IgclPsuRail unreported8Pin = new IgclPsuRail { type = IGCL.CTL_PSU_TYPE_PSU_8PIN };
            IgclPsuRail[] rails = { Rail(IGCL.CTL_PSU_TYPE_PSU_8PIN), unreported8Pin, default, default, default };

            Assert.AreEqual("GPU 8-Pin", IntelGclGpu.GetPsuRailName(0, rails));
        }

        [TestMethod]
        public void PsuRailName_UnknownType_FallsBackToPsu()
        {
            IgclPsuRail[] rails = { Rail(0), Rail(0), default, default, default };

            Assert.AreEqual("GPU PSU 1", IntelGclGpu.GetPsuRailName(0, rails));
            Assert.AreEqual("GPU PSU 2", IntelGclGpu.GetPsuRailName(1, rails));
        }

        private static IgclPsuRail Rail(int type) => new IgclPsuRail
        {
            type = type,
            power = new IgclTelemetryItem { supported = true, value = 42.0 },
            voltage = new IgclTelemetryItem { supported = true, value = 12.0 }
        };
    }
}
