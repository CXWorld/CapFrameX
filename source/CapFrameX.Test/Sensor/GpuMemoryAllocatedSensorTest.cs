using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

using CapFrameX.Data;
using CapFrameX.Monitoring.Contracts;
using LibreHardwareMonitor.Hardware;
using LibreHardwareMonitor.Hardware.Gpu;
using LibreHardwareMonitor.Hardware.Simulation;
using LibreHardwareMonitor.Interop;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

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

        [DataTestMethod]
        [DataRow("0_8_0", "0_8_0_1", "0_8_1")]
        [DataRow("0_6_0", "0_6_0_1", "0_6_1")]
        [DataRow("0_1", "0_1_1", "0_2_0")]
        public void ProcessMemorySortKeys_FollowGlobalMemorySensors(
            string dedicatedGlobalSortKey,
            string allocatedGlobalSortKey,
            string sharedGlobalSortKey)
        {
            string dedicatedGameSortKey = GenericGpu.GetProcessMemoryPresentationSortKey(
                dedicatedGlobalSortKey,
                2,
                "99_0");
            string sharedGameSortKey = GenericGpu.GetProcessMemoryPresentationSortKey(
                sharedGlobalSortKey,
                1,
                "99_1");

            var sortedKeys = new[]
                {
                    sharedGameSortKey,
                    dedicatedGameSortKey,
                    sharedGlobalSortKey,
                    allocatedGlobalSortKey,
                    dedicatedGlobalSortKey
                }
                .OrderBy(key => key, new SortKeyComparer())
                .ToArray();

            CollectionAssert.AreEqual(
                new[]
                {
                    dedicatedGlobalSortKey,
                    allocatedGlobalSortKey,
                    dedicatedGameSortKey,
                    sharedGlobalSortKey,
                    sharedGameSortKey
                },
                sortedKeys);
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
        public void GenericGpu_AllocatedMemorySensorParticipatesInEvaluateSelection()
        {
            var evaluatedIdentifiers = new List<string>();
            var sensorConfig = new Mock<ISensorConfig>();
            sensorConfig
                .Setup(config => config.GetSensorEvaluate(It.IsAny<string>()))
                .Returns((string identifier) =>
                {
                    evaluatedIdentifiers.Add(identifier);
                    return false;
                });

            var gpu = new TestWddmGpu(new TestSettings(), sensorConfig.Object);

            try
            {
                bool updated = gpu.TryUpdateWddmMemorySensors(false, out _);

                Assert.IsFalse(updated);
                CollectionAssert.AreEqual(
                    new[] { "/gpu-wddm-test/0/data/5" },
                    evaluatedIdentifiers);
            }
            finally
            {
                gpu.Close();
            }
        }

        [TestMethod]
        public void GenericGpu_ProcessMemorySensorsEvaluateBothIdentifiersWithoutShortCircuiting()
        {
            var evaluatedIdentifiers = new List<string>();
            var sensorConfig = new Mock<ISensorConfig>();
            var gpu = new TestWddmGpu(new TestSettings());
            var dedicatedSensor = new LibreHardwareMonitor.Hardware.Sensor(
                "GPU Memory Dedicated Game",
                90,
                SensorType.Data,
                gpu,
                new TestSettings());
            var sharedSensor = new LibreHardwareMonitor.Hardware.Sensor(
                "GPU Memory Shared Game",
                91,
                SensorType.Data,
                gpu,
                new TestSettings());

            sensorConfig
                .Setup(config => config.GetSensorEvaluate(It.IsAny<string>()))
                .Returns((string identifier) =>
                {
                    evaluatedIdentifiers.Add(identifier);
                    return identifier == dedicatedSensor.Identifier.ToString();
                });

            try
            {
                (bool evaluateDedicated, bool evaluateShared) = GenericGpu.EvaluateSensorPair(
                    sensorConfig.Object,
                    dedicatedSensor,
                    sharedSensor);

                Assert.IsTrue(evaluateDedicated);
                Assert.IsFalse(evaluateShared);
                CollectionAssert.AreEqual(
                    new[]
                    {
                        dedicatedSensor.Identifier.ToString(),
                        sharedSensor.Identifier.ToString()
                    },
                    evaluatedIdentifiers);
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

        [TestMethod]
        public void AdlxTelemetryLayouts_MatchNativeAbi()
        {
            Assert.AreEqual(16, Marshal.SizeOf<ADLX.AdlxTelemetrySupport>());
            Assert.AreEqual(256, Marshal.SizeOf<ADLX.AdlxTelemetryData>());
        }

        private static void AssertAllocatedSensor(IHardware gpu, string expectedIdentifier)
        {
            ISensor sensor = gpu.Sensors.Single(item => item.Name == "GPU Memory Allocated");

            Assert.AreEqual(SensorType.Data, sensor.SensorType);
            Assert.AreEqual(expectedIdentifier, sensor.Identifier.ToString());
        }

        private sealed class TestWddmGpu : GenericGpu
        {
            public TestWddmGpu(ISettings settings, ISensorConfig sensorConfig = null)
                : base(
                    "Test WDDM GPU",
                    new Identifier("gpu-wddm-test", "0"),
                    settings,
                    enableProcessMemorySensors: false,
                    sensorConfig: sensorConfig)
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
