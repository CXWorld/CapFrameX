using System;
using System.Collections.Generic;
using CapFrameX.Monitoring.Contracts;
using LibreHardwareMonitor.Hardware;
using LibreHardwareMonitor.Hardware.Cpu;
using LibreHardwareMonitor.Hardware.Memory.Sensors;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

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
            int reads = 0;
            var sensor = CreateSpdThermalSensor(
                () => selected,
                () => { reads++; return 42.5f; },
                new RefreshThrottle(TimeSpan.FromSeconds(2), clockMilliseconds: () => 0));

            Assert.IsFalse(sensor.UpdateSensor());
            Assert.AreEqual(0, reads);

            selected = true;

            Assert.IsTrue(sensor.UpdateSensor());
            Assert.AreEqual(1, reads);
            Assert.AreEqual(42.5f, sensor.Value);
        }

        [TestMethod]
        public void SpdThermalSensorKeepsItsValueBetweenThrottledReads()
        {
            long now = 0;
            float temperature = 40f;
            int reads = 0;
            var sensor = CreateSpdThermalSensor(
                () => true,
                () => { reads++; return temperature; },
                new RefreshThrottle(TimeSpan.FromSeconds(2), clockMilliseconds: () => now));

            Assert.IsTrue(sensor.UpdateSensor());

            temperature = 41f;
            for (now = 250; now < 2000; now += 250)
                Assert.IsFalse(sensor.UpdateSensor());

            Assert.AreEqual(1, reads);
            Assert.AreEqual(40f, sensor.Value);

            Assert.IsTrue(sensor.UpdateSensor());
            Assert.AreEqual(2, reads);
            Assert.AreEqual(41f, sensor.Value);
        }

        [TestMethod]
        public void SpdThermalSensorDoesNotUseUpItsThrottleWhileUnselected()
        {
            long now = 0;
            bool selected = true;
            int reads = 0;
            var sensor = CreateSpdThermalSensor(
                () => selected,
                () => { reads++; return 40f; },
                new RefreshThrottle(TimeSpan.FromSeconds(2), clockMilliseconds: () => now));

            Assert.IsTrue(sensor.UpdateSensor());

            selected = false;
            for (now = 250; now <= 5000; now += 250)
                Assert.IsFalse(sensor.UpdateSensor());

            // Selected again long after the last read: it reads in the very next poll.
            selected = true;
            now = 5250;
            Assert.IsTrue(sensor.UpdateSensor());
            Assert.AreEqual(2, reads);
        }

        [TestMethod]
        public void SpdThermalSensorRetriesAFailedReadOnlyAfterTheInterval()
        {
            long now = 0;
            float? temperature = null;
            int reads = 0;
            var sensor = CreateSpdThermalSensor(
                () => true,
                () => { reads++; return temperature; },
                new RefreshThrottle(TimeSpan.FromSeconds(2), clockMilliseconds: () => now));

            Assert.IsFalse(sensor.UpdateSensor());
            Assert.IsNull(sensor.Value);

            temperature = 40f;
            for (now = 250; now < 2000; now += 250)
                Assert.IsFalse(sensor.UpdateSensor());

            Assert.AreEqual(1, reads);

            Assert.IsTrue(sensor.UpdateSensor());
            Assert.AreEqual(2, reads);
            Assert.AreEqual(40f, sensor.Value);
        }

        private static SpdThermalSensor CreateSpdThermalSensor(Func<bool> selected, Func<float?> readTemperature, RefreshThrottle throttle)
        {
            var sensorConfig = new Mock<ISensorConfig>();
            sensorConfig
                .Setup(config => config.GetSensorEvaluate(It.IsAny<string>()))
                .Returns(() => selected());

            return new SpdThermalSensor(
                "DIMM #0",
                0,
                SensorType.Temperature,
                new TestHardware(),
                new TestSettings(),
                readTemperature,
                throttle,
                sensorConfig.Object);
        }

        private sealed class TestHardware : LibreHardwareMonitor.Hardware.Hardware
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
