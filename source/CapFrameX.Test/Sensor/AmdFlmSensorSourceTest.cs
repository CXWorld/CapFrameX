using CapFrameX.Contracts.Configuration;
using CapFrameX.Contracts.Latency;
using CapFrameX.Contracts.Sensor;
using CapFrameX.Monitoring.Contracts;
using CapFrameX.Sensor;
using CapFrameX.Sensor.Reporting;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Subjects;

namespace CapFrameX.Test.Sensor
{
    [TestClass]
    public class AmdFlmSensorSourceTest
    {
        [TestMethod]
        public void CurrentValue_UsesFreshSampleOnlyWhileFeatureIsRunning()
        {
            var samples = new Subject<AmdFlmSample>();
            var service = new Mock<IAmdFlmService>();
            service.SetupGet(value => value.SampleStream).Returns(samples);
            service.SetupGet(value => value.IsRunning).Returns(true);

            bool enabled = true;
            var configurationChanges = new Subject<(string key, object value)>();
            var configuration = new Mock<IAppConfiguration>();
            configuration.SetupGet(value => value.UseAmdFlmLatency).Returns(() => enabled);
            configuration.SetupGet(value => value.OnValueChanged).Returns(configurationChanges);

            using var source = new AmdFlmSensorSource(service.Object, configuration.Object);

            Assert.IsTrue(float.IsNaN(source.GetCurrentValue()));

            samples.OnNext(new AmdFlmSample(
                1, 10, Stopwatch.GetTimestamp(), 12.75, 1.5, 120));

            Assert.AreEqual(12.75f, source.GetCurrentValue(), 0.001f);

            configurationChanges.OnNext((nameof(IAppConfiguration.AmdFlmFrameGeneration), true));
            Assert.IsTrue(float.IsNaN(source.GetCurrentValue()));

            samples.OnNext(new AmdFlmSample(
                2, 10, Stopwatch.GetTimestamp() - (Stopwatch.Frequency * 3), 25, 2, 120));

            Assert.IsTrue(float.IsNaN(source.GetCurrentValue()));

            enabled = false;
            samples.OnNext(new AmdFlmSample(
                3, 10, Stopwatch.GetTimestamp(), 8.5, 1, 120));

            Assert.IsTrue(float.IsNaN(source.GetCurrentValue()));
        }

        [TestMethod]
        public void Entry_UsesStableLatencyMetadata()
        {
            var samples = new Subject<AmdFlmSample>();
            var service = new Mock<IAmdFlmService>();
            service.SetupGet(value => value.SampleStream).Returns(samples);

            var configuration = new Mock<IAppConfiguration>();
            using var source = new AmdFlmSensorSource(service.Object, configuration.Object);

            var entry = source.CreateEntry();

            Assert.AreEqual(AmdFlmSensorMetadata.Identifier, entry.Identifier);
            Assert.AreEqual("AMD FLM Latency", entry.Name);
            Assert.AreEqual("Latency", entry.SensorType);
            Assert.AreEqual("GpuAmd", entry.HardwareType);
            Assert.AreEqual("AMD Frame Latency Meter/latency/AMD FLM Latency",
                SensorIdentifierHelper.BuildStableIdentifier(entry));

            var entryProvider = new SensorEntryProvider(
                new Mock<ISensorService>().Object,
                new Mock<ISensorConfig>().Object);
            Assert.IsTrue(entryProvider.GetIsDefaultActiveSensor(entry));
        }

        [TestMethod]
        public void SessionLogging_BackfillsLateSensorAndReportIgnoresMissingValues()
        {
            var live = new SessionSensorDataLive();
            var sensor = new SensorEntry
            {
                Identifier = AmdFlmSensorMetadata.Identifier,
                Name = AmdFlmSensorMetadata.Name,
                SensorType = "Latency",
                HardwareName = AmdFlmSensorMetadata.HardwareName
            };

            live.AddMeasureTime(DateTime.UtcNow);
            live.CompleteMeasure();

            live.AddMeasureTime(DateTime.UtcNow.AddMilliseconds(250));
            live.AddSensorValue(sensor, 14.5f);
            live.CompleteMeasure();

            var data = live.ToSessionSensorData();
            var loggedValues = data[AmdFlmSensorMetadata.Identifier].Values.ToArray();
            Assert.AreEqual(2, loggedValues.Length);
            Assert.IsTrue(double.IsNaN(loggedValues[0]));
            Assert.AreEqual(14.5, loggedValues[1], 0.001);

            var reportEntry = SensorReport.GetSensorReportEntries(new[] { data })
                .Single(value => value.Type == "Latency");
            CollectionAssert.AreEqual(new[] { 14.5 }, reportEntry.Values);
            Assert.AreEqual("AMD FLM Latency (ms)", reportEntry.DisplayName);
        }
    }
}
