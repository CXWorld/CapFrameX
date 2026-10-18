using System;
using System.Collections.Generic;
using System.Globalization;
using CapFrameX.Statistics.NetStandard;

namespace CapFrameX.OSD.Integration
{
    /// <summary>
    /// Sliding-window averages of telemetry readings for the keys a design requests as
    /// <c>avg:{seconds}:{source}</c> (heatmap cells). The renderers only read the values; every
    /// host that feeds a design (runtime publisher, designer preview) averages with this class.
    /// </summary>
    public sealed class OverlayMovingAverages
    {
        /// <summary>Prefix of an averaged key, as the editor writes it.</summary>
        public const string KeyPrefix = "avg:";
        private const int MaximumSeconds = 3600;

        private readonly Dictionary<string, Entry> _entries = new Dictionary<string, Entry>(StringComparer.Ordinal);

        /// <summary>The averaged key of <paramref name="source"/> over <paramref name="seconds"/>.</summary>
        public static string Key(int seconds, string source)
            => KeyPrefix + seconds.ToString(CultureInfo.InvariantCulture) + ":" + source;

        /// <summary>
        /// Reads an averaged key. The source is everything after the window, so source ids that
        /// contain colons ("sensor:...") stay intact.
        /// </summary>
        public static bool TryParseKey(string key, out int seconds, out string source)
        {
            seconds = 0;
            source = null;
            if (key == null || !key.StartsWith(KeyPrefix, StringComparison.Ordinal)) return false;
            int separator = key.IndexOf(':', KeyPrefix.Length);
            if (separator <= KeyPrefix.Length || separator == key.Length - 1) return false;
            if (!int.TryParse(key.Substring(KeyPrefix.Length, separator - KeyPrefix.Length), NumberStyles.None,
                    CultureInfo.InvariantCulture, out seconds) || seconds < 1 || seconds > MaximumSeconds)
                return false;
            source = key.Substring(separator + 1);
            return true;
        }

        /// <summary>
        /// Averages exactly the averaged keys among <paramref name="keys"/>. Averages that stay
        /// keep their samples; the others are dropped.
        /// </summary>
        public void Track(IEnumerable<string> keys)
        {
            var wanted = new HashSet<string>(StringComparer.Ordinal);
            foreach (string key in keys ?? Array.Empty<string>())
            {
                if (!TryParseKey(key, out int seconds, out string source)) continue;
                wanted.Add(key);
                if (!_entries.ContainsKey(key))
                    _entries[key] = new Entry(source, seconds * 1000.0);
            }
            foreach (string key in new List<string>(_entries.Keys))
                if (!wanted.Contains(key)) _entries.Remove(key);
        }

        /// <summary>Whether any averaged key is tracked.</summary>
        public bool IsEmpty => _entries.Count == 0;

        /// <summary>
        /// Adds the snapshot's readings. A source missing from the snapshot is looked up through
        /// its semantic alias, as <c>CreateMetrics</c> resolves design keys.
        /// </summary>
        public void Add(OverlayTelemetrySnapshot snapshot, IReadOnlyDictionary<string, string> aliases = null)
        {
            if (snapshot == null) return;
            double time = ToMilliseconds(snapshot.TimestampUtc);
            foreach (var entry in _entries.Values)
            {
                if (!snapshot.Values.TryGetValue(entry.Source, out double? value) && aliases != null
                    && aliases.TryGetValue(entry.Source, out string alias))
                    snapshot.Values.TryGetValue(alias, out value);
                if (value.HasValue) entry.Add(time, value.Value);
            }
        }

        /// <summary>
        /// The current average of a tracked key: true with null when its window holds no reading,
        /// false when the key is not tracked.
        /// </summary>
        public bool TryGetAverage(string key, DateTime nowUtc, out double? average)
        {
            average = null;
            if (key == null || !_entries.TryGetValue(key, out var entry)) return false;
            average = entry.Current(ToMilliseconds(nowUtc));
            return true;
        }

        /// <summary>Writes every tracked average into <paramref name="values"/>; null when empty.</summary>
        public void CopyTo(IDictionary<string, object> values, DateTime nowUtc)
        {
            foreach (var pair in _entries)
            {
                double? average = pair.Value.Current(ToMilliseconds(nowUtc));
                values[pair.Key] = average.HasValue ? (object)average.Value : null;
            }
        }

        /// <summary>Drops every sample but keeps the tracked keys.</summary>
        public void Clear()
        {
            foreach (var entry in _entries.Values) entry.Clear();
        }

        private static double ToMilliseconds(DateTime utc) => utc.Ticks / (double)TimeSpan.TicksPerMillisecond;

        // A window shorter than the interval between two readings would fall empty before the next
        // one arrives and blink its cell to "no reading" (a 1 s window at the default 1 s overlay
        // refresh). The newest reading therefore stays current for twice the gap between the last
        // two readings; a source that stops reporting still turns empty soon after.
        private sealed class Entry
        {
            private readonly SlidingWindowAverage _average;
            private double _newestTime = double.NaN, _newestValue, _lastGap;

            public Entry(string source, double windowMilliseconds)
            {
                Source = source;
                _average = new SlidingWindowAverage(windowMilliseconds);
            }

            public string Source { get; }

            public void Add(double time, double value)
            {
                if (!double.IsFinite(time) || !double.IsFinite(value)) return;
                _lastGap = time > _newestTime ? time - _newestTime : 0;
                _newestTime = time;
                _newestValue = value;
                _average.Add(time, value);
            }

            public double? Current(double now)
            {
                double? average = _average.Average(now);
                if (average.HasValue || double.IsNaN(_newestTime)) return average;
                return now - _newestTime < 2 * _lastGap ? _newestValue : (double?)null;
            }

            public void Clear()
            {
                _average.Clear();
                _newestTime = double.NaN;
                _lastGap = 0;
            }
        }
    }
}
