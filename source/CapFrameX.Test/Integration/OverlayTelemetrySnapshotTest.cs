using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CapFrameX.OSD.Integration;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CapFrameX.Test.Integration
{
    [TestClass]
    public class OverlayTelemetrySnapshotTest
    {
        [TestMethod]
        public void PublishedSnapshotOwnsReadOnlyCopiesAndPreservesTypedCaseSensitiveSourceIds()
        {
            var timestamp = new DateTime(2026, 10, 12, 8, 30, 0, DateTimeKind.Utc);
            var values = new Dictionary<string, double?>
            {
                ["sensor/Load"] = 0,
                ["sensor/load"] = null,
                ["sensor/temperature"] = -12.5
            };
            var text = new Dictionary<string, object>
            {
                ["metric/name"] = "  Mein Overlay · 사용자 텍스트\r\n",
                ["metric/status"] = null
            };
            var snapshot = new OverlayTelemetrySnapshot(timestamp, values, text);

            values.Clear();
            text["metric/name"] = "Changed";
            text.Clear();

            Assert.AreEqual(timestamp, snapshot.TimestampUtc);
            Assert.AreEqual(3, snapshot.Values.Count);
            Assert.AreEqual(5, snapshot.MetricValues.Count);
            Assert.AreEqual(0d, snapshot.Values["sensor/Load"]);
            Assert.IsNull(snapshot.Values["sensor/load"]);
            Assert.AreEqual(-12.5d, snapshot.MetricValues["sensor/temperature"]);
            Assert.AreEqual("  Mein Overlay · 사용자 텍스트\r\n", snapshot.MetricValues["metric/name"]);
            Assert.IsNull(snapshot.MetricValues["metric/status"]);
            Assert.IsFalse(snapshot.Values.ContainsKey("metric/name"));
            Assert.ThrowsExactly<NotSupportedException>(() =>
                ((IDictionary<string, double?>)snapshot.Values)["sensor/Load"] = 100);
            Assert.ThrowsExactly<NotSupportedException>(() =>
                ((IDictionary<string, object>)snapshot.MetricValues)["metric/name"] = "Changed");
        }

        [TestMethod]
        public void TextExactlyAtTheNativeUtf8LimitIsPreservedWithoutEllipsis()
        {
            var labels = new Dictionary<string, object>
            {
                ["ascii"] = new string('x', 2048),
                ["korean"] = new string('한', 682) + "ab",
                ["supplementary"] = string.Concat(Enumerable.Repeat("🚀", 512))
            };
            var snapshot = new OverlayTelemetrySnapshot(DateTime.UtcNow,
                new Dictionary<string, double?>(), labels);

            foreach (var label in labels)
            {
                Assert.AreEqual(2048, Encoding.UTF8.GetByteCount((string)label.Value));
                Assert.AreEqual(label.Value, snapshot.MetricValues[label.Key], label.Key);
            }
        }

        [TestMethod]
        public void TextOverflowReservesSpaceForEllipsisWithoutSplittingTheLastRune()
        {
            var labels = new Dictionary<string, object>
            {
                ["ascii"] = new string('x', 2045) + "🚀",
                ["supplementary"] = new string('x', 2043) + "🚀tail",
                ["korean"] = new string('한', 681) + "🚀tail"
            };
            var expected = new Dictionary<string, string>
            {
                ["ascii"] = new string('x', 2045) + "…",
                ["supplementary"] = new string('x', 2043) + "…",
                ["korean"] = new string('한', 681) + "…"
            };
            var snapshot = new OverlayTelemetrySnapshot(DateTime.UtcNow,
                new Dictionary<string, double?>(), labels);
            var strictUtf8 = new UTF8Encoding(false, true);

            foreach (var label in expected)
            {
                string actual = (string)snapshot.MetricValues[label.Key];
                Assert.AreEqual(label.Value, actual, label.Key);
                Assert.IsTrue(strictUtf8.GetByteCount(actual) <= 2048);
            }
        }
    }
}
