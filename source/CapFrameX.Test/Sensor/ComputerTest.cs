using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenHardwareMonitor.Hardware;
using System.Linq;

namespace CapFrameX.Test.Sensor
{
    [TestClass]
    public class ComputerTest
    {
        [TestMethod]
        public void InitializeHardware_AnyDectedHardware()
        {
            var computer = new Computer(null, null, null);
            computer.HardwareAdded += new HardwareEventHandler(h => { });
            computer.HardwareRemoved += new HardwareEventHandler(h => { });

            computer.Open();

            computer.MainboardEnabled = true;
            computer.FanControllerEnabled = true;
            computer.GPUEnabled = true;
            computer.CPUEnabled = true;
            computer.RAMEnabled = true;
            computer.HDDEnabled = true;

            Assert.IsTrue(computer.Hardware.Any());

            computer.Close();
        }
    }
}
