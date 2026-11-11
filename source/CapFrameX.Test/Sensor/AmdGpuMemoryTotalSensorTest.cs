using System.Collections.Generic;
using System.Linq;

using LibreHardwareMonitor.Hardware;
using LibreHardwareMonitor.Hardware.Gpu;
using LibreHardwareMonitor.Interop;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CapFrameX.Test.Sensor
{
    [TestClass]
    public class AmdGpuMemoryTotalSensorTest
    {
        [TestMethod]
        [DataRow(16384u, 16f)]
        [DataRow(1536u, 1.5f)]
        public void AmdGpu_TotalVramIsAvailableWithoutTelemetryPolling(uint totalVramMB, float expectedGb)
        {
            var gpu = new AmdGpu(7, new ADLX.AdlxDeviceInfo
            {
                GpuName = "Test Radeon",
                GpuType = (uint)ADLX.GpuType.Discrete,
                TotalVramMB = totalVramMB
            }, new TestSettings());

            try
            {
                // Read directly after construction: capacity must not require ADLX polling.
                ISensor sensor = gpu.Sensors.Single(item => item.Name == "GPU Memory Total");

                Assert.AreEqual(SensorType.Data, sensor.SensorType);
                Assert.AreEqual("/gpu-amd/7/data/2", sensor.Identifier.ToString());
                Assert.IsTrue(sensor.Value.HasValue);
                Assert.AreEqual(expectedGb, sensor.Value.Value, 0.001f);
            }
            finally
            {
                gpu.Close();
            }
        }

        [TestMethod]
        public void AmdGpu_UnavailableTotalVramDoesNotExposeCapacitySensor()
        {
            var gpu = new AmdGpu(7, new ADLX.AdlxDeviceInfo
            {
                GpuName = "Test Radeon",
                GpuType = (uint)ADLX.GpuType.Discrete,
                TotalVramMB = 0
            }, new TestSettings());

            try
            {
                Assert.IsFalse(gpu.Sensors.Any(item => item.Name == "GPU Memory Total"));
            }
            finally
            {
                gpu.Close();
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
