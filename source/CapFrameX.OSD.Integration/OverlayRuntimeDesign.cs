using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using CapFrameX.Contracts.Configuration;
using CapFrameX.Contracts.Localization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CapFrameX.OSD.Integration
{
    /// <summary>The native scene, stripped of editor metadata, and precisely the readings it uses.</summary>
    public sealed class OverlayRuntimeDesign
    {
        public string ProfileId { get; private set; }
        public string Name { get; private set; }
        public string TemplateJson { get; private set; }
        public IReadOnlyList<string> MetricKeys { get; private set; }
        public bool NeedsFrametimes { get; private set; }
        public bool NeedsDisplayTimes { get; private set; }
        public bool HasClassicRows { get; private set; }
        /// <summary>OLED pixel orbit requested by the design itself; null when it has none.</summary>
        public OverlayPixelShift? PixelShift { get; private set; }
        internal string AuthoringJson { get; private set; }

        public static OverlayRuntimeDesign Parse(string profileId, string name, string json)
        {
            var document = ParseDocument(json);
            var keys = new HashSet<string>(StringComparer.Ordinal);
            bool frame = false, display = false, classicRows = false;
            int count = 0;
            void AddKey(string key)
            {
                if (string.IsNullOrWhiteSpace(key) || key.IndexOf('\0') >= 0 || Encoding.UTF8.GetByteCount(key) > 2048)
                    throw OverlayDesignText.Error(new LocalizedText("OverlayDesignError_TelemetryKey"), message => new InvalidDataException(message));
                keys.Add(key);
            }
            void Inspect(JObject node, int depth)
            {
                if (depth > 32 || ++count > 512)
                    throw OverlayDesignText.Error(new LocalizedText("OverlayDesignError_NodeLimit"), message => new InvalidDataException(message));
                if (node["type"]?.Type != JTokenType.String)
                    throw OverlayDesignText.Error(new LocalizedText("OverlayDesignError_WidgetType"), message => new InvalidDataException(message));
                string type = (string)node["type"];
                classicRows |= type == "classicRows";
                if (string.IsNullOrWhiteSpace(type)) throw OverlayDesignText.Error(new LocalizedText("OverlayDesignError_WidgetType"), message => new InvalidDataException(message));
                if (type == "chart")
                {
                    // A chart draws its series and the series of its lines; a stutter threshold
                    // derives from the series it names.
                    var series = new List<string> { node["series"]?.Type == JTokenType.String ? (string)node["series"] : null };
                    if (node["lines"] is JArray lines)
                        series.AddRange(lines.OfType<JObject>()
                            .Select(line => line["series"]?.Type == JTokenType.String ? (string)line["series"] : null));
                    frame |= series.Any(name => name == "frametimes" || name == "framerates");
                    display |= series.Contains("displaytimes");
                }
                // Group members are already expanded into native child widgets; disabled
                // members exist only in editor metadata and must never request sensor values.
                foreach (string field in new[] { "key", "secondaryKey" })
                {
                    if (node[field] == null) continue;
                    if (node[field].Type != JTokenType.String)
                        throw OverlayDesignText.Error(new LocalizedText("OverlayDesignError_TelemetryKey"), message => new InvalidDataException(message));
                    AddKey((string)node[field]);
                }
                // A heatmap reads one key per cell.
                if (node["keys"] != null)
                {
                    if (node["keys"] is not JArray cellKeys || cellKeys.Any(cell => cell.Type != JTokenType.String))
                        throw OverlayDesignText.Error(new LocalizedText("OverlayDesignError_TelemetryKey"), message => new InvalidDataException(message));
                    foreach (var cell in cellKeys) AddKey((string)cell);
                }
                if (node["children"] is JArray children)
                {
                    foreach (var child in children)
                    {
                        if (child is not JObject childNode)
                            throw OverlayDesignText.Error(new LocalizedText("OverlayDesignError_ChildInvalid"), message => new InvalidDataException(message));
                        Inspect(childNode, depth + 1);
                    }
                }
                else if (node["children"] != null)
                    throw OverlayDesignText.Error(new LocalizedText("OverlayDesignError_ChildrenArray"), message => new InvalidDataException(message));
            }
            Inspect((JObject)document["root"], 0);
            var pixelShift = ReadPixelShift(document["pixelShift"]);
            string authoringJson = document.ToString(Formatting.None);
            foreach (var metadata in document.Descendants().OfType<JProperty>().Where(property => property.Name == "editor").ToArray())
                metadata.Remove();
            // The host applies the orbit through the placement; the renderers never see it.
            document.Remove("pixelShift");
            return new OverlayRuntimeDesign
            {
                ProfileId = profileId,
                Name = name,
                TemplateJson = document.ToString(Formatting.None),
                AuthoringJson = authoringJson,
                MetricKeys = Array.AsReadOnly(keys.OrderBy(key => key, StringComparer.Ordinal).ToArray()),
                NeedsFrametimes = frame,
                NeedsDisplayTimes = display,
                HasClassicRows = classicRows,
                PixelShift = pixelShift
            };
        }

        /// <summary>
        /// The design's own OLED pixel orbit request, or null. The editor validates the exact
        /// ranges; at runtime an out-of-range value is clamped rather than rejecting the scene.
        /// </summary>
        private static OverlayPixelShift? ReadPixelShift(JToken token)
        {
            if (token is not JObject shift) return null;
            static int Read(JToken value, int fallback)
                => value?.Type == JTokenType.Integer ? (int)Math.Max(int.MinValue, Math.Min(int.MaxValue, (long)value)) : fallback;
            return new OverlayPixelShift(
                OsdPixelOrbit.ClampInterval(Read(shift["intervalSeconds"], OsdPixelOrbit.DefaultIntervalSeconds)),
                OsdPixelOrbit.ClampRadius(Read(shift["radius"], OsdPixelOrbit.DefaultRadius)));
        }

        internal static string Canonicalize(string json) => ParseDocument(json).ToString(Formatting.None);

        private static JObject ParseDocument(string json)
        {
            if (string.IsNullOrWhiteSpace(json) || json.IndexOf('\0') >= 0 || Encoding.UTF8.GetByteCount(json) > HookDesignChannel.PayloadCapacity)
                throw OverlayDesignText.Error(new LocalizedText("OverlayDesignError_SavedSize"), message => new InvalidDataException(message));
            using var reader = new JsonTextReader(new StringReader(json)) { MaxDepth = 64, DateParseHandling = DateParseHandling.None };
            var document = JObject.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
            if (reader.Read())
                throw OverlayDesignText.Error(new LocalizedText("OverlayDesignError_TrailingContent"), message => new InvalidDataException(message));
            if (document["version"]?.Type != JTokenType.Integer || document["version"].ToString() != "1" || document["root"] is not JObject)
                throw OverlayDesignText.Error(new LocalizedText("OverlayDesignError_SceneVersion"), message => new InvalidDataException(message));
            return document;
        }

        internal string CreateMetrics(OverlayTelemetrySnapshot snapshot, IReadOnlyDictionary<string, string> aliases,
            OverlayMovingAverages averages = null, DateTime nowUtc = default)
        {
            var values = new JObject();
            foreach (string key in MetricKeys)
            {
                object value = null;
                if (averages != null && averages.TryGetAverage(key, nowUtc, out double? average))
                    value = average;
                else if (snapshot != null && !snapshot.MetricValues.TryGetValue(key, out value) && aliases != null && aliases.TryGetValue(key, out string source))
                    snapshot.MetricValues.TryGetValue(source, out value);
                values[key] = Scalar(value);
            }
            string json = values.ToString(Formatting.None);
            if (Encoding.UTF8.GetByteCount(json) > HookDesignChannel.PayloadCapacity)
                throw OverlayDesignText.Error(new LocalizedText("OverlayDesignError_TelemetryCapacity"), message => new InvalidDataException(message));
            return json;
        }

        private static JToken Scalar(object value)
        {
            if (value is string text) return new JValue(text);
            if (value is double number) return double.IsFinite(number) ? new JValue(number) : JValue.CreateNull();
            if (value is float single) return float.IsFinite(single) ? new JValue(single) : JValue.CreateNull();
            if (value is int || value is long || value is uint || value is ulong || value is decimal || value is short || value is byte)
                return JToken.FromObject(value);
            return JValue.CreateNull();
        }
    }
}
