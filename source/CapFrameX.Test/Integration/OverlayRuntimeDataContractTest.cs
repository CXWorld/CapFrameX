using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using CapFrameX.OSD.Integration;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CapFrameX.Test.Integration
{
    [TestClass]
    public class OverlayRuntimeDataContractTest
    {
        [TestMethod]
        public void ExactReadingsIncludingUnavailableValuesTakePriorityOverSemanticAliases()
        {
            var design = Parse(Metric("cpuLoad"), Metric("gpuLoad"), Metric("ramUsed"), Metric("FPS"));
            var snapshot = Snapshot(new Dictionary<string, object>
            {
                ["cpuLoad"] = 0d,
                ["gpuLoad"] = null,
                ["ramUsed"] = double.NaN,
                ["fps"] = 144d,
                ["hardware/cpu"] = 75d,
                ["hardware/gpu"] = 80d,
                ["hardware/ram"] = 16d
            });
            var aliases = new Dictionary<string, string>
            {
                ["cpuLoad"] = "hardware/cpu",
                ["gpuLoad"] = "hardware/gpu",
                ["ramUsed"] = "hardware/ram"
            };

            var metrics = JObject.Parse(design.CreateMetrics(snapshot, aliases));

            Assert.AreEqual(0d, (double)metrics["cpuLoad"], "Zero is a valid reading and must not trigger fallback.");
            Assert.AreEqual(JTokenType.Null, metrics["gpuLoad"].Type, "An explicit unavailable reading must not revive another source.");
            Assert.AreEqual(JTokenType.Null, metrics["ramUsed"].Type, "Invalid exact readings must not revive another source either.");
            Assert.AreEqual(JTokenType.Null, metrics["FPS"].Type, "Source IDs are case-sensitive.");
            Assert.AreEqual(4, metrics.Count, "Alias targets and unrequested source IDs must not leak into the payload.");

            var disconnected = JObject.Parse(design.CreateMetrics(null, aliases));
            Assert.AreEqual(4, disconnected.Count);
            Assert.IsTrue(disconnected.Properties().All(property => property.Value.Type == JTokenType.Null));
        }

        [TestMethod]
        public void MetricPayloadUsesNativeScalarsAndInvariantNumbersForEveryNumericSourceType()
        {
            var readings = new Dictionary<string, object>
            {
                ["double"] = 12.125d,
                ["float"] = 1.25f,
                ["decimal"] = 12.125m,
                ["int"] = int.MinValue,
                ["long"] = long.MinValue,
                ["uint"] = uint.MaxValue,
                ["ulong"] = ulong.MaxValue,
                ["short"] = short.MinValue,
                ["byte"] = byte.MaxValue,
                ["text"] = "12,125 MHz",
                ["positiveInfinity"] = double.PositiveInfinity,
                ["negativeInfinity"] = float.NegativeInfinity,
                ["notANumber"] = float.NaN,
                ["boolean"] = true,
                ["timestamp"] = new DateTime(2026, 10, 12, 0, 0, 0, DateTimeKind.Utc),
                ["object"] = new { Value = 42 },
                ["array"] = new[] { 42 }
            };
            var design = Parse(readings.Keys.Select(Metric).ToArray());
            var previousCulture = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
                var metrics = JObject.Parse(design.CreateMetrics(Snapshot(readings), null));

                foreach (string key in new[] { "double", "float", "decimal" })
                {
                    Assert.AreEqual(JTokenType.Float, metrics[key].Type, key);
                    Assert.AreEqual(Convert.ToDouble(readings[key], CultureInfo.InvariantCulture), (double)metrics[key], key);
                }
                foreach (string key in new[] { "int", "long", "uint", "ulong", "short", "byte" })
                {
                    Assert.AreEqual(JTokenType.Integer, metrics[key].Type, key);
                    Assert.AreEqual(Convert.ToDecimal(readings[key], CultureInfo.InvariantCulture), (decimal)metrics[key], key);
                }
                Assert.AreEqual("12,125 MHz", (string)metrics["text"]);
                foreach (string key in new[] { "positiveInfinity", "negativeInfinity", "notANumber", "boolean", "timestamp", "object", "array" })
                    Assert.AreEqual(JTokenType.Null, metrics[key].Type, key + " must not invalidate the scalar-only native metric payload.");
            }
            finally
            {
                CultureInfo.CurrentCulture = previousCulture;
            }
        }

        [TestMethod]
        public void EscapedUnicodeSourceIdsAndTextSurviveJsonAndUtf8TransportWithoutCreatingExtraKeys()
        {
            const string key = "sensor/\"gpu\\코어\n1";
            const string text = "CPU \"A\"\\sensor\nGPU 온도 🚀";
            var design = Parse(Metric(key), Metric("unavailable"));

            string json = design.CreateMetrics(Snapshot(new Dictionary<string, object> { [key] = text }), null);
            var decoded = JObject.Parse(Encoding.UTF8.GetString(HookDesignChannel.Encode(json)));

            CollectionAssert.AreEquivalent(new[] { key, "unavailable" }, decoded.Properties().Select(property => property.Name).ToArray());
            Assert.AreEqual(text, (string)decoded[key]);
            Assert.AreEqual(JTokenType.Null, decoded["unavailable"].Type);
            CollectionAssert.AreEqual(new[] { key, "unavailable" }, design.MetricKeys.ToArray());
        }

        [TestMethod]
        public void NestedSourceDependenciesStayDistinctAndAuthoringMetadataDoesNotBecomeTelemetry()
        {
            var metric = Metric("gpu/load");
            metric["secondaryKey"] = "GPU/load";
            metric["editor"] = new JObject { ["key"] = "private/editor/source", ["label"] = "Mein Text 한국어" };
            var container = new JObject
            {
                ["type"] = "panel",
                ["children"] = new JArray(metric, Metric("gpu/load"), new JObject { ["type"] = "classicRows" }),
                ["extension"] = new JObject { ["timestamp"] = "2026-10-12T00:00:00Z", ["key"] = "extension/source" }
            };
            var document = Scene(container);
            document["name"] = "Mein Profil 한국어";
            document["editor"] = new JObject { ["format"] = "cfx-grid", ["selectedSource"] = "private/editor/source" };
            string original = document.ToString(Formatting.None);

            var design = OverlayRuntimeDesign.Parse("profile-id", "Mein Profil 한국어", original);
            var native = JObject.Parse(design.TemplateJson, new JsonLoadSettings());
            var extension = native["root"]["children"][0]["extension"];

            CollectionAssert.AreEqual(new[] { "GPU/load", "gpu/load" }, design.MetricKeys.ToArray());
            Assert.IsTrue(design.HasClassicRows);
            Assert.AreEqual("profile-id", design.ProfileId);
            Assert.AreEqual("Mein Profil 한국어", design.Name);
            Assert.AreEqual("Mein Profil 한국어", (string)native["name"]);
            Assert.AreEqual("extension/source", (string)extension["key"]);
            Assert.IsTrue(design.TemplateJson.Contains("\"timestamp\":\"2026-10-12T00:00:00Z\""), "Date-looking extension values must remain literal strings.");
            Assert.IsFalse(native.Descendants().OfType<JProperty>().Any(property => property.Name == "editor"));
            Assert.AreEqual(original, design.AuthoringJson, "Compilation must retain the complete authoring document for future source rebinding.");
        }

        [TestMethod]
        [DataRow("key")]
        [DataRow("secondaryKey")]
        public void SourceKeyLimitUsesUtf8BytesAndRejectsEmbeddedNulls(string field)
        {
            string key = string.Concat(Enumerable.Repeat("🚀", 511)) + "abcd";
            Assert.AreEqual(2048, Encoding.UTF8.GetByteCount(key));
            var widget = Metric("primary");
            widget[field] = key;

            var design = Parse(widget);
            Assert.IsTrue(design.MetricKeys.Contains(key));

            widget[field] = key + "é";
            Assert.ThrowsExactly<InvalidDataException>(() => Parse(widget), "A key with fewer than 2048 UTF-16 characters can still exceed the native UTF-8 limit.");
            widget[field] = "sensor\0hidden";
            Assert.ThrowsExactly<InvalidDataException>(() => Parse(widget), "Escaped JSON nulls must not enter source identifiers.");
        }

        [TestMethod]
        public void MetricPayloadAcceptsExactCapacityAndRejectsOneExtraUtf8Byte()
        {
            var readings = Enumerable.Range(0, 510).ToDictionary(index => "k" + index.ToString("D3"), _ => (object)new string('x', 2048));
            // Each four-character key adds ten JSON bytes including its separators;
            // the final comma is replaced by the closing brace.
            int lastValueLength = HookDesignChannel.PayloadCapacity - (509 * 2048 + 510 * 10 + 1);
            readings["k509"] = new string('x', lastValueLength);
            var design = Parse(readings.Keys.Select(Metric).ToArray());

            string metrics = design.CreateMetrics(Snapshot(readings), null);
            Assert.AreEqual(HookDesignChannel.PayloadCapacity, Encoding.UTF8.GetByteCount(metrics));
            Assert.AreEqual(510, JObject.Parse(metrics).Count);

            readings["k509"] = new string('x', lastValueLength - 1) + "é";
            Assert.AreEqual(lastValueLength, ((string)readings["k509"]).Length);
            Assert.ThrowsExactly<InvalidDataException>(() => design.CreateMetrics(Snapshot(readings), null),
                "Equal character counts must not conceal an oversized UTF-8 payload.");
        }

        [TestMethod]
        public void SemanticBindingIgnoresUnavailableSourcesAndDeduplicatesOnlyIdenticalSourceIds()
        {
            var document = BindingScene(Metric("gpuLoad"));
            var design = OverlayRuntimeDesign.Parse("id", "GPU", document.ToString(Formatting.None));
            var available = Source("gpu/0/load", "gpuLoad", "%");
            var catalog = new[]
            {
                null,
                Source("gpu/offline/load", "gpuLoad", "%", false),
                Source(" ", "gpuLoad", "%"),
                available,
                available
            };

            var bound = OverlayRuntimeSourceBinder.Bind(design, catalog);
            CollectionAssert.AreEqual(new[] { "gpu/0/load" }, bound.MetricKeys.ToArray());
            Assert.AreEqual(design.ProfileId, bound.ProfileId);
            Assert.AreEqual(design.Name, bound.Name);
            Assert.AreEqual("gpuLoad", (string)JObject.Parse(design.AuthoringJson)["root"]["children"][0]["children"][0]["key"]);

            var ambiguous = catalog.Append(Source("gpu/1/load", "gpuLoad", "%")).ToArray();
            Assert.AreSame(design, OverlayRuntimeSourceBinder.Bind(design, ambiguous), "Two GPUs with the same role must never select an arbitrary device.");
        }

        [TestMethod]
        public void SemanticBindingPreservesAuthoredUnitsScalesFormatsAndTextForBothMetricInputs()
        {
            var clock = Metric("cpuClock");
            clock["unit"] = "GHz";
            clock["valueScale"] = .001;
            clock["secondaryKey"] = "ramUsed";
            clock["secondaryUnit"] = "MB";
            clock["secondaryScale"] = 1024;
            clock["label"] = "Eigener Text 한국어";
            var formatted = Metric("runtime");
            formatted["unit"] = "custom time";
            formatted["valueFormat"] = "duration";
            var temperature = Metric("gpuTemperature");
            temperature["unit"] = "old unit";
            temperature["secondaryKey"] = "ramTotal";
            temperature["secondaryUnit"] = "old secondary unit";
            var document = BindingScene(clock, formatted, temperature);
            var design = OverlayRuntimeDesign.Parse("id", "Eigener Profilname", document.ToString(Formatting.None));
            string original = design.AuthoringJson;
            var catalog = new[]
            {
                Source("cpu/clock", "cpuClock", "MHz"),
                Source("ram/used", "ramUsed", "GB"),
                Source("app/runtime", "runtime", "s"),
                Source("gpu/temp", "gpuTemperature", "°C"),
                Source("ram/total", "ramTotal", "GB")
            };

            var bound = OverlayRuntimeSourceBinder.Bind(design, catalog);
            var widgets = JObject.Parse(bound.TemplateJson)["root"]["children"].Select(frame => frame["children"][0]).ToArray();

            Assert.AreEqual("cpu/clock", (string)widgets[0]["key"]);
            Assert.AreEqual("ram/used", (string)widgets[0]["secondaryKey"]);
            Assert.AreEqual("GHz", (string)widgets[0]["unit"]);
            Assert.AreEqual(.001, (double)widgets[0]["valueScale"]);
            Assert.AreEqual("MB", (string)widgets[0]["secondaryUnit"]);
            Assert.AreEqual(1024d, (double)widgets[0]["secondaryScale"]);
            Assert.AreEqual("Eigener Text 한국어", (string)widgets[0]["label"]);
            Assert.AreEqual("custom time", (string)widgets[1]["unit"]);
            Assert.AreEqual("duration", (string)widgets[1]["valueFormat"]);
            Assert.AreEqual("°C", (string)widgets[2]["unit"]);
            Assert.AreEqual("GB", (string)widgets[2]["secondaryUnit"]);
            CollectionAssert.AreEquivalent(catalog.Select(source => source.Id).ToArray(), bound.MetricKeys.ToArray());
            Assert.AreEqual(original, design.AuthoringJson);

            var metrics = JObject.Parse(bound.CreateMetrics(Snapshot(catalog.ToDictionary(source => source.Id, _ => (object)42d)), null));
            Assert.IsTrue(metrics.Properties().All(property => (double)property.Value == 42d), "Unit conversion belongs to the native metric definition; source data must remain unscaled.");
        }

        [TestMethod]
        [DataRow(true)]
        [DataRow(false)]
        public void PortableGroupsDoNotMergeFamiliesWithConflictingUnitsOrCategories(bool differentUnits)
        {
            var group = new JObject
            {
                ["type"] = "panel",
                ["editor"] = new JObject { ["kind"] = "group", ["groupKey"] = "role:cpu-loads", ["groupDisplay"] = "metric" },
                ["children"] = new JArray(
                    new JObject { ["type"] = "text", ["text"] = "CPU" },
                    new JObject { ["type"] = "panel", ["children"] = new JArray(Metric("role:cpu-loads/1")) })
            };
            var design = OverlayRuntimeDesign.Parse("id", "CPU", BindingScene(group).ToString(Formatting.None));
            var catalog = Enumerable.Range(0, 4).Select(index => new OverlayTelemetrySource
            {
                Id = "cpu/readings/" + index,
                Name = "Core #" + index,
                DeviceKey = "cpu0",
                GroupKey = "cpu0/cpu-loads",
                Unit = differentUnits && index >= 2 ? "MHz" : "%",
                Category = !differentUnits && index >= 2 ? "Clock" : "Load"
            }).ToArray();

            Assert.AreSame(design, OverlayRuntimeSourceBinder.Bind(design, catalog), "A portable group must stay unresolved instead of combining incompatible readings.");
            CollectionAssert.AreEqual(new[] { "role:cpu-loads/1" }, design.MetricKeys.ToArray());
        }

        [TestMethod]
        [DataRow(null)]
        [DataRow("future-grid-v2")]
        public void BindingLeavesDocumentsWithoutTheSupportedAuthoringSchemaUntouched(string format)
        {
            var document = BindingScene(Metric("cpuLoad"));
            if (format == null)
                document.Remove("editor");
            else
                document["editor"]["format"] = format;
            var design = OverlayRuntimeDesign.Parse("id", "Custom JSON", document.ToString(Formatting.None));
            var catalog = new[] { Source("cpu/load", "cpuLoad", "%") };

            Assert.AreSame(design, OverlayRuntimeSourceBinder.Bind(design, catalog));
            CollectionAssert.AreEqual(new[] { "cpuLoad" }, design.MetricKeys.ToArray());
        }

        private static OverlayTelemetrySnapshot Snapshot(Dictionary<string, object> readings)
        {
            return new OverlayTelemetrySnapshot(new DateTime(2026, 10, 12, 0, 0, 0, DateTimeKind.Utc),
                new Dictionary<string, double?>(), readings);
        }

        private static OverlayRuntimeDesign Parse(params JObject[] widgets)
        {
            return OverlayRuntimeDesign.Parse("id", "Data contract", Scene(widgets).ToString(Formatting.None));
        }

        private static JObject Metric(string key)
        {
            return new JObject { ["type"] = "metric", ["key"] = key };
        }

        private static JObject Scene(params JObject[] widgets)
        {
            return new JObject
            {
                ["version"] = 1,
                ["root"] = new JObject { ["type"] = "panel", ["layout"] = "vertical", ["width"] = 512, ["children"] = new JArray(widgets) }
            };
        }

        private static JObject BindingScene(params JObject[] widgets)
        {
            var frames = widgets.Select(widget =>
            {
                widget["editor"] ??= new JObject { ["kind"] = "metric" };
                return new JObject { ["type"] = "panel", ["children"] = new JArray(widget) };
            }).ToArray();
            var document = Scene(frames);
            document["editor"] = new JObject { ["format"] = "cfx-grid", ["scale"] = 1, ["gridSize"] = 8 };
            return document;
        }

        private static OverlayTelemetrySource Source(string id, string semanticKey, string unit, bool available = true)
        {
            return new OverlayTelemetrySource { Id = id, SemanticKey = semanticKey, Unit = unit, IsAvailable = available };
        }
    }
}
