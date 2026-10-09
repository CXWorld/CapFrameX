using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
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
        internal string AuthoringJson { get; private set; }

        public static OverlayRuntimeDesign Parse(string profileId, string name, string json)
        {
            var document = ParseDocument(json);
            var keys = new HashSet<string>(StringComparer.Ordinal);
            bool frame = false, display = false, classicRows = false;
            int count = 0;
            void Inspect(JObject node, int depth)
            {
                if (depth > 32 || ++count > 512)
                    throw new InvalidDataException("An overlay design exceeds the 512-node or 32-level limit.");
                if (node["type"]?.Type != JTokenType.String)
                    throw new InvalidDataException("An overlay widget has no type.");
                string type = (string)node["type"];
                classicRows |= type == "classicRows";
                if (string.IsNullOrWhiteSpace(type)) throw new InvalidDataException("An overlay widget has no type.");
                if (type == "chart")
                {
                    string series = (string)node["series"];
                    frame |= series == "frametimes" || series == "framerates";
                    display |= series == "displaytimes";
                }
                // Group members are already expanded into native child widgets; disabled
                // members exist only in editor metadata and must never request sensor values.
                if (node["key"] != null)
                {
                    if (node["key"].Type != JTokenType.String)
                        throw new InvalidDataException("An overlay widget has an invalid telemetry key.");
                    string key = (string)node["key"];
                    if (string.IsNullOrWhiteSpace(key) || key.IndexOf('\0') >= 0 || Encoding.UTF8.GetByteCount(key) > 2048)
                        throw new InvalidDataException("An overlay widget has an invalid telemetry key.");
                    keys.Add(key);
                }
                if (node["children"] is JArray children)
                {
                    foreach (var child in children)
                    {
                        if (child is not JObject childNode)
                            throw new InvalidDataException("An overlay widget contains an invalid child.");
                        Inspect(childNode, depth + 1);
                    }
                }
                else if (node["children"] != null)
                    throw new InvalidDataException("Overlay widget children must be an array.");
            }
            Inspect((JObject)document["root"], 0);
            string authoringJson = document.ToString(Formatting.None);
            foreach (var metadata in document.Descendants().OfType<JProperty>().Where(property => property.Name == "editor").ToArray())
                metadata.Remove();
            return new OverlayRuntimeDesign
            {
                ProfileId = profileId,
                Name = name,
                TemplateJson = document.ToString(Formatting.None),
                AuthoringJson = authoringJson,
                MetricKeys = Array.AsReadOnly(keys.OrderBy(key => key, StringComparer.Ordinal).ToArray()),
                NeedsFrametimes = frame,
                NeedsDisplayTimes = display,
                HasClassicRows = classicRows
            };
        }

        internal static string Canonicalize(string json) => ParseDocument(json).ToString(Formatting.None);

        private static JObject ParseDocument(string json)
        {
            if (string.IsNullOrWhiteSpace(json) || json.IndexOf('\0') >= 0 || Encoding.UTF8.GetByteCount(json) > HookDesignChannel.PayloadCapacity)
                throw new InvalidDataException("The saved overlay design is empty or exceeds the 1 MiB limit.");
            using var reader = new JsonTextReader(new StringReader(json)) { MaxDepth = 64, DateParseHandling = DateParseHandling.None };
            var document = JObject.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
            if (reader.Read())
                throw new InvalidDataException("Unexpected content follows the saved overlay design.");
            if (document["version"]?.Type != JTokenType.Integer || document["version"].ToString() != "1" || document["root"] is not JObject)
                throw new InvalidDataException("The saved overlay design has an unsupported version or no native scene.");
            return document;
        }

        internal string CreateMetrics(OverlayTelemetrySnapshot snapshot, IReadOnlyDictionary<string, string> aliases)
        {
            var values = new JObject();
            foreach (string key in MetricKeys)
            {
                object value = null;
                if (snapshot != null && !snapshot.MetricValues.TryGetValue(key, out value) && aliases != null && aliases.TryGetValue(key, out string source))
                    snapshot.MetricValues.TryGetValue(source, out value);
                values[key] = Scalar(value);
            }
            string json = values.ToString(Formatting.None);
            if (Encoding.UTF8.GetByteCount(json) > HookDesignChannel.PayloadCapacity)
                throw new InvalidDataException("The selected overlay readings exceed the telemetry channel capacity.");
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
