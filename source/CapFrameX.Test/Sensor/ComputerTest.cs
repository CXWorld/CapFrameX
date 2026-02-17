using CapFrameX.Monitoring.Contracts;
using LibreHardwareMonitor.Hardware;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;
using System.Reactive.Subjects;

namespace CapFrameX.Test.Sensor
{
    [TestClass]
    public class ComputerTest
    {
        [TestMethod]
        public void InitializeHardware_AnyDectedHardware()
        {
            // GenericGpu constructor requires ProcessServiceProvider.ProcessService
            if (ProcessServiceProvider.ProcessService == null)
            {
                ProcessServiceProvider.ProcessService = new StubProcessService();
            }

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

        private class StubProcessService : IProcessService
        {
            public ISubject<int> ProcessIdStream { get; } = new BehaviorSubject<int>(0);
        }
    }
}
