using System;
using System.Collections.Generic;
using System.Linq;
using CapFrameX.OSD.Integration;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CapFrameX.Test.Integration
{
    [TestClass]
    public class OverlayMovingAveragesTest
    {
        private static readonly DateTime Start = new DateTime(2026, 10, 18, 12, 0, 0, DateTimeKind.Utc);

        private static OverlayTelemetrySnapshot Snapshot(double milliseconds, params (string Id, double? Value)[] values)
            => new OverlayTelemetrySnapshot(Start.AddMilliseconds(milliseconds),
                values.ToDictionary(value => value.Id, value => value.Value));

        [TestMethod]
        public void Key_KeepsColonsOfTheSourceId()
        {
            string key = OverlayMovingAverages.Key(60, "sensor:Intel%20Core%2Fload%2FCore%20%231%20P");
            Assert.AreEqual("avg:60:sensor:Intel%20Core%2Fload%2FCore%20%231%20P", key);
            Assert.IsTrue(OverlayMovingAverages.TryParseKey(key, out int seconds, out string source));
            Assert.AreEqual(60, seconds);
            Assert.AreEqual("sensor:Intel%20Core%2Fload%2FCore%20%231%20P", source);
        }

        [TestMethod]
        [DataRow("fps")]
        [DataRow("avg:")]
        [DataRow("avg:60:")]
        [DataRow("avg::cpu")]
        [DataRow("avg:x:cpu")]
        [DataRow("avg:0:cpu")]
        [DataRow("avg:-5:cpu")]
        [DataRow("avg:3601:cpu")]
        public void TryParseKey_RejectsOtherKeys(string key)
        {
            Assert.IsFalse(OverlayMovingAverages.TryParseKey(key, out _, out _));
        }

        [TestMethod]
        public void TrackedKeysAverageTheirSourceOverTheirOwnWindow()
        {
            var averages = new OverlayMovingAverages();
            averages.Track(new[] { "avg:2:cpu", "fps", "avg:1:gpu" });
            averages.Add(Snapshot(0, ("cpu", 10), ("gpu", 50)));
            averages.Add(Snapshot(500, ("cpu", 30)));
            averages.Add(Snapshot(1500, ("cpu", 50), ("gpu", null)));

            Assert.IsTrue(averages.TryGetAverage("avg:2:cpu", Start.AddMilliseconds(1500), out double? cpu));
            Assert.AreEqual(30, cpu.Value, 1e-12);
            Assert.IsTrue(averages.TryGetAverage("avg:1:gpu", Start.AddMilliseconds(1500), out double? gpu));
            Assert.IsNull(gpu, "The only GPU reading has left its one-second window.");
            Assert.IsFalse(averages.TryGetAverage("fps", Start, out _), "A plain key is not averaged.");

            var values = new Dictionary<string, object>();
            averages.CopyTo(values, Start.AddMilliseconds(1500));
            CollectionAssert.AreEquivalent(new[] { "avg:2:cpu", "avg:1:gpu" }, values.Keys.ToArray());
            Assert.AreEqual(30, (double)values["avg:2:cpu"], 1e-12);
            Assert.IsNull(values["avg:1:gpu"]);
        }

        [TestMethod]
        public void A_SourceMissingFromTheSnapshotIsFoundThroughItsAlias()
        {
            var averages = new OverlayMovingAverages();
            averages.Track(new[] { "avg:10:cpuLoad" });
            averages.Add(Snapshot(0, ("cpu/total", 40)), new Dictionary<string, string> { ["cpuLoad"] = "cpu/total" });

            Assert.IsTrue(averages.TryGetAverage("avg:10:cpuLoad", Start, out double? average));
            Assert.AreEqual(40, average.Value, 1e-12);
        }

        [TestMethod]
        public void Track_KeepsTheSamplesOfKeysThatStayAndDropsTheOthers()
        {
            var averages = new OverlayMovingAverages();
            averages.Track(new[] { "avg:60:cpu", "avg:60:gpu" });
            averages.Add(Snapshot(0, ("cpu", 20), ("gpu", 80)));

            averages.Track(new[] { "avg:60:cpu", "avg:30:gpu" });
            averages.Add(Snapshot(1000, ("cpu", 40), ("gpu", 60)));

            Assert.IsTrue(averages.TryGetAverage("avg:60:cpu", Start.AddSeconds(1), out double? cpu));
            Assert.AreEqual(30, cpu.Value, 1e-12, "A key that stays tracked keeps its window.");
            Assert.IsTrue(averages.TryGetAverage("avg:30:gpu", Start.AddSeconds(1), out double? gpu));
            Assert.AreEqual(60, gpu.Value, 1e-12, "A new window starts empty.");
            Assert.IsFalse(averages.TryGetAverage("avg:60:gpu", Start.AddSeconds(1), out _));
        }

        [TestMethod]
        public void A_DesignRequestsTheSourcesItsHeatmapAverages()
        {
            string scene = @"{""version"":1,""name"":""Heatmap"",""root"":{""type"":""panel"",""children"":[
                {""type"":""heatmap"",""keys"":[""avg:60:cpu/1"",""avg:60:cpu/2""]},{""type"":""metric"",""key"":""cpuLoad""}]}}";
            var design = OverlayRuntimeDesign.Parse("profile", "Heatmap", scene);

            CollectionAssert.AreEquivalent(new[] { "avg:60:cpu/1", "avg:60:cpu/2", "cpuLoad" }, design.MetricKeys.ToArray());
            CollectionAssert.AreEquivalent(new[] { "cpu/1", "cpu/2", "cpuLoad", "cpu/total" },
                OverlayDesignRuntimePublisher.GetRequiredSourceIds(design, new Dictionary<string, string> { ["cpuLoad"] = "cpu/total" }).ToArray());
        }
    }
}
