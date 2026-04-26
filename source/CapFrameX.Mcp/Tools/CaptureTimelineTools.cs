using CapFrameX.Contracts.Configuration;
using CapFrameX.Mcp.Attributes;
using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace CapFrameX.Mcp.Tools
{
    [McpServerToolType]
    public class CaptureTimelineTools
    {
        // Priority-ordered classification: first matching keyword wins.
        // Specific terms must come before generic ones.
        private static readonly (string keyword, string eventType)[] EventMap =
        {
            ("PresentMon successfully started", "presentmon-started"),
            ("TryKillPresentMon",               "presentmon-shutdown"),
            ("PresentMon",                      "presentmon"),
            ("Hotkey",                          "hotkey"),
            ("CapturesFolder",                  "captures-folder"),
            ("SaveSessionRunsToFile",           "session-saved"),
            ("Loading data from",               "session-loaded"),
            ("session",                         "session"),
            ("Capture",                         "capture"),
            ("RTSS",                            "rtss"),
        };

        private readonly IPathService _paths;

        public CaptureTimelineTools(IPathService paths)
        {
            _paths = paths;
        }

        [McpServerTool(Name = "cfx_get_capture_timeline",
            Description = "Returns capture-related log events in chronological order: " +
                "hotkey presses, PresentMon start/stop, session save/load, errors. " +
                "Useful when a capture didn't behave as expected and you want to see the sequence of events.")]
        public CaptureTimelineResult GetCaptureTimeline(
            [Description("Look back N minutes (default 30).")] int sinceMinutes = 30,
            [Description("Optional process name filter — only include events that mention this process.")] string processName = null,
            [Description("Maximum number of events to return (default 50). Newest events are kept.")] int maxEvents = 50)
        {
            if (sinceMinutes <= 0) sinceMinutes = 30;
            if (maxEvents <= 0) maxEvents = 50;

            var cutoff = DateTime.UtcNow.AddMinutes(-sinceMinutes);
            var processFilter = string.IsNullOrWhiteSpace(processName) ? null : processName.Trim();

            var result = new CaptureTimelineResult { LookbackMinutes = sinceMinutes };

            var files = LogReader.FindRelevantLogs(_paths?.LogsFolder, cutoff);
            if (files.Count == 0)
            {
                result.LogPath = _paths?.LogsFolder ?? "(unknown)";
                return result;
            }
            result.LogPath = files[files.Count - 1].FullName;

            var events = new List<CaptureEvent>();
            foreach (var file in files)
            {
                foreach (var entry in LogReader.EnumerateEntries(file))
                {
                    if (entry.Timestamp < cutoff) continue;

                    var rendered = entry.Render();
                    var haystack = (rendered + " " + (entry.Exception ?? ""));

                    if (processFilter != null
                        && haystack.IndexOf(processFilter, StringComparison.OrdinalIgnoreCase) < 0)
                        continue;

                    var eventType = ClassifyEvent(haystack, entry.Level);
                    if (eventType == null) continue;

                    events.Add(new CaptureEvent
                    {
                        When = entry.Timestamp,
                        EventType = eventType,
                        Level = LevelToString(entry.Level),
                        Message = Truncate(rendered, 300),
                        Exception = Truncate(entry.Exception, 300),
                    });
                }
            }

            // Newest first, capped
            events.Sort((a, b) => b.When.CompareTo(a.When));
            if (events.Count > maxEvents) events.RemoveRange(maxEvents, events.Count - maxEvents);

            // Then re-sort chronologically for output (humans read top-to-bottom)
            events.Sort((a, b) => a.When.CompareTo(b.When));
            result.Events = events;
            return result;
        }

        private static string ClassifyEvent(string text, string level)
        {
            // Errors/warnings are always interesting events even without keyword match.
            bool isElevated = level == "Error" || level == "Fatal" || level == "Warning";

            for (int i = 0; i < EventMap.Length; i++)
            {
                if (text.IndexOf(EventMap[i].keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                    return EventMap[i].eventType;
            }
            return isElevated ? "error" : null;
        }

        private static string LevelToString(string level)
        {
            switch (level)
            {
                case "Fatal": return "fatal";
                case "Error": return "error";
                case "Warning": return "warning";
                case "Debug": return "debug";
                default: return "info";
            }
        }

        private static string Truncate(string s, int max)
        {
            if (string.IsNullOrEmpty(s) || s.Length <= max) return s;
            return s.Substring(0, max) + "…";
        }
    }
}
