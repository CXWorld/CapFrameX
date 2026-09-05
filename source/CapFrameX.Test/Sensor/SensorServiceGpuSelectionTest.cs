using System.Collections.Generic;

using LibreHardwareMonitor.Hardware;
using LibreHardwareMonitor.Hardware.Gpu;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CapFrameX.Test.Sensor
{
    [TestClass]
    public class SensorServiceGpuSelectionTest
    {
        [TestMethod]
        public void AutoSelection_UpdatesDiscreteGpuButNotIntegratedGpu()
        {
            var integratedGpu = new TestGpu("AMD Radeon iGPU", false, 0);
            var discreteGpu = new TestGpu("NVIDIA GeForce RTX 4090", true, 1);
            IHardware[] gpus = { integratedGpu, discreteGpu };

            try
            {
                Assert.IsFalse(CapFrameX.Sensor.SensorService.ShouldUpdateHardware(integratedGpu, gpus, "Auto"));
                Assert.IsTrue(CapFrameX.Sensor.SensorService.ShouldUpdateHardware(discreteGpu, gpus, "Auto"));
            }
            finally
            {
                integratedGpu.Close();
                discreteGpu.Close();
            }
        }

        [TestMethod]
        public void ExplicitSelection_UpdatesOnlySelectedGpu()
        {
            var integratedGpu = new TestGpu("AMD Radeon iGPU", false, 0);
            var discreteGpu = new TestGpu("NVIDIA GeForce RTX 4090", true, 1);
            IHardware[] gpus = { integratedGpu, discreteGpu };

            try
            {
                Assert.IsTrue(CapFrameX.Sensor.SensorService.ShouldUpdateHardware(
                    integratedGpu,
                    gpus,
                    integratedGpu.Name));
                Assert.IsFalse(CapFrameX.Sensor.SensorService.ShouldUpdateHardware(
                    discreteGpu,
                    gpus,
                    integratedGpu.Name));
            }
            finally
            {
                integratedGpu.Close();
                discreteGpu.Close();
            }
        }

        [TestMethod]
        public void AutoSelection_UpdatesIntegratedGpuWhenNoDiscreteGpuExists()
        {
            var firstGpu = new TestGpu("AMD Radeon iGPU 1", false, 0);
            var secondGpu = new TestGpu("AMD Radeon iGPU 2", false, 1);
            IHardware[] gpus = { firstGpu, secondGpu };

            try
            {
                Assert.IsTrue(CapFrameX.Sensor.SensorService.ShouldUpdateHardware(firstGpu, gpus, "Auto"));
                Assert.IsTrue(CapFrameX.Sensor.SensorService.ShouldUpdateHardware(secondGpu, gpus, "Auto"));
            }
            finally
            {
                firstGpu.Close();
                secondGpu.Close();
            }
        }

        private sealed class TestGpu : GenericGpu
        {
            public TestGpu(string name, bool isDiscrete, int index)
                : base(
                    name,
                    new Identifier("gpu-selection-test", index.ToString()),
                    new TestSettings(),
                    enableProcessMemorySensors: false)
            {
                IsDiscreteGpu = isDiscrete;
            }

            public override string DeviceId => Identifier.ToString();

            public override HardwareType HardwareType => HardwareType.GpuAmd;

            public override void Update()
            {
            }
        }

        private sealed class TestSettings : ISettings
        {
            private readonly Dictionary<string, string> _values = new();

            public bool Contains(string name) => _values.ContainsKey(name);

            public string GetValue(string name, string value) =>
                _values.TryGetValue(name, out string storedValue) ? storedValue : value;

            public void Remove(string name) => _values.Remove(name);

            public void SetValue(string name, string value) => _values[name] = value;
        }
    }
}
