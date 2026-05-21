using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace CapFrameX.Mcp.Tools
{
    /// <summary>
    /// Helpers for reading CapFrameX log files (Serilog Compact JSON, one entry per line).
    /// </summary>
    internal static class LogReader
    {
        private static readonly Regex CapFrameXLogPattern = new Regex(
            @"^CapFrameX(?:_\d+)?\.log$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex TemplateProperty = new Regex(
            @"\{(@?\w+)(?::[^}]+)?\}", RegexOptions.Compiled);

        /// <summary>
        /// Returns log files whose last-write timestamp is at or after the cutoff,
        /// ordered chronologically (oldest first). Reading them in order keeps
        /// timeline-style consumers' output naturally sorted.
        /// </summary>
        public static List<FileInfo> FindRelevantLogs(string folder, DateTime cutoffUtc)
        {
            try
            {
                var dir = new DirectoryInfo(folder);
                if (!dir.Exists) return new List<FileInfo>();

                return dir.EnumerateFiles("CapFrameX*.log")
                    .Where(f => CapFrameXLogPattern.IsMatch(f.Name))
                    .Where(f => f.LastWriteTimeUtc >= cutoffUtc)
                    .OrderBy(f => f.LastWriteTimeUtc)
                    .ToList();
            }
            catch
            {
                return new List<FileInfo>();
            }
        }

        public static IEnumerable<LogEntry> EnumerateEntries(FileInfo file)
        {
            // Open with FileShare.ReadWrite so a live writer doesn't block us.
            using (var fs = new FileStream(file.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var reader = new StreamReader(fs))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    LogEntry entry = null;
                    try { entry = LogEntry.Parse(line); }
                    catch { /* malformed line: skip */ }
                    if (entry != null) yield return entry;
                }
            }
        }

        public static string RenderTemplate(string template, IDictionary<string, JToken> properties)
        {
            if (string.IsNullOrEmpty(template) || properties == null || properties.Count == 0)
                return template ?? string.Empty;

            return TemplateProperty.Replace(template, m =>
            {
                var key = m.Groups[1].Value.TrimStart('@');
                if (properties.TryGetValue(key, out var val))
                    return val.Type == JTokenType.String
                        ? val.Value<string>()
                        : val.ToString(Newtonsoft.Json.Formatting.None);
                return m.Value;
            });
        }
    }

    internal sealed class LogEntry
    {
        public DateTime Timestamp;
        public string Level;
        public string MessageTemplate;
        public string Exception;
        public IDictionary<string, JToken> Properties;

        public string Render() => LogReader.RenderTemplate(MessageTemplate, Properties);

        public static LogEntry Parse(string json)
        {
            var jo = JObject.Parse(json);
            var entry = new LogEntry
            {
                Timestamp = jo["@t"]?.Value<DateTime>().ToUniversalTime() ?? DateTime.MinValue,
                Level = jo["@l"]?.Value<string>() ?? "Information",
                MessageTemplate = jo["@mt"]?.Value<string>() ?? string.Empty,
                Exception = jo["@x"]?.Value<string>(),
                Properties = new Dictionary<string, JToken>(StringComparer.Ordinal),
            };
            foreach (var prop in jo.Properties())
            {
                if (prop.Name.StartsWith("@")) continue;
                entry.Properties[prop.Name] = prop.Value;
            }
            return entry;
        }
    }
}
