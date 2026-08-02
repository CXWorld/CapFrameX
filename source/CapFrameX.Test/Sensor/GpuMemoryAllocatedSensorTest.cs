using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using LibreHardwareMonitor.Hardware;
using LibreHardwareMonitor.Hardware.Gpu;
using LibreHardwareMonitor.Hardware.Simulation;
using LibreHardwareMonitor.Interop;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CapFrameX.Test.Sensor
{
    [TestClass]
    public class GpuMemoryAllocatedSensorTest
    {
        [TestMethod]
        public void SimulatedNvidiaGpu_ExposesAllocatedMemorySensor()
        {
            var gpu = new SimulatedNvidiaGpu(0, new TestSettings());

            try
            {
                AssertAllocatedSensor(gpu, "/gpu-nvidia-sim/0/data/5");
            }
            finally
            {
                gpu.Close();
            }
        }

        [TestMethod]
        public void SimulatedAmdGpu_ExposesAllocatedMemorySensor()
        {
            var gpu = new SimulatedAmdGpu(0, new TestSettings());

            try
            {
                AssertAllocatedSensor(gpu, "/gpu-amd-sim/0/data/4");
            }
            finally
            {
                gpu.Close();
            }
        }

        [TestMethod]
        public void SimulatedIntelD3dGpu_ExposesAllocatedMemorySensor()
        {
            var gpu = new SimulatedIntelD3DGpu(0, new TestSettings());

            try
            {
                AssertAllocatedSensor(gpu, "/gpu-intel-d3d-sim/0/data/4");
            }
            finally
            {
                gpu.Close();
            }
        }

        [TestMethod]
        public void GenericGpu_UpdatesAllocatedMemoryFromDedicatedCommittedSegments()
        {
            var gpu = new TestWddmGpu(new TestSettings());

            try
            {
                gpu.ApplyWddmMemory(new D3DDisplayDevice.D3DDeviceInfo
                {
                    GpuDedicatedCommitted = 3UL * 1024 * 1024 * 1024
                });

                ISensor sensor = gpu.Sensors.Single(item => item.Name == "GPU Memory Allocated");
                Assert.AreEqual(3f, sensor.Value.GetValueOrDefault(), 0.001f);
                Assert.AreEqual("/gpu-wddm-test/0/data/5", sensor.Identifier.ToString());
            }
            finally
            {
                gpu.Close();
            }
        }

        [TestMethod]
        public void DeviceIdentifiersMatch_InterfacePathAndPnpInstance()
        {
            const string interfacePath = @"\\?\PCI#VEN_1002&DEV_744C&SUBSYS_0E3A1002&REV_C8#6&12345678&0&00000019#{1ca05180-a699-450a-9a0c-de4fbe3ddd89}";
            const string pnpInstance = @"PCI\VEN_1002&DEV_744C&SUBSYS_0E3A1002&REV_C8\6&12345678&0&00000019";

            Assert.IsTrue(D3DDisplayDevice.DeviceIdentifiersMatch(interfacePath, pnpInstance));
            Assert.IsFalse(D3DDisplayDevice.DeviceIdentifiersMatch(
                interfacePath,
                @"PCI\VEN_1002&DEV_744C&SUBSYS_0E3A1002&REV_C8\6&87654321&0&00000019"));
        }

        [TestMethod]
        public void AdlxDeviceInfo_ManagedLayoutMatchesNativeAbi()
        {
            Assert.AreEqual(596, Marshal.SizeOf<ADLX.AdlxDeviceInfo>());
            Assert.AreEqual(328, Marshal.OffsetOf<ADLX.AdlxDeviceInfo>(nameof(ADLX.AdlxDeviceInfo.PnpString)).ToInt32());
            Assert.AreEqual(584, Marshal.OffsetOf<ADLX.AdlxDeviceInfo>(nameof(ADLX.AdlxDeviceInfo.LuidLowPart)).ToInt32());
            Assert.AreEqual(588, Marshal.OffsetOf<ADLX.AdlxDeviceInfo>(nameof(ADLX.AdlxDeviceInfo.LuidHighPart)).ToInt32());
            Assert.AreEqual(592, Marshal.OffsetOf<ADLX.AdlxDeviceInfo>(nameof(ADLX.AdlxDeviceInfo.LuidValid)).ToInt32());
        }

        private static void AssertAllocatedSensor(IHardware gpu, string expectedIdentifier)
        {
            ISensor sensor = gpu.Sensors.Single(item => item.Name == "GPU Memory Allocated");

            Assert.AreEqual(SensorType.Data, sensor.SensorType);
            Assert.AreEqual(expectedIdentifier, sensor.Identifier.ToString());
        }

        private sealed class TestWddmGpu : GenericGpu
        {
            public TestWddmGpu(ISettings settings)
                : base("Test WDDM GPU", new Identifier("gpu-wddm-test", "0"), settings, enableProcessMemorySensors: false)
            {
                InitializeWddmDevice(
                    @"\\?\PCI#VEN_1234&DEV_5678#0#{1ca05180-a699-450a-9a0c-de4fbe3ddd89}",
                    "luid_0x00000000_0x00000001",
                    5,
                    "0_0");
            }

            public override string DeviceId => WddmDeviceId;

            public override HardwareType HardwareType => HardwareType.GpuNvidia;

            public void ApplyWddmMemory(D3DDisplayDevice.D3DDeviceInfo deviceInfo)
            {
                UpdateWddmMemorySensor(deviceInfo);
            }

            public override void Update()
            {
            }
        }

        private sealed class TestSettings : ISettings
        {
            private readonly Dictionary<string, string> _values = new Dictionary<string, string>();

            public bool Contains(string name)
            {
                return _values.ContainsKey(name);
            }

            public string GetValue(string name, string value)
            {
                return _values.TryGetValue(name, out string storedValue) ? storedValue : value;
            }

            public void Remove(string name)
            {
                _values.Remove(name);
            }

            public void SetValue(string name, string value)
            {
                _values[name] = value;
            }
        }
    }
}
