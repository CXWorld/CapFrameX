using System.Collections.Generic;
using CapFrameX.Monitoring.Contracts;
using LibreHardwareMonitor.Hardware;
using LibreHardwareMonitor.Hardware.Cpu;
using LibreHardwareMonitor.Hardware.Memory.Sensors;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using RAMSPDToolkit.SPD.Interfaces;

namespace CapFrameX.Test.Sensor
{
    [TestClass]
    public class SensorPollingSelectionTest
    {
        [TestMethod]
        public void ShouldEvaluateAnyEvaluatesEveryIdentifierWithoutShortCircuiting()
        {
            var evaluatedIdentifiers = new List<string>();
            var sensorConfig = new Mock<ISensorConfig>();
            var hardware = new TestHardware();
            var firstSensor = new LibreHardwareMonitor.Hardware.Sensor(
                "First",
                0,
                SensorType.Clock,
                hardware,
                new TestSettings());
            var secondSensor = new LibreHardwareMonitor.Hardware.Sensor(
                "Second",
                1,
                SensorType.Clock,
                hardware,
                new TestSettings());

            sensorConfig
                .Setup(config => config.GetSensorEvaluate(It.IsAny<string>()))
                .Returns((string identifier) =>
                {
                    evaluatedIdentifiers.Add(identifier);
                    return identifier == firstSensor.Identifier.ToString();
                });

            bool evaluate = SensorPolling.ShouldEvaluateAny(
                sensorConfig.Object,
                new ISensor[] { firstSensor, secondSensor });

            Assert.IsTrue(evaluate);
            CollectionAssert.AreEqual(
                new[]
                {
                    firstSensor.Identifier.ToString(),
                    secondSensor.Identifier.ToString()
                },
                evaluatedIdentifiers);
        }

        [TestMethod]
        public void SmuRawDumpKeepsPollingAndStillEvaluatesEveryIdentifier()
        {
            var evaluatedIdentifiers = new List<string>();
            var sensorConfig = new Mock<ISensorConfig>();
            var hardware = new TestHardware();
            var sensors = new ISensor[]
            {
                new LibreHardwareMonitor.Hardware.Sensor("TDC", 0, SensorType.Current, hardware, new TestSettings()),
                new LibreHardwareMonitor.Hardware.Sensor("EDC", 1, SensorType.Current, hardware, new TestSettings())
            };

            sensorConfig
                .Setup(config => config.GetSensorEvaluate(It.IsAny<string>()))
                .Returns((string identifier) =>
                {
                    evaluatedIdentifiers.Add(identifier);
                    return false;
                });

            bool shouldPoll = Amd17Cpu.ShouldPollSmuTable(
                sensorConfig.Object,
                sensors,
                isRawDumpCaptureActive: true);

            Assert.IsTrue(shouldPoll);
            Assert.AreEqual(sensors.Length, evaluatedIdentifiers.Count);
        }

        [TestMethod]
        public void SpdThermalSensorPollsOnlyWhenSelected()
        {
            bool selected = false;
            var sensorConfig = new Mock<ISensorConfig>();
            sensorConfig
                .Setup(config => config.GetSensorEvaluate(It.IsAny<string>()))
                .Returns(() => selected);

            var thermalSensor = new Mock<IThermalSensor>();
            thermalSensor.Setup(sensor => sensor.UpdateTemperature()).Returns(true);
            thermalSensor.SetupGet(sensor => sensor.Temperature).Returns(42.5f);

            var sensor = new SpdThermalSensor(
                "DIMM #0",
                0,
                SensorType.Temperature,
                new TestHardware(),
                new TestSettings(),
                thermalSensor.Object,
                sensorConfig.Object);

            Assert.IsFalse(sensor.UpdateSensor());
            thermalSensor.Verify(item => item.UpdateTemperature(), Times.Never);

            selected = true;

            Assert.IsTrue(sensor.UpdateSensor());
            thermalSensor.Verify(item => item.UpdateTemperature(), Times.Once);
            Assert.AreEqual(42.5f, sensor.Value);
        }

        private sealed class TestHardware : Hardware
        {
            public TestHardware()
                : base("Polling Test", new Identifier("polling-test"), new TestSettings())
            { }

            public override HardwareType HardwareType => HardwareType.Cpu;

            public override void Update()
            { }
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
