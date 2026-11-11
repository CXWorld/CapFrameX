using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;

namespace CapFrameX.OSD.Integration
{
    public sealed class OverlayTelemetrySnapshot
    {
        public DateTime TimestampUtc { get; }

        /// <summary>Source IDs map to measured values; null means unavailable, never zero.</summary>
        public IReadOnlyDictionary<string, double?> Values { get; }

        /// <summary>All numeric and text sources as JSON-ready scalars; null means unavailable.</summary>
        public IReadOnlyDictionary<string, object> MetricValues { get; }

        public OverlayTelemetrySnapshot(DateTime timestampUtc, IDictionary<string, double?> values,
            IDictionary<string, object> additionalValues = null)
        {
            TimestampUtc = timestampUtc;
            Values = new ReadOnlyDictionary<string, double?>(
                new Dictionary<string, double?>(values, StringComparer.Ordinal));
            var merged = new Dictionary<string, object>(StringComparer.Ordinal);
            foreach (var pair in values) merged[pair.Key] = pair.Value;
            if (additionalValues != null)
                foreach (var pair in additionalValues)
                    merged[pair.Key] = pair.Value is string text ? BoundText(text) : pair.Value;
            MetricValues = new ReadOnlyDictionary<string, object>(merged);
        }

        private static string BoundText(string text)
        {
            // The native metric protocol caps each UTF-8 text value at 2048 bytes. One long
            // custom hardware name must not make it reject the entire telemetry snapshot.
            const int limit = 2048;
            if (Encoding.UTF8.GetByteCount(text) <= limit) return text;
            var builder = new StringBuilder();
            int bytes = 0;
            foreach (var rune in text.EnumerateRunes())
            {
                if (bytes + rune.Utf8SequenceLength > limit - 3) break;
                builder.Append(rune.ToString());
                bytes += rune.Utf8SequenceLength;
            }
            return builder.Append('…').ToString();
        }
    }
}
