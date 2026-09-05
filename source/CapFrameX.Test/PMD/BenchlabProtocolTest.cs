using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.ExceptionServices;
using CapFrameX.PMD.Benchlab;
using Microsoft.VisualStudio.TestTools.UnitTesting;
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

        [TestMethod]
        public void TryGetPowerSensorIndices_ReturnsFalseWhenRequiredSensorIsMissing()
        {
            var sensors = new List<BenchlabSensor>
            {
                new BenchlabSensor(0, "CPU_P", "CPU Power", SensorType.Power),
                new BenchlabSensor(1, "GPU_P", "GPU Power", SensorType.Power),
                new BenchlabSensor(2, "SYS_P", "System Power", SensorType.Power)
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
    }
}
