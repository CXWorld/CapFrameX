using CapFrameX.Data.Session.Classes;
using CapFrameX.Data.Session.Contracts;
using CapFrameX.Sensor.Reporting;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;

namespace CapFrameX.Test.Sensor
{
    [TestClass]
    public class SensorAggregationStableIdTest
    {
        #region Helper

        private static SessionSensorData2 BuildSensorData(
            IEnumerable<(string key, string name, string type, string stableId, double[] values)> sensors,
            double[] measureTimes)
        {
            var data = new SessionSensorData2(initialAdd: false);

            var mtEntry = new SessionSensorEntry("MeasureTime", "Time");
            foreach (var t in measureTimes) mtEntry.Values.AddLast(t);
            data["MeasureTime"] = mtEntry;

            var btEntry = new SessionSensorEntry("BetweenMeasureTime", "Time");
            double prev = 0;
            foreach (var t in measureTimes)
            {
                btEntry.Values.AddLast(t - prev);
                prev = t;
            }
            data["BetweenMeasureTime"] = btEntry;

            foreach (var (key, name, type, stableId, values) in sensors)
            {
                var entry = new SessionSensorEntry(name, type) { StableIdentifier = stableId };
                foreach (var v in values) entry.Values.AddLast(v);
                data[key] = entry;
            }

            return data;
        }

        private static double[] Times3 = new[] { 1.0, 2.0, 3.0 };

        #endregion

        #region Baseline: Same version, same keys

        [TestMethod]
        public void SameVersion_SameKeys_AllSensorsAggregated()
        {
            var session1 = BuildSensorData(new[]
            {
                ("/gpu-nvidia/0/temperature/0", "GPU Core", "Temperature", "NVIDIA RTX 4090/temperature/GPU Core", new[] { 60.0, 62.0, 65.0 }),
                ("/amdcpu/0/load/0",            "CPU Total", "Load",       "AMD Ryzen 9 7950X/load/CPU Total",     new[] { 40.0, 45.0, 50.0 }),
            }, Times3);

            var session2 = BuildSensorData(new[]
            {
                ("/gpu-nvidia/0/temperature/0", "GPU Core", "Temperature", "NVIDIA RTX 4090/temperature/GPU Core", new[] { 70.0, 72.0, 75.0 }),
                ("/amdcpu/0/load/0",            "CPU Total", "Load",       "AMD Ryzen 9 7950X/load/CPU Total",     new[] { 50.0, 55.0, 60.0 }),
            }, Times3);

            var entries = SensorReport.GetSensorReportEntries(new[] { session1, session2 }).ToList();

            var gpuEntry = entries.FirstOrDefault(e => e.Name == "GPU Core" && e.Type == "Temperature");
            Assert.IsNotNull(gpuEntry, "GPU Core Temperature should be present");
            Assert.AreEqual(6, gpuEntry.Values.Length, "Should have 6 values (3 per session)");

            var cpuEntry = entries.FirstOrDefault(e => e.Name == "CPU Total" && e.Type == "Load");
            Assert.IsNotNull(cpuEntry, "CPU Total Load should be present");
            Assert.AreEqual(6, cpuEntry.Values.Length);
        }

        [TestMethod]
        public void SameVersion_SameKeys_ValuesCorrect()
        {
            var session1 = BuildSensorData(new (string, string, string, string, double[])[]
            {
                ("/gpu/0/temp/0", "GPU Core", "Temperature", null, new[] { 60.0, 70.0 }),
            }, new[] { 1.0, 2.0 });

            var session2 = BuildSensorData(new (string, string, string, string, double[])[]
            {
                ("/gpu/0/temp/0", "GPU Core", "Temperature", null, new[] { 80.0, 90.0 }),
            }, new[] { 1.0, 2.0 });

            var entries = SensorReport.GetSensorReportEntries(new[] { session1, session2 }).ToList();
            var gpu = entries.First(e => e.Name == "GPU Core");

            CollectionAssert.AreEqual(new[] { 60.0, 70.0, 80.0, 90.0 }, gpu.Values);
        }

        #endregion

        #region Shifted keys: Matched by canonical key (Name+Type)

        [TestMethod]
        public void ShiftedKeys_NoStableId_MatchedByCanonicalKey()
        {
            // Session 1: old library version (index 0)
            var session1 = BuildSensorData(new (string, string, string, string, double[])[]
            {
                ("/gpu-nvidia/0/temperature/0", "GPU Core", "Temperature", null, new[] { 60.0, 62.0, 65.0 }),
                ("/gpu-nvidia/0/load/0",        "GPU Core", "Load",        null, new[] { 80.0, 85.0, 90.0 }),
            }, Times3);

            // Session 2: new library version (index shifted to 1)
            var session2 = BuildSensorData(new (string, string, string, string, double[])[]
            {
                ("/gpu-nvidia/0/temperature/1", "GPU Core", "Temperature", null, new[] { 70.0, 72.0, 75.0 }),
                ("/gpu-nvidia/0/load/1",        "GPU Core", "Load",        null, new[] { 90.0, 92.0, 95.0 }),
            }, Times3);

            var entries = SensorReport.GetSensorReportEntries(new[] { session1, session2 }).ToList();

            var gpuTemp = entries.FirstOrDefault(e => e.Name == "GPU Core" && e.Type == "Temperature");
            Assert.IsNotNull(gpuTemp, "GPU Core Temperature should be matched across shifted keys");
            Assert.AreEqual(6, gpuTemp.Values.Length);

            var gpuLoad = entries.FirstOrDefault(e => e.Name == "GPU Core" && e.Type == "Load");
            Assert.IsNotNull(gpuLoad, "GPU Core Load should be matched across shifted keys");
            Assert.AreEqual(6, gpuLoad.Values.Length);
        }

        [TestMethod]
        public void ShiftedKeys_WithStableId_MatchedByCanonicalKey()
        {
            var session1 = BuildSensorData(new[]
            {
                ("/gpu-nvidia/0/temperature/0", "GPU Core", "Temperature", "NVIDIA RTX 4090/temperature/GPU Core", new[] { 60.0, 62.0 }),
            }, new[] { 1.0, 2.0 });

            var session2 = BuildSensorData(new[]
            {
                ("/gpu-nvidia/0/temperature/1", "GPU Core", "Temperature", "NVIDIA RTX 4090/temperature/GPU Core", new[] { 70.0, 72.0 }),
            }, new[] { 1.0, 2.0 });

            var entries = SensorReport.GetSensorReportEntries(new[] { session1, session2 }).ToList();

            var gpuTemp = entries.FirstOrDefault(e => e.Name == "GPU Core" && e.Type == "Temperature");
            Assert.IsNotNull(gpuTemp, "Should match via canonical key even with shifted dict keys");
            Assert.AreEqual(4, gpuTemp.Values.Length);
        }

        [TestMethod]
        public void ShiftedKeys_MultipleSensors_AllMatched()
        {
            var session1 = BuildSensorData(new[]
            {
                ("/gpu/0/temp/0",  "GPU Core",    "Temperature", "GPU/temperature/GPU Core",    new[] { 60.0 }),
                ("/gpu/0/load/0",  "GPU Core",    "Load",        "GPU/load/GPU Core",           new[] { 80.0 }),
                ("/cpu/0/temp/0",  "CPU Package", "Temperature", "CPU/temperature/CPU Package", new[] { 50.0 }),
                ("/cpu/0/power/0", "CPU Package", "Power",       "CPU/power/CPU Package",       new[] { 95.0 }),
            }, new[] { 1.0 });

            var session2 = BuildSensorData(new[]
            {
                ("/gpu/0/temp/5",  "GPU Core",    "Temperature", "GPU/temperature/GPU Core",    new[] { 70.0 }),
                ("/gpu/0/load/5",  "GPU Core",    "Load",        "GPU/load/GPU Core",           new[] { 90.0 }),
                ("/cpu/0/temp/5",  "CPU Package", "Temperature", "CPU/temperature/CPU Package", new[] { 55.0 }),
                ("/cpu/0/power/5", "CPU Package", "Power",       "CPU/power/CPU Package",       new[] { 100.0 }),
            }, new[] { 1.0 });

            var entries = SensorReport.GetSensorReportEntries(new[] { session1, session2 }).ToList();

            // 4 real sensors + 1 synthetic "GPU Limit Time" (auto-added when GPU Core Load is present)
            var realSensors = entries.Where(e => e.Type != "Time" && e.Type != "LoadLimit").ToList();
            Assert.AreEqual(4, realSensors.Count, "All 4 sensors should be matched");
            Assert.IsTrue(realSensors.All(e => e.Values.Length == 2), "Each sensor should have 2 values");
        }

        #endregion

        #region Mixed old/new sessions

        [TestMethod]
        public void MixedOldNew_SessionWithAndWithoutStableId_MatchedByCanonicalKey()
        {
            // Old session (no StableIdentifier)
            var oldSession = BuildSensorData(new (string, string, string, string, double[])[]
            {
                ("/gpu/0/temp/0", "GPU Core", "Temperature", null, new[] { 60.0, 65.0 }),
            }, new[] { 1.0, 2.0 });

            // New session (with StableIdentifier, shifted index)
            var newSession = BuildSensorData(new[]
            {
                ("/gpu/0/temp/1", "GPU Core", "Temperature", "NVIDIA RTX 4090/temperature/GPU Core", new[] { 70.0, 75.0 }),
            }, new[] { 1.0, 2.0 });

            var entries = SensorReport.GetSensorReportEntries(new[] { oldSession, newSession }).ToList();

            var gpuTemp = entries.FirstOrDefault(e => e.Name == "GPU Core" && e.Type == "Temperature");
            Assert.IsNotNull(gpuTemp, "Canonical key should match regardless of StableIdentifier presence");
            Assert.AreEqual(4, gpuTemp.Values.Length);
        }

        #endregion

        #region Multi-GPU: Colliding canonical keys

        [TestMethod]
        public void MultiGpu_CollidingCanonicalKey_FallbackToDictKey()
        {
            // Two identical GPUs in session 1: same Name+Type, different dict keys
            var session1 = BuildSensorData(new[]
            {
                ("/gpu-nvidia/0/temperature/0", "GPU Core", "Temperature", "NVIDIA RTX 4090/temperature/GPU Core", new[] { 60.0 }),
                ("/gpu-nvidia/1/temperature/0", "GPU Core", "Temperature", "NVIDIA RTX 4090/temperature/GPU Core", new[] { 65.0 }),
            }, new[] { 1.0 });

            // Same two GPUs in session 2: same dict keys
            var session2 = BuildSensorData(new[]
            {
                ("/gpu-nvidia/0/temperature/0", "GPU Core", "Temperature", "NVIDIA RTX 4090/temperature/GPU Core", new[] { 70.0 }),
                ("/gpu-nvidia/1/temperature/0", "GPU Core", "Temperature", "NVIDIA RTX 4090/temperature/GPU Core", new[] { 75.0 }),
            }, new[] { 1.0 });

            var entries = SensorReport.GetSensorReportEntries(new[] { session1, session2 }).ToList();

            // Both GPUs should be present (matched by dict key since canonical key is ambiguous)
            var gpuTemps = entries.Where(e => e.Name == "GPU Core" && e.Type == "Temperature").ToList();
            Assert.AreEqual(2, gpuTemps.Count, "Both GPUs should be present via dict key fallback");
        }

        [TestMethod]
        public void MultiGpu_CollidingCanonicalKey_ShiftedKeys_SensorsDropped()
        {
            // Two identical GPUs, but indices shift between sessions
            // This case cannot be resolved — sensors should be dropped
            var session1 = BuildSensorData(new[]
            {
                ("/gpu-nvidia/0/temperature/0", "GPU Core", "Temperature", "NVIDIA RTX 4090/temperature/GPU Core", new[] { 60.0 }),
                ("/gpu-nvidia/1/temperature/0", "GPU Core", "Temperature", "NVIDIA RTX 4090/temperature/GPU Core", new[] { 65.0 }),
            }, new[] { 1.0 });

            var session2 = BuildSensorData(new[]
            {
                ("/gpu-nvidia/0/temperature/1", "GPU Core", "Temperature", "NVIDIA RTX 4090/temperature/GPU Core", new[] { 70.0 }),
                ("/gpu-nvidia/1/temperature/1", "GPU Core", "Temperature", "NVIDIA RTX 4090/temperature/GPU Core", new[] { 75.0 }),
            }, new[] { 1.0 });

            var entries = SensorReport.GetSensorReportEntries(new[] { session1, session2 }).ToList();

            // Keys shifted + canonical key ambiguous = dict key fallback = no match across sessions
            var gpuTemps = entries.Where(e => e.Name == "GPU Core" && e.Type == "Temperature").ToList();
            Assert.AreEqual(0, gpuTemps.Count, "Ambiguous sensors with shifted keys cannot be matched");
        }

        [TestMethod]
        public void MultiGpu_DifferentCards_CanonicalKeyUnique_AllMatched()
        {
            // Different GPU models: canonical keys are unique
            var session1 = BuildSensorData(new[]
            {
                ("/gpu-nvidia/0/temperature/0", "GPU Core",    "Temperature", "NVIDIA RTX 4090/temperature/GPU Core", new[] { 60.0 }),
                ("/gpu-amd/0/temperature/0",    "GPU Hot Spot","Temperature", "AMD RX 7900/temperature/GPU Hot Spot", new[] { 55.0 }),
            }, new[] { 1.0 });

            var session2 = BuildSensorData(new[]
            {
                ("/gpu-nvidia/0/temperature/1", "GPU Core",    "Temperature", "NVIDIA RTX 4090/temperature/GPU Core", new[] { 70.0 }),
                ("/gpu-amd/0/temperature/1",    "GPU Hot Spot","Temperature", "AMD RX 7900/temperature/GPU Hot Spot", new[] { 65.0 }),
            }, new[] { 1.0 });

            var entries = SensorReport.GetSensorReportEntries(new[] { session1, session2 }).ToList();

            var nvidiaTemp = entries.FirstOrDefault(e => e.Name == "GPU Core");
            Assert.IsNotNull(nvidiaTemp, "NVIDIA GPU should match by canonical key");
            Assert.AreEqual(2, nvidiaTemp.Values.Length);

            var amdTemp = entries.FirstOrDefault(e => e.Name == "GPU Hot Spot");
            Assert.IsNotNull(amdTemp, "AMD GPU should match by canonical key");
            Assert.AreEqual(2, amdTemp.Values.Length);
        }

        #endregion

        #region MeasureTime special entries

        [TestMethod]
        public void MeasureTime_AlwaysMatchedAcrossSessions()
        {
            var session1 = BuildSensorData(new (string, string, string, string, double[])[]
            {
                ("/gpu/0/temp/0", "GPU Core", "Temperature", null, new[] { 60.0, 65.0 }),
            }, new[] { 1.0, 2.0 });

            var session2 = BuildSensorData(new (string, string, string, string, double[])[]
            {
                ("/gpu/0/temp/1", "GPU Core", "Temperature", null, new[] { 70.0, 75.0 }),
            }, new[] { 1.0, 2.0 });

            var entries = SensorReport.GetSensorReportEntries(new[] { session1, session2 }).ToList();

            var measureTime = entries.FirstOrDefault(e => e.Name == "MeasureTime");
            Assert.IsNotNull(measureTime, "MeasureTime should always be present");
            Assert.AreEqual(4, measureTime.Values.Length);
        }

        #endregion

        #region JSON Round-Trip

        [TestMethod]
        public void JsonRoundTrip_StableIdentifier_Persisted()
        {
            var original = BuildSensorData(new[]
            {
                ("/gpu/0/temp/0", "GPU Core", "Temperature", "NVIDIA RTX 4090/temperature/GPU Core", new[] { 60.0, 65.0 }),
                ("/cpu/0/load/0", "CPU Total", "Load", "AMD Ryzen 9 7950X/load/CPU Total", new[] { 40.0, 45.0 }),
            }, new[] { 1.0, 2.0 });

            var json = JsonConvert.SerializeObject(original);
            var deserialized = JsonConvert.DeserializeObject<Dictionary<string, SessionSensorEntry>>(json);

            var gpuEntry = deserialized["/gpu/0/temp/0"];
            Assert.AreEqual("NVIDIA RTX 4090/temperature/GPU Core", gpuEntry.StableIdentifier);
            Assert.AreEqual("GPU Core", gpuEntry.Name);
            Assert.AreEqual("Temperature", gpuEntry.Type);
            Assert.AreEqual(2, gpuEntry.Values.Count);

            var cpuEntry = deserialized["/cpu/0/load/0"];
            Assert.AreEqual("AMD Ryzen 9 7950X/load/CPU Total", cpuEntry.StableIdentifier);
        }

        [TestMethod]
        public void JsonRoundTrip_OldFormat_StableIdentifierIsNull()
        {
            // Simulate old session JSON: no StableIdentifier field
            var json = @"{
                ""/gpu/0/temp/0"": { ""Name"": ""GPU Core"", ""Type"": ""Temperature"", ""Values"": [60.0, 65.0] },
                ""MeasureTime"": { ""Name"": ""MeasureTime"", ""Type"": ""Time"", ""Values"": [1.0, 2.0] },
                ""BetweenMeasureTime"": { ""Name"": ""BetweenMeasureTime"", ""Type"": ""Time"", ""Values"": [1.0, 1.0] }
            }";

            var deserialized = JsonConvert.DeserializeObject<Dictionary<string, SessionSensorEntry>>(json);

            var gpuEntry = deserialized["/gpu/0/temp/0"];
            Assert.IsNull(gpuEntry.StableIdentifier, "Old format should deserialize with null StableIdentifier");
            Assert.AreEqual("GPU Core", gpuEntry.Name);
            Assert.AreEqual("Temperature", gpuEntry.Type);
            Assert.AreEqual(2, gpuEntry.Values.Count);
        }

        #endregion

        #region End-to-End: GetFullReportFromSessionSensorData

        [TestMethod]
        public void GetFullReport_ShiftedKeys_CorrectMinAvgMax()
        {
            // Session 1: GPU temp 60, 70 → min=60, avg=65, max=70
            var session1 = BuildSensorData(new[]
            {
                ("/gpu/0/temp/0", "GPU Core", "Temperature", "GPU/temperature/GPU Core", new[] { 60.0, 70.0 }),
            }, new[] { 1.0, 2.0 });

            // Session 2: GPU temp 80, 90 → min=80, avg=85, max=90
            // With shifted key
            var session2 = BuildSensorData(new[]
            {
                ("/gpu/0/temp/5", "GPU Core", "Temperature", "GPU/temperature/GPU Core", new[] { 80.0, 90.0 }),
            }, new[] { 1.0, 2.0 });

            var items = SensorReport.GetFullReportFromSessionSensorData(new[] { session1, session2 }).ToList();

            var gpu = items.FirstOrDefault(i => i.Name.Contains("GPU Core"));
            Assert.IsNotNull(gpu, "GPU Core should appear in report despite shifted keys");
            // All 4 values combined: 60, 70, 80, 90 → min=60, avg=75, max=90
            Assert.AreEqual(60.0, gpu.MinValue);
            Assert.AreEqual(75.0, gpu.AverageValue);
            Assert.AreEqual(90.0, gpu.MaxValue);
        }

        #endregion
    }
}
