using CapFrameX.Monitoring.Contracts;
using LibreHardwareMonitor.Hardware;
using LibreHardwareMonitor.Hardware.Storage;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System.Collections.Generic;
using System.Text;

namespace CapFrameX.Test.Sensor
{
    [TestClass]
    public class StoragePollingSelectionTest
    {
        [TestMethod]
        public void UpdateSensorsRunsOnlyForSelectedSensorsAndEvaluatesEveryIdentifier()
        {
            var evaluatedIdentifiers = new List<string>();
            var selectedIdentifiers = new HashSet<string>();
            var sensorConfig = new Mock<ISensorConfig>();
            sensorConfig
                .Setup(config => config.GetSensorEvaluate(It.IsAny<string>()))
                .Returns((string identifier) =>
                {
                    evaluatedIdentifiers.Add(identifier);
                    return selectedIdentifiers.Contains(identifier);
                });

            var storage = new TestStorage();
            storage.SetSensorConfig(sensorConfig.Object);

            storage.Update();

            Assert.AreEqual(0, storage.DeviceUpdateCount, "Unselected storage sensors still triggered a device poll.");
            Assert.AreEqual(2, evaluatedIdentifiers.Count, "Not every storage sensor was evaluated in the same tick.");

            evaluatedIdentifiers.Clear();
            selectedIdentifiers.Add(storage.Sensors[0].Identifier.ToString());

            storage.Update();

            Assert.AreEqual(1, storage.DeviceUpdateCount, "A selected storage sensor did not trigger its device poll.");
            Assert.AreEqual(2, evaluatedIdentifiers.Count, "Selection short-circuited before every identifier was registered.");
        }

        private sealed class TestStorage : AbstractStorage
        {
            public TestStorage()
                : base(null, "Test NVMe", "1.0", "nvme", 99, new TestSettings())
            {
                ActivateSensor(new LibreHardwareMonitor.Hardware.Sensor("Drive Temperature", 0, SensorType.Temperature, this, _settings));
                ActivateSensor(new LibreHardwareMonitor.Hardware.Sensor("Drive Lifetime Used", 1, SensorType.Level, this, _settings));
            }

            public int DeviceUpdateCount { get; private set; }

            protected override void GetReport(StringBuilder r)
            { }

            protected override void UpdateSensors()
            {
                DeviceUpdateCount++;
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
                string storedValue;
                return _values.TryGetValue(name, out storedValue) ? storedValue : value;
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
