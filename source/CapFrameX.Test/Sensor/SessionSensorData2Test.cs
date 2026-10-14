using CapFrameX.Data.Session.Classes;
using CapFrameX.Data.Session.Contracts;
using CapFrameX.Sensor.Reporting;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using System.Linq;

namespace CapFrameX.Test.Sensor
{
    [TestClass]
    public class SessionSensorData2Test
    {
        private static SessionSensorData2 BuildSensorData(params (string key, string name, string type, double[] values)[] sensors)
        {
            var data = new SessionSensorData2();
            int count = sensors.Max(sensor => sensor.values.Length);
            for (int i = 0; i < count; i++)
            {
                data.MeasureTime.Values.AddLast(i * 0.25);
                data.BetweenMeasureTime.Values.AddLast(0.25);
            }

            foreach (var (key, name, type, values) in sensors)
            {
                var entry = new SessionSensorEntry(name, type);
                foreach (var value in values)
                    entry.Values.AddLast(value);
                data[key] = entry;
            }

            return data;
        }

        [TestMethod]
        public void LeadingMissingReading_TakesFirstReading()
        {
            // SessionSensorDataLive logs NaN for a measurement in which no sensor had a value yet.
            var data = BuildSensorData(("/amdcpu/0/load/0", "CPU Total", "Load", new[] { double.NaN, double.NaN, 33.7, 72.1 }));

            CollectionAssert.AreEqual(new[] { 34, 34, 34, 72 }, data.CpuUsage);
        }

        [TestMethod]
        public void MissingReadingInBetween_TakesPrecedingReading()
        {
            var data = BuildSensorData(("/gpu-nvidia/0/power/0", "GPU Power", "Power",
                new[] { 250.0, double.NaN, double.PositiveInfinity, 310.0 }));

            CollectionAssert.AreEqual(new[] { 250, 250, 250, 310 }, data.GpuPower);
        }

        [TestMethod]
        public void SensorWithoutReading_IsNotLogged()
        {
            var data = BuildSensorData(
                ("/amdcpu/0/load/0", "CPU Total", "Load", new[] { 40.0, 45.0 }),
                ("/gpu-nvidia/0/load/0", "GPU Core", "Load", new[] { double.NaN, double.NaN }),
                ("/ram/data/4", "RAM Game Used", "Data", new[] { double.NaN, double.NaN }));

            Assert.AreEqual(0, data.GpuUsage.Length);
            Assert.AreEqual(0, data.RamUsage.Length);
        }

        [TestMethod]
        public void DoubleSeries_FillMissingReadings()
        {
            var data = BuildSensorData(
                ("/ram/data/4", "RAM Game Used", "Data", new[] { double.NaN, 7.5, double.NaN }),
                ("/gpu-nvidia/0/data/3", "GPU Memory Dedicated", "Data", new[] { double.NaN, 8.25, 8.5 }));

            CollectionAssert.AreEqual(new[] { 7.5, 7.5, 7.5 }, data.RamUsage);
            CollectionAssert.AreEqual(new[] { 8.25, 8.25, 8.5 }, data.VRamUsageGB);
        }

        [TestMethod]
        public void FilledSeries_StayAlignedWithMeasureTime()
        {
            var data = BuildSensorData(("/gpu-nvidia/0/load/0", "GPU Core", "Load", new[] { double.NaN, 83.0, 86.0, 84.0 }));

            Assert.AreEqual(data.MeasureTime.Values.Count, data.GpuUsage.Length);
        }

        [TestMethod]
        public void RecordWithMissingFirstReading_LoadsAndReports()
        {
            // Shape of a 1.9.1.5 capture whose first sensor measurement had no values at all.
            const string json = @"{
                ""Runs"": [{
                    ""SensorData2"": {
                        ""MeasureTime"": { ""Name"": ""MeasureTime"", ""Type"": ""Time"", ""Values"": [-0.031, 0.026, 0.263] },
                        ""BetweenMeasureTime"": { ""Name"": ""BetweenMeasureTime"", ""Type"": ""Time"", ""Values"": [-0.031, 0.057, 0.237] },
                        ""/amdcpu/0/load/0"": { ""Name"": ""CPU Total"", ""Type"": ""Load"", ""Values"": [""NaN"", 33.68, 72.11] },
                        ""/gpu-nvidia/0/load/0"": { ""Name"": ""GPU Core"", ""Type"": ""Load"", ""Values"": [""NaN"", 83.0, 86.0] },
                        ""/gpu-nvidia/0/power/0"": { ""Name"": ""GPU Power"", ""Type"": ""Power"", ""Values"": [""NaN"", 230.0, 214.0] }
                    }
                }]
            }";

            var session = JsonConvert.DeserializeObject<Session>(json);
            var sensorData = session.Runs.Single().SensorData2;

            CollectionAssert.AreEqual(new[] { 34, 34, 72 }, sensorData.CpuUsage);
            CollectionAssert.AreEqual(new[] { 83, 83, 86 }, sensorData.GpuUsage);
            // The filled slot lies before the report window (measure time < 0) and does not count.
            Assert.AreEqual(222, SensorReport.GetAverageSensorValues(new ISessionSensorData[] { sensorData }, EReportSensorName.GpuPower));
        }
    }
}
