using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.ExceptionServices;
using CapFrameX.PMD.Benchlab;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using BenchlabSensor = CapFrameX.PMD.Benchlab.Sensor;

namespace CapFrameX.Test.PMD
{
    [TestClass]
    public class BenchlabProtocolTest
    {
        [TestMethod]
        public void SelectDevice_PrefersPreviouslySelectedConnectedDevice()
        {
            const string json = @"[
                {
                    ""deviceName"": ""Disconnected device"",
                    ""guid"": ""DEVICE-1"",
                    ""status"": ""DISCONNECTED"",
                    ""pipeName"": ""BenchlabSensorPipe_10_1000""
                },
                {
                    ""deviceName"": ""First connected device"",
                    ""guid"": ""DEVICE-2"",
                    ""status"": ""CONNECTED"",
                    ""pipeName"": ""BenchlabSensorPipe_10_2000""
                },
                {
                    ""deviceName"": ""Selected device"",
                    ""guid"": ""DEVICE-3"",
                    ""status"": ""CONNECTED"",
                    ""pipeName"": ""BenchlabSensorPipe_11_3000""
                }
            ]";

            var devices = BenchlabProtocol.DeserializeDevices(json);
            var firstConnectedDevice = BenchlabProtocol.SelectDevice(devices);
            var device = BenchlabProtocol.SelectDevice(devices, "device-3");

            Assert.IsNotNull(firstConnectedDevice);
            Assert.AreEqual("DEVICE-2", firstConnectedDevice.DeviceId);
            Assert.IsNotNull(device);
            Assert.AreEqual("BenchlabSensorPipe_11_3000", device.PipeName);
        }

        [TestMethod]
        public void DeserializeSensors_ReadsTelemetryEnvelopeAndPowerSensorIndices()
        {
            var json = "\uFEFF" + @"{
                ""status"": ""CONNECTED"",
                ""sensorsUpdated"": true,
                ""sensors"": [
                    { ""id"": 10, ""shortName"": ""SYS_P"", ""name"": ""System Power"", ""type"": 6, ""value"": 500.0, ""isValid"": true },
                    { ""id"": 11, ""shortName"": ""CPU_P"", ""name"": ""CPU Power"", ""type"": 6, ""value"": 125.0, ""isValid"": true },
                    { ""id"": 12, ""shortName"": ""GPU_P"", ""name"": ""GPU Power"", ""type"": 6, ""value"": 300.0, ""isValid"": true },
                    { ""id"": 13, ""shortName"": ""MB_P"", ""name"": ""Motherboard Power"", ""type"": 6, ""value"": 75.0, ""isValid"": true }
                ]
            }";

            var sensors = BenchlabProtocol.DeserializeSensors(json);
            var foundPowerSensors = BenchlabProtocol.TryGetPowerSensorIndices(
                sensors,
                out var cpuPowerSensorIndex,
                out var gpuPowerSensorIndex,
                out var mainboardPowerSensorIndex,
                out var systemPowerSensorIndex);

            Assert.IsTrue(foundPowerSensors);
            Assert.AreEqual(1, cpuPowerSensorIndex);
            Assert.AreEqual(2, gpuPowerSensorIndex);
            Assert.AreEqual(3, mainboardPowerSensorIndex);
            Assert.AreEqual(0, systemPowerSensorIndex);
            Assert.AreEqual(300.0, sensors[gpuPowerSensorIndex].Value);
            Assert.IsTrue(sensors[gpuPowerSensorIndex].IsValid);
            Assert.AreEqual(SensorType.Power, sensors[gpuPowerSensorIndex].Type);
        }

        [TestMethod]
        public void DeserializeSensors_RejectsUnsuccessfulRefresh()
        {
            const string json = @"{
                ""status"": ""CONNECTED"",
                ""sensorsUpdated"": false,
                ""sensors"": []
            }";

            Assert.ThrowsException<InvalidDataException>(() => BenchlabProtocol.DeserializeSensors(json));
        }

        [DataTestMethod]
        [DataRow("null", false)]
        [DataRow("null", true)]
        [DataRow(null, true)]
        [DataRow("\"NaN\"", true)]
        [DataRow("\"Infinity\"", true)]
        [DataRow("\"-Infinity\"", true)]
        [DataRow("-1.7976931348623157E+308", false)]
        [DataRow("-1.7976931348623157E+308", true)]
        [DataRow("24.5", false)]
        public void DeserializeSensors_UnavailableOptionalReadingPreservesPowerValuesAndIndices(
            string valueJson, bool isValid)
        {
            var sensors = BenchlabProtocol.DeserializeSensors(CreateTelemetryJson("TS1", valueJson, isValid));

            Assert.AreEqual(6, sensors.Count);
            Assert.IsNull(sensors[0]);
            Assert.AreEqual("TS1", sensors[1].ShortName);
            Assert.IsFalse(sensors[1].IsValid);
            Assert.IsTrue(double.IsNaN(sensors[1].Value));

            Assert.IsTrue(BenchlabProtocol.TryGetPowerSensorIndices(
                sensors, out var cpu, out var gpu, out var mainboard, out var system));
            Assert.AreEqual(2, cpu);
            Assert.AreEqual(3, gpu);
            Assert.AreEqual(4, mainboard);
            Assert.AreEqual(5, system);
            Assert.AreEqual(125.0, sensors[cpu].Value);
            Assert.AreEqual(300.0, sensors[gpu].Value);
            Assert.AreEqual(75.0, sensors[mainboard].Value);
            Assert.AreEqual(500.0, sensors[system].Value);
        }

        [DataTestMethod]
        [DataRow("CPU_P", "null", true)]
        [DataRow("GPU_P", "null", true)]
        [DataRow("MB_P", "null", true)]
        [DataRow("SYS_P", "null", true)]
        [DataRow("CPU_P", "null", false)]
        [DataRow("CPU_P", null, true)]
        [DataRow("GPU_P", "\"NaN\"", true)]
        [DataRow("MB_P", "\"Infinity\"", true)]
        [DataRow("SYS_P", "\"-Infinity\"", true)]
        [DataRow("CPU_P", "125.0", false)]
        [DataRow("CPU_P", "-1.7976931348623157E+308", true)]
        public void TryGetPowerSensorIndices_RejectsUnavailableRequiredReading(
            string shortName, string valueJson, bool isValid)
        {
            var sensors = BenchlabProtocol.DeserializeSensors(CreateTelemetryJson(shortName, valueJson, isValid));

            Assert.IsFalse(BenchlabProtocol.TryGetPowerSensorIndices(sensors, out _, out _, out _, out _));
        }

        [TestMethod]
        public void DeserializeSensors_PreservesValidZeroPower()
        {
            var sensors = BenchlabProtocol.DeserializeSensors(CreateTelemetryJson("CPU_P", "0.0", true));

            Assert.IsTrue(BenchlabProtocol.TryGetPowerSensorIndices(sensors, out var cpu, out _, out _, out _));
            Assert.AreEqual(0.0, sensors[cpu].Value);
            Assert.IsTrue(sensors[cpu].IsValid);
        }

        [TestMethod]
        public void TryGetPowerSensorIndices_ReturnsFalseWhenRequiredSensorIsMissing()
        {
            var sensors = new List<BenchlabSensor>
            {
                new BenchlabSensor(0, "CPU_P", "CPU Power", SensorType.Power) { Value = 125, IsValid = true },
                new BenchlabSensor(1, "GPU_P", "GPU Power", SensorType.Power) { Value = 300, IsValid = true },
                new BenchlabSensor(2, "SYS_P", "System Power", SensorType.Power) { Value = 500, IsValid = true }
            };

            var foundPowerSensors = BenchlabProtocol.TryGetPowerSensorIndices(
                sensors,
                out _,
                out _,
                out _,
                out _);

            Assert.IsFalse(foundPowerSensors);
        }

        [DataTestMethod]
        [DataRow("\"C:\\Services\\Benchlab_Service\\PMD_Service.exe\"")]
        [DataRow("C:\\Services\\PMD_Service.exe --service")]
        [DataRow("C:\\Services\\pmd_service.EXE")]
        public void IsLegacyServiceImagePath_RecognizesFormerServiceExecutable(string imagePath)
        {
            Assert.IsTrue(BenchlabService.IsLegacyServiceImagePath(imagePath));
        }

        [DataTestMethod]
        [DataRow(null)]
        [DataRow("")]
        [DataRow("\"C:\\Services\\Benchlab_Service\\BL_Service.exe\"")]
        [DataRow("C:\\Services\\PMD_Service.exe.backup")]
        public void IsLegacyServiceImagePath_RejectsCurrentOrInvalidExecutable(string imagePath)
        {
            Assert.IsFalse(BenchlabService.IsLegacyServiceImagePath(imagePath));
        }

        [DataTestMethod]
        [DataRow(2, true)]
        [DataRow(3, false)]
        [DataRow(4, false)]
        public void ShouldConfigureDemandStart_OnlyMigratesAutomaticServices(int startType, bool expected)
        {
            Assert.AreEqual(expected, BenchlabService.ShouldConfigureDemandStart(startType));
        }

        [TestMethod]
        public void ShouldConfigureDemandStart_DoesNotMigrateMissingService()
        {
            Assert.IsFalse(BenchlabService.ShouldConfigureDemandStart(null));
        }

        [TestMethod]
        public void IsWindowsServiceRunning_DoesNotQueryStatusForMissingService()
        {
            var serviceControllerExceptions = 0;
            EventHandler<FirstChanceExceptionEventArgs> handler = (_, args) =>
            {
                if (args.Exception.Source == "System.ServiceProcess.ServiceController")
                {
                    serviceControllerExceptions++;
                }
            };

            AppDomain.CurrentDomain.FirstChanceException += handler;
            try
            {
                Assert.IsFalse(BenchlabService.IsWindowsServiceRunning(
                    $"CapFrameX.Test.Missing.{Guid.NewGuid():N}"));
            }
            finally
            {
                AppDomain.CurrentDomain.FirstChanceException -= handler;
            }

            Assert.AreEqual(0, serviceControllerExceptions);
        }

        private static string CreateTelemetryJson(string shortName, string valueJson, bool isValid)
        {
            // Include an empty row and an optional sensor before the power readings to
            // catch filtering that would change the indices used by charts and captures.
            var response = JObject.Parse(@"{
                ""status"": ""CONNECTED"",
                ""sensorsUpdated"": true,
                ""sensors"": [
                    null,
                    { ""Id"": 16, ""ShortName"": ""TS1"", ""Name"": ""Temperature Sensor #1"", ""Type"": 0, ""Value"": 24.5, ""IsValid"": true },
                    { ""Id"": 23, ""ShortName"": ""CPU_P"", ""Name"": ""CPU Power"", ""Type"": 6, ""Value"": 125.0, ""IsValid"": true },
                    { ""Id"": 24, ""ShortName"": ""GPU_P"", ""Name"": ""GPU Power"", ""Type"": 6, ""Value"": 300.0, ""IsValid"": true },
                    { ""Id"": 25, ""ShortName"": ""MB_P"", ""Name"": ""Motherboard Power"", ""Type"": 6, ""Value"": 75.0, ""IsValid"": true },
                    { ""Id"": 22, ""ShortName"": ""SYS_P"", ""Name"": ""System Power"", ""Type"": 6, ""Value"": 500.0, ""IsValid"": true }
                ]
            }");

            foreach (var sensor in response["sensors"].Children<JObject>())
            {
                if ((string)sensor["ShortName"] != shortName)
                {
                    continue;
                }

                if (valueJson == null)
                {
                    sensor.Remove("Value");
                }
                else
                {
                    sensor["Value"] = JToken.Parse(valueJson);
                }

                sensor["IsValid"] = isValid;
            }

            return response.ToString();
        }
    }
}
