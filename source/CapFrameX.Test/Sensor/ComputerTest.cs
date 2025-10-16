using LibreHardwareMonitor.Hardware;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;

namespace CapFrameX.Test.Sensor
{
    [TestClass]
    public class ComputerTest
    {
        [TestMethod]
        public void InitializeHardware_AnyDectedHardware()
        {
            var computer = new Computer();
            computer.HardwareAdded += new HardwareEventHandler(h => { });
            computer.HardwareRemoved += new HardwareEventHandler(h => { });

            computer.Open();

            computer.IsMotherboardEnabled = true;
            computer.IsGpuEnabled = true;
            computer.IsCpuEnabled = true;
            computer.IsMemoryEnabled = true;

            Assert.IsTrue(computer.Hardware.Any());

            computer.Close();
        }
    }
}
