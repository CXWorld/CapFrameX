using CapFrameX.Contracts.Configuration;
using CapFrameX.Mcp.Attributes;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace CapFrameX.Mcp.Tools
{
    [McpServerToolType]
    public class DiagnosticsTools
    {
        // Focus values that patterns and queries can use.
        public const string FocusGeneral = "general";
        public const string FocusCapture = "capture";
        public const string FocusSensors = "sensors";
        public const string FocusOverlay = "overlay";
        public const string FocusAll = "all";

        private readonly IPathService _paths;

        public DiagnosticsTools(IPathService paths)
        {
            _paths = paths;
        }

        [McpServerTool(Name = "cfx_diagnose_capture",
            Description = "Analyzes recent CapFrameX log files for capture-related failures. " +
                "Use when a capture didn't happen, was incomplete, or behaved unexpectedly. " +
                "Returns structured findings (severity, category, suggestion) plus a brief summary. " +
                "Reads across rolled log files within the lookback window.")]
        public DiagnosticsResult DiagnoseCapture(
            [Description("Look back N minutes (default 30). Use a longer window if the failure was earlier.")] int sinceMinutes = 30,
            [Description("Optional process name filter. If set, only entries that mention this process are considered.")] string processName = null,
            [Description("Maximum number of issues to return (default 20). Newest issues are kept.")] int maxIssues = 20)
        {
            return Scan(sinceMinutes, processName, maxIssues, focus: FocusCapture);
        }

        [McpServerTool(Name = "cfx_diagnose_general",
            Description = "Analyzes recent CapFrameX log files across capture, sensors, and overlay subsystems. " +
                "Use 'all' to cover everything, or scope to a single subsystem. " +
                "Returns structured findings with severity, category, and suggestions.")]
        public DiagnosticsResult DiagnoseGeneral(
            [Description("Focus area: 'capture', 'sensors', 'overlay', or 'all'. Default: 'all'.")] string focus = FocusAll,
            [Description("Look back N minutes (default 60).")] int sinceMinutes = 60,
            [Description("Maximum number of issues to return (default 30).")] int maxIssues = 30)
        {
            return Scan(sinceMinutes, processName: null, maxIssues, focus: NormalizeFocus(focus));
        }

        private DiagnosticsResult Scan(int sinceMinutes, string processName, int maxIssues, string focus)
        {
            if (sinceMinutes <= 0) sinceMinutes = 30;
            if (maxIssues <= 0) maxIssues = 20;

            var cutoff = DateTime.UtcNow.AddMinutes(-sinceMinutes);
            var processFilter = string.IsNullOrWhiteSpace(processName) ? null : processName.Trim();

            var result = new DiagnosticsResult
            {
                LookbackMinutes = sinceMinutes,
            };

            var files = LogReader.FindRelevantLogs(_paths?.LogsFolder, cutoff);
            if (files.Count == 0)
            {
                result.LogPath = _paths?.LogsFolder ?? "(unknown)";
                result.Summary = "No CapFrameX*.log file with entries in the lookback window was found.";
                return result;
            }

            // Reflect which files were considered (newest one is most useful for the LLM to know).
            result.LogPath = files[files.Count - 1].FullName;

            var activePatterns = Patterns.Where(p => MatchesFocus(p.Focus, focus)).ToArray();

            int scanned = 0;
            var issues = new List<DiagnosticIssue>();

            foreach (var file in files)
            {
                foreach (var entry in LogReader.EnumerateEntries(file))
                {
                    scanned++;
                    if (entry.Timestamp < cutoff) continue;

                    var rendered = entry.Render();
                    var haystack = (rendered + " " + (entry.Exception ?? "")).Trim();

                    if (processFilter != null
                        && haystack.IndexOf(processFilter, StringComparison.OrdinalIgnoreCase) < 0)
                        continue;

                    var pattern = MatchPattern(activePatterns, haystack);

                    bool isElevatedLevel = entry.Level == "Error" || entry.Level == "Fatal" || entry.Level == "Warning";
                    if (!isElevatedLevel && pattern == null) continue;

                    issues.Add(new DiagnosticIssue
                    {
                        When = entry.Timestamp,
                        Severity = SeverityFromLevel(entry.Level, pattern?.SeverityHint),
                        Category = pattern?.Category ?? "unclassified",
                        Message = TruncateForOutput(rendered, 500),
                        Suggestion = pattern?.Suggestion,
                        Exception = TruncateForOutput(entry.Exception, 500),
                    });
                }
            }

            result.EntriesScanned = scanned;
            issues.Sort((a, b) => b.When.CompareTo(a.When));   // newest first
            if (issues.Count > maxIssues) issues.RemoveRange(maxIssues, issues.Count - maxIssues);
            result.Issues = issues;
            result.IssuesFound = issues.Count;
            result.Summary = BuildSummary(issues, sinceMinutes, files.Count);

            return result;
        }

        private static string NormalizeFocus(string focus)
        {
            if (string.IsNullOrWhiteSpace(focus)) return FocusAll;
            switch (focus.Trim().ToLowerInvariant())
            {
                case FocusCapture: return FocusCapture;
                case FocusSensors: return FocusSensors;
                case FocusOverlay: return FocusOverlay;
                case FocusAll: return FocusAll;
                default: return FocusAll;
            }
        }

        private static bool MatchesFocus(string patternFocus, string requestedFocus)
        {
            if (requestedFocus == FocusAll) return true;
            if (patternFocus == FocusGeneral) return true;
            return patternFocus == requestedFocus;
        }

        private static LogPattern MatchPattern(LogPattern[] active, string text)
        {
            for (int i = 0; i < active.Length; i++)
                if (active[i].Matches(text)) return active[i];
            return null;
        }

        private static string SeverityFromLevel(string level, string severityHint)
        {
            if (!string.IsNullOrEmpty(severityHint)) return severityHint;
            switch (level)
            {
                case "Fatal": return "fatal";
                case "Error": return "error";
                case "Warning": return "warning";
                case "Debug": return "debug";
                default: return "info";
            }
        }

        private static string TruncateForOutput(string s, int max)
        {
            if (string.IsNullOrEmpty(s) || s.Length <= max) return s;
            return s.Substring(0, max) + "…";
        }

        private static string BuildSummary(List<DiagnosticIssue> issues, int sinceMinutes, int filesScanned)
        {
            if (issues.Count == 0)
                return $"No issues found in the last {sinceMinutes} minute(s) across {filesScanned} log file(s).";

            var byCategory = issues.GroupBy(i => i.Category)
                .OrderByDescending(g => g.Count())
                .Select(g => $"{g.Count()}× {g.Key}");
            return $"{issues.Count} issue(s) in the last {sinceMinutes} minute(s) across {filesScanned} log file(s): "
                   + string.Join(", ", byCategory) + ".";
        }

        private sealed class LogPattern
        {
            public string Category;
            public string Suggestion;
            public string SeverityHint;   // optional override; null = derive from log level
            public string Focus;          // FocusGeneral / Capture / Sensors / Overlay
            private readonly Func<string, bool> _matcher;

            public LogPattern(string category, string focus, string suggestion, string severityHint, Func<string, bool> matcher)
            {
                Category = category;
                Focus = focus;
                Suggestion = suggestion;
                SeverityHint = severityHint;
                _matcher = matcher;
            }

            public bool Matches(string text) => _matcher(text);
        }

        // Pattern library — each pattern is tagged with a focus area.
        // FocusGeneral patterns apply to every diagnose call.
        private static readonly LogPattern[] Patterns =
        {
            // ─── general ──────────────────────────────────────────────────
            new LogPattern("permission", FocusGeneral,
                "Access denied. Run CapFrameX as administrator (right-click shortcut → Run as administrator).",
                "error",
                t => Has(t, "access is denied") || Has(t, "access denied")
                  || Has(t, "elevation") || Has(t, "UnauthorizedAccessException")),

            new LogPattern("dependency-missing", FocusGeneral,
                "A required runtime dependency is missing (.NET Framework or VC++ Redistributable). Install missing prerequisites and restart.",
                "error",
                t => Has(t, "Missing Dependencies") || Has(t, "VCRedist")
                  || Has(t, "MissingDotNetFrameworkVersion")),

            new LogPattern("disk-full", FocusGeneral,
                "Insufficient disk space. Free space or change ObservedDirectory in settings.",
                "error",
                t => (Has(t, "disk") && Has(t, "full")) || Has(t, "There is not enough space")),

            new LogPattern("mcp-tool-error", FocusGeneral,
                "An MCP tool invocation failed. Check the exception detail for the offending tool name and arguments. " +
                "Often caused by missing required parameters or stale record paths.",
                "error",
                t => Has(t, "MCP tool ") && Has(t, "threw")),

            // ─── capture ──────────────────────────────────────────────────
            new LogPattern("etw-conflict", FocusCapture,
                "NVIDIA FrameView SDK is running an ETW session that blocks PresentMon. Stop the FrameViewService or uninstall the FrameView SDK and restart CapFrameX.",
                "warning",
                t => Has(t, "FrameViewService") || Has(t, "FrameView SDK")),

            new LogPattern("presentmon-shutdown-hang", FocusCapture,
                "PresentMon failed to shut down cleanly from a previous run. Usually harmless; if subsequent captures fail, restart CapFrameX as admin.",
                "warning",
                t => Has(t, "TryKillPresentMon") || Has(t, "Error while shutting down PresentMon")),

            new LogPattern("presentmon-failure", FocusCapture,
                "PresentMon could not capture frames. Common causes: anti-cheat blocked PresentMon, ETW session conflict, missing trace permissions.",
                "error",
                t => Has(t, "PresentMon") && (Has(t, "error") || Has(t, "fail") || Has(t, "exception"))),

            new LogPattern("process-blacklisted", FocusCapture,
                "The target process is on the blacklist. Open CapFrameX → Settings → Process List and remove it.",
                "warning",
                t => Has(t, "blacklist") || Has(t, "ignored process")),

            new LogPattern("process-not-found", FocusCapture,
                "Target process was not running when capture was triggered. Start the game first, then trigger the capture.",
                "warning",
                t => Has(t, "process not found") || Has(t, "no process")
                  || Has(t, "process is not running")),

            new LogPattern("hotkey-locked", FocusCapture,
                "The capture hotkey was triggered while the capture service was locked. Wait for the previous capture to finish or restart CapFrameX.",
                "warning",
                t => Has(t, "Hotkey") && Has(t, "lock") && Has(t, "true")),

            new LogPattern("file-write-failure", FocusCapture,
                "Could not write the capture file. Check that the records directory exists and is writable.",
                "error",
                t => (Has(t, "Error") || Has(t, "Failed")) && (Has(t, "write") || Has(t, "save")) && Has(t, "file")),

            new LogPattern("anti-cheat", FocusCapture,
                "Capture failed in a way consistent with anti-cheat blocking PresentMon (VAC, EAC, BattlEye, Vanguard). Try a single-player title or built-in benchmark mode.",
                "error",
                t => Has(t, "EasyAntiCheat") || Has(t, "BattlEye") || Has(t, "Vanguard")
                  || Has(t, "anti-cheat") || Has(t, "anticheat")),

            // ─── sensors ──────────────────────────────────────────────────
            new LogPattern("sensor-service-error", FocusSensors,
                "Sensor service failed. Sensor readings (CPU/GPU temp, power, clocks) may be missing from captures. Restart CapFrameX as admin to re-initialize.",
                "error",
                t => (Has(t, "sensor service") || Has(t, "ISensorService") || Has(t, "SensorService"))
                  && (Has(t, "error") || Has(t, "fail") || Has(t, "exception"))),

            new LogPattern("hwinfo-not-running", FocusSensors,
                "HWInfo64 is not running or its shared memory interface is unavailable. Start HWInfo64 in shared-memory mode (Settings → Shared Memory Support).",
                "warning",
                t => Has(t, "HwInfo") && (Has(t, "not running") || Has(t, "shared memory") || Has(t, "not available"))),

            new LogPattern("lhm-error", FocusSensors,
                "LibreHardwareMonitor encountered an error. Some sensor readings may be missing. Common cause: insufficient driver permissions.",
                "warning",
                t => Has(t, "LibreHardwareMonitor")
                  && (Has(t, "error") || Has(t, "fail") || Has(t, "exception"))),

            // ─── overlay ──────────────────────────────────────────────────
            new LogPattern("rtss-not-running", FocusOverlay,
                "RTSS (RivaTuner Statistics Server) is not running. Start RTSS — required for the on-screen overlay.",
                "warning",
                t => Has(t, "RTSS")
                  && (Has(t, "not running") || Has(t, "not found") || Has(t, "could not connect"))),

            new LogPattern("rtss-error", FocusOverlay,
                "RTSS communication error. The overlay may be partially or fully broken until RTSS is restarted.",
                "error",
                t => (Has(t, "RTSS") || Has(t, "RivaTuner"))
                  && (Has(t, "error") || Has(t, "fail") || Has(t, "exception"))),

            new LogPattern("overlay-service-error", FocusOverlay,
                "Overlay service failed. The OSD may not show or may show incorrect data.",
                "error",
                t => (Has(t, "overlay service") || Has(t, "IOverlayService") || Has(t, "OverlayService"))
                  && (Has(t, "error") || Has(t, "fail") || Has(t, "exception"))),
        };

        private static bool Has(string haystack, string needle) =>
            haystack.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
