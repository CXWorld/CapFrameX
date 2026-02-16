using CapFrameX.Contracts.Overlay;
using CapFrameX.Contracts.Sensor;
using CapFrameX.Sensor;
using CapFrameX.Test.Mocks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;

namespace CapFrameX.Test.Sensor
{
    [TestClass]
    public class StableIdMigrationTest
    {
        #region GetSensorTypeString

        [TestMethod]
        public void GetSensorTypeString_CpuLoad_ReturnsCorrect()
        {
            var service = new MockSensorService();
            var result = service.GetSensorTypeString(
                EOverlayEntryType.CPU,
                "AMD Ryzen 9 7950X/load/CPU Total");
            Assert.AreEqual("CPU Load", result);
        }

        [TestMethod]
        public void GetSensorTypeString_CpuTemperature_ReturnsCorrect()
        {
            var service = new MockSensorService();
            var result = service.GetSensorTypeString(
                EOverlayEntryType.CPU,
                "AMD Ryzen 9 7950X/temperature/CPU Package");
            Assert.AreEqual("CPU Temperature", result);
        }

        [TestMethod]
        public void GetSensorTypeString_CpuClock_ReturnsCorrect()
        {
            var service = new MockSensorService();
            var result = service.GetSensorTypeString(
                EOverlayEntryType.CPU,
                "AMD Ryzen 9 7950X/clock/Core #1");
            Assert.AreEqual("CPU Clock", result);
        }

        [TestMethod]
        public void GetSensorTypeString_CpuPower_ReturnsCorrect()
        {
            var service = new MockSensorService();
            var result = service.GetSensorTypeString(
                EOverlayEntryType.CPU,
                "AMD Ryzen 9 7950X/power/CPU Package");
            Assert.AreEqual("CPU Power", result);
        }

        [TestMethod]
        public void GetSensorTypeString_CpuVoltage_ReturnsCorrect()
        {
            var service = new MockSensorService();
            var result = service.GetSensorTypeString(
                EOverlayEntryType.CPU,
                "AMD Ryzen 9 7950X/voltage/Core #1 VID");
            Assert.AreEqual("CPU Voltage", result);
        }

        [TestMethod]
        public void GetSensorTypeString_GpuLoad_ReturnsCorrect()
        {
            var service = new MockSensorService();
            var result = service.GetSensorTypeString(
                EOverlayEntryType.GPU,
                "NVIDIA GeForce RTX 4090/load/GPU Core");
            Assert.AreEqual("GPU Load", result);
        }

        [TestMethod]
        public void GetSensorTypeString_GpuTemperature_ReturnsCorrect()
        {
            var service = new MockSensorService();
            var result = service.GetSensorTypeString(
                EOverlayEntryType.GPU,
                "NVIDIA GeForce RTX 4090/temperature/GPU Core");
            Assert.AreEqual("GPU Temperature", result);
        }

        [TestMethod]
        public void GetSensorTypeString_GpuClock_ReturnsCorrect()
        {
            var service = new MockSensorService();
            var result = service.GetSensorTypeString(
                EOverlayEntryType.GPU,
                "NVIDIA GeForce RTX 4090/clock/GPU Core");
            Assert.AreEqual("GPU Clock", result);
        }

        [TestMethod]
        public void GetSensorTypeString_GpuPower_ReturnsCorrect()
        {
            var service = new MockSensorService();
            var result = service.GetSensorTypeString(
                EOverlayEntryType.GPU,
                "NVIDIA GeForce RTX 4090/power/GPU Power");
            Assert.AreEqual("GPU Power", result);
        }

        [TestMethod]
        public void GetSensorTypeString_GpuVoltage_ReturnsCorrect()
        {
            var service = new MockSensorService();
            var result = service.GetSensorTypeString(
                EOverlayEntryType.GPU,
                "NVIDIA GeForce RTX 4090/voltage/GPU Core");
            Assert.AreEqual("GPU Voltage", result);
        }

        [TestMethod]
        public void GetSensorTypeString_GpuFactor_ReturnsGpuLimits()
        {
            var service = new MockSensorService();
            var result = service.GetSensorTypeString(
                EOverlayEntryType.GPU,
                "NVIDIA GeForce RTX 4090/factor/GPU Power Limit");
            Assert.AreEqual("GPU Limits", result);
        }

        [TestMethod]
        public void GetSensorTypeString_NullStableId_ReturnsEmpty()
        {
            var service = new MockSensorService();
            var result = service.GetSensorTypeString(EOverlayEntryType.CPU, null);
            Assert.AreEqual(string.Empty, result);
        }

        [TestMethod]
        public void GetSensorTypeString_RamType_ReturnsEmpty()
        {
            var service = new MockSensorService();
            var result = service.GetSensorTypeString(
                EOverlayEntryType.RAM,
                "Generic Memory/data/Used Memory");
            Assert.AreEqual(string.Empty, result);
        }

        [TestMethod]
        public void GetSensorTypeString_CXType_ReturnsEmpty()
        {
            var service = new MockSensorService();
            var result = service.GetSensorTypeString(EOverlayEntryType.CX, null);
            Assert.AreEqual(string.Empty, result);
        }

        [TestMethod]
        public void GetSensorTypeString_UnknownSubtype_ReturnsEmpty()
        {
            var service = new MockSensorService();
            var result = service.GetSensorTypeString(
                EOverlayEntryType.GPU,
                "NVIDIA GeForce RTX 4090/unknowntype/Something");
            Assert.AreEqual(string.Empty, result);
        }

        #endregion

        #region MockSensorService GetSensorTypeString

        [TestMethod]
        public void MockGetSensorTypeString_GpuTemperature_ReturnsCorrect()
        {
            var mock = new MockSensorService();
            var result = mock.GetSensorTypeString(
                EOverlayEntryType.GPU,
                "NVIDIA GeForce RTX 4090/temperature/GPU Core");
            Assert.AreEqual("GPU Temperature", result);
        }

        [TestMethod]
        public void MockGetSensorTypeString_CpuLoad_ReturnsCorrect()
        {
            var mock = new MockSensorService();
            var result = mock.GetSensorTypeString(
                EOverlayEntryType.CPU,
                "AMD Ryzen 9 7950X/load/CPU Total");
            Assert.AreEqual("CPU Load", result);
        }

        [TestMethod]
        public void MockGetSensorTypeString_NullStableId_ReturnsEmpty()
        {
            var mock = new MockSensorService();
            var result = mock.GetSensorTypeString(EOverlayEntryType.GPU, null);
            Assert.AreEqual(string.Empty, result);
        }

        #endregion

        #region SensorIdentifierHelper - StableIdentifier parsing

        [TestMethod]
        public void StableIdentifier_ContainsSlashSensorType_ForLoadMatching()
        {
            var stableId = "AMD Ryzen 9 7950X/load/CPU Total";
            Assert.IsTrue(stableId.Contains("/load/"));
            Assert.IsFalse(stableId.Contains("/temperature/"));
        }

        [TestMethod]
        public void StableIdentifier_ContainsSlashSensorType_ForTemperatureMatching()
        {
            var stableId = "NVIDIA GeForce RTX 4090/temperature/GPU Core";
            Assert.IsTrue(stableId.Contains("/temperature/"));
            Assert.IsFalse(stableId.Contains("/load/"));
        }

        [TestMethod]
        public void StableIdentifier_ContainsSlashSensorType_ForClockMatching()
        {
            var stableId = "Intel Core i9-13900K/clock/Core #1";
            Assert.IsTrue(stableId.Contains("/clock/"));
        }

        [TestMethod]
        public void StableIdentifier_ContainsSlashSensorType_ForPowerMatching()
        {
            var stableId = "AMD Ryzen 9 7950X/power/CPU Package";
            Assert.IsTrue(stableId.Contains("/power/"));
        }

        [TestMethod]
        public void StableIdentifier_ContainsSlashSensorType_ForVoltageMatching()
        {
            var stableId = "Intel Core i9-13900K/voltage/Core #1 VID";
            Assert.IsTrue(stableId.Contains("/voltage/"));
        }

        [TestMethod]
        public void StableIdentifier_SlashDelimiters_PreventFalsePositives()
        {
            // "GPU Power Upload" sensor name should NOT match "/load/"
            var stableId = "NVIDIA GeForce RTX 4090/data/GPU Power Upload";
            Assert.IsFalse(stableId.Contains("/load/"));

            // "clock" in hardware name should NOT match "/clock/" sensor type
            var stableId2 = "Overclock CPU/temperature/CPU Package";
            Assert.IsFalse(stableId2.Contains("/clock/"));
            Assert.IsTrue(stableId2.Contains("/temperature/"));
        }

        #endregion

        #region SessionSensorDataLive StableIdentifier population

        [TestMethod]
        public void SessionSensorDataLive_AddSensorValue_PopulatesStableIdentifier()
        {
            var live = new SessionSensorDataLive();
            var sensor = new SensorEntry
            {
                Identifier = "/gpu-nvidia/0/temperature/0",
                Name = "GPU Core",
                SensorType = "Temperature",
                HardwareName = "NVIDIA GeForce RTX 4090"
            };

            live.AddSensorValue(sensor, 65.0f);
            var data = live.ToSessionSensorData();

            Assert.IsTrue(data.ContainsKey("/gpu-nvidia/0/temperature/0"));
            var entry = data["/gpu-nvidia/0/temperature/0"];
            Assert.AreEqual("NVIDIA GeForce RTX 4090/temperature/GPU Core", entry.StableIdentifier);
        }

        [TestMethod]
        public void SessionSensorDataLive_AddSensorValue_NullHardwareName_StableIdentifierIsNull()
        {
            var live = new SessionSensorDataLive();
            var sensor = new SensorEntry
            {
                Identifier = "Framerate",
                Name = "Framerate",
                SensorType = "CX",
                HardwareName = null
            };

            live.AddSensorValue(sensor, 60.0f);
            var data = live.ToSessionSensorData();

            var entry = data["Framerate"];
            Assert.IsNull(entry.StableIdentifier);
        }

        #endregion
    }
}
