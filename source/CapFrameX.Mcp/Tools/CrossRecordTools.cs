using CapFrameX.Contracts.Configuration;
using CapFrameX.Contracts.Data;
using CapFrameX.Data.Session.Contracts;
using CapFrameX.Mcp.Attributes;
using CapFrameX.Statistics.NetStandard.Contracts;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;

namespace CapFrameX.Mcp.Tools
{
    [McpServerToolType]
    public class CrossRecordTools
    {
        private readonly RecordTools _recordTools;
        private readonly IStatisticProvider _stats;
        private readonly IAppConfiguration _config;
        private readonly ISystemInfo _systemInfo;

        public CrossRecordTools(RecordTools recordTools, IStatisticProvider stats, IAppConfiguration config, ISystemInfo systemInfo)
        {
            _recordTools = recordTools;
            _stats = stats;
            _config = config;
            _systemInfo = systemInfo;
        }

        // ─── cfx_find_regressions ────────────────────────────────────────────

        [McpServerTool(Name = "cfx_find_regressions",
            Description = "Compares all records of a given process against a baseline record on a single metric. " +
                "If baselineId is omitted, the oldest record for the process is used. " +
                "Returns each record sorted by date with absolute and percentage delta — useful for 'has FPS regressed since driver X?'. " +
                "FPS source follows AppSettings.UseDisplayChangeMetrics by default (matches Analysis tab); override with useDisplayChangeMetrics.")]
        public RegressionsResult FindRegressions(
            [Description("Process name to filter by (case-insensitive). Required.")] string processName,
            [Description("Optional baseline record id (absolute path). Omit to use the oldest record for processName.")] string baselineId = null,
            [Description("Metric name to track. Default 'Average'. See cfx_get_metrics for the list.")] string metric = "Average",
            [Description("Run index (0-based) used in every record. Default: 0.")] int runIndex = 0,
            [Description("Maximum records returned (newest first). 0 = unlimited.")] int maxResults = 0,
            [Description("Override the FPS source. true = display-change times, false = present times, omit = follow AppSettings.UseDisplayChangeMetrics.")] bool? useDisplayChangeMetrics = null)
        {
            if (string.IsNullOrWhiteSpace(processName))
                throw new ArgumentException("processName must be provided", nameof(processName));
            if (!Enum.TryParse(metric ?? "Average", ignoreCase: true, out EMetric eMetric) || eMetric == EMetric.None)
                throw new ArgumentException($"Metric '{metric}' is not supported.", nameof(metric));

            bool useDisplay = useDisplayChangeMetrics ?? _config.UseDisplayChangeMetrics;
            var records = LoadRecordsForProcess(processName);
            if (records.Count == 0)
                return new RegressionsResult { ProcessName = processName, Metric = eMetric.ToString(), MetricSource = FrametimeSequenceHelper.SourceLabel(useDisplay) };

            int rounding = _config.FpsValuesRoundingDigits > 0 ? _config.FpsValuesRoundingDigits : 2;

            // Compute metric per record (skip records with no data at runIndex).
            // Track source-fit warnings so we can surface them in the result.
            var samples = new List<(string id, DateTime when, ISession session, double value)>();
            string firstWarning = null;
            int warningCount = 0;
            foreach (var (id, session) in records)
            {
                if (session.Runs == null || session.Runs.Count <= runIndex) continue;
                var sessionRun = session.Runs[runIndex];
                var sequence = FrametimeSequenceHelper.ResolveSequence(sessionRun, useDisplay);
                if (sequence == null || sequence.Count == 0) continue;

                double v;
                try { v = _stats.GetFpsMetricValue(sequence, eMetric); }
                catch { continue; }
                if (double.IsNaN(v)) continue;

                samples.Add((id, session.Info?.CreationDate ?? DateTime.MinValue, session, Math.Round(v, rounding)));

                var w = FrametimeSequenceHelper.GetSourceWarning(sessionRun, _stats, useDisplay);
                if (!string.IsNullOrEmpty(w))
                {
                    if (firstWarning == null) firstWarning = w;
                    warningCount++;
                }
            }
            if (samples.Count == 0)
                return new RegressionsResult { ProcessName = processName, Metric = eMetric.ToString(), MetricSource = FrametimeSequenceHelper.SourceLabel(useDisplay) };

            // Establish baseline.
            string resolvedBaseline = baselineId;
            (string id, DateTime when, ISession session, double value) baseline;
            if (!string.IsNullOrEmpty(baselineId))
            {
                baseline = samples.FirstOrDefault(s => string.Equals(s.id, baselineId, StringComparison.OrdinalIgnoreCase));
                if (baseline.id == null)
                    throw new InvalidOperationException($"Baseline id '{baselineId}' not found in samples for '{processName}'.");
            }
            else
            {
                baseline = samples.OrderBy(s => s.when).First();
                resolvedBaseline = baseline.id;
            }

            string aggregatedWarning = firstWarning;
            if (firstWarning != null && warningCount > 1)
                aggregatedWarning = $"{warningCount} of {samples.Count} records trigger this warning. Sample: {firstWarning}";
            var result = new RegressionsResult
            {
                ProcessName = processName,
                BaselineId = resolvedBaseline,
                BaselineRecordedAt = baseline.when,
                BaselineValue = baseline.value,
                Metric = eMetric.ToString(),
                Unit = "fps",
                MetricSource = FrametimeSequenceHelper.SourceLabel(useDisplay),
                MetricSourceWarning = aggregatedWarning,
            };

            foreach (var s in samples.OrderByDescending(s => s.when))
            {
                double deltaAbs = s.value - baseline.value;
                double deltaPct = baseline.value != 0 ? (deltaAbs / baseline.value * 100.0) : double.NaN;

                result.Samples.Add(new RegressionSample
                {
                    RecordId = s.id,
                    RecordedAt = s.when,
                    Value = s.value,
                    DeltaAbs = Math.Round(deltaAbs, rounding),
                    DeltaPct = double.IsNaN(deltaPct) ? deltaPct : Math.Round(deltaPct, 2),
                    Gpu = s.session.Info?.GPU,
                    GpuDriver = s.session.Info?.GPUDriverVersion,
                    Comment = s.session.Info?.Comment,
                });

                if (maxResults > 0 && result.Samples.Count >= maxResults) break;
            }
            return result;
        }

        // ─── cfx_detect_system_drift ─────────────────────────────────────────

        [McpServerTool(Name = "cfx_detect_system_drift",
            Description = "Compares the system snapshot stored in a record against the live system info as CapFrameX sees it now. " +
                "Returns per-field differences. Use to flag records that aren't comparable to the current setup (different GPU, driver, RAM).")]
        public SystemDriftResult DetectSystemDrift(
            [Description("Record id (absolute file path) from cfx_list_records")] string id)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("id must be provided", nameof(id));
            if (!File.Exists(id)) throw new FileNotFoundException("Record not found", id);

            var session = _recordTools.SafeLoad(id);
            if (session?.Info == null)
                throw new InvalidOperationException("Could not load record info: " + id);

            try { _systemInfo.SetSystemInfosStatus(); } catch { /* keep cached */ }

            var info = session.Info;
            string SafeStr(Func<string> fn) { try { return fn(); } catch { return null; } }
            string curCpu = SafeStr(() => _systemInfo.GetProcessorName());
            string curGpu = SafeStr(() => _systemInfo.GetGraphicCardName());
            string curRam = SafeStr(() => _systemInfo.GetSystemRAMInfoName());
            string curMb = SafeStr(() => _systemInfo.GetMotherboardName());
            string curOs = SafeStr(() => _systemInfo.GetOSVersion());

            var fields = new[]
            {
                ("Cpu", info.Processor, curCpu),
                ("Gpu", info.GPU, curGpu),
                ("Ram", info.SystemRam, curRam),
                ("Motherboard", info.Motherboard, curMb),
                ("Os", info.OS, curOs),
                ("GpuDriver", info.GPUDriverVersion, null),
            };

            var result = new SystemDriftResult { RecordId = id, RecordedAt = info.CreationDate };
            foreach (var (field, recVal, curVal) in fields)
            {
                bool different = curVal != null && !string.Equals((recVal ?? "").Trim(), (curVal ?? "").Trim(),
                    StringComparison.OrdinalIgnoreCase);
                result.Drift.Add(new SystemDriftField
                {
                    Field = field,
                    RecordValue = recVal,
                    CurrentValue = curVal,
                    Different = different,
                });
            }
            result.AnyDifferent = result.Drift.Any(d => d.Different);
            return result;
        }

        // ─── cfx_find_outliers ───────────────────────────────────────────────

        [McpServerTool(Name = "cfx_find_outliers",
            Description = "Finds outlier records for a given process+metric using a robust z-score (median ± MAD). " +
                "Records with |z| > zThreshold are returned. Useful for spotting bad runs in a benchmark series. " +
                "FPS source follows AppSettings.UseDisplayChangeMetrics by default (matches Analysis tab); override with useDisplayChangeMetrics.")]
        public OutliersResult FindOutliers(
            [Description("Process name to filter by (case-insensitive). Required.")] string processName,
            [Description("Metric name. Default 'Average'.")] string metric = "Average",
            [Description("Run index (0-based) used in every record. Default: 0.")] int runIndex = 0,
            [Description("Robust z-score threshold for flagging an outlier. Default 2.5.")] double zThreshold = 2.5,
            [Description("Override the FPS source. true = display-change times, false = present times, omit = follow AppSettings.UseDisplayChangeMetrics.")] bool? useDisplayChangeMetrics = null)
        {
            if (string.IsNullOrWhiteSpace(processName))
                throw new ArgumentException("processName must be provided", nameof(processName));
            if (!Enum.TryParse(metric ?? "Average", ignoreCase: true, out EMetric eMetric) || eMetric == EMetric.None)
                throw new ArgumentException($"Metric '{metric}' is not supported.", nameof(metric));
            if (zThreshold <= 0) zThreshold = 2.5;

            bool useDisplay = useDisplayChangeMetrics ?? _config.UseDisplayChangeMetrics;
            var records = LoadRecordsForProcess(processName);
            int rounding = _config.FpsValuesRoundingDigits > 0 ? _config.FpsValuesRoundingDigits : 2;

            var samples = new List<(string id, DateTime when, double value)>();
            string firstWarning = null;
            int warningCount = 0;
            foreach (var (id, session) in records)
            {
                if (session.Runs == null || session.Runs.Count <= runIndex) continue;
                var sessionRun = session.Runs[runIndex];
                var sequence = FrametimeSequenceHelper.ResolveSequence(sessionRun, useDisplay);
                if (sequence == null || sequence.Count == 0) continue;
                double v;
                try { v = _stats.GetFpsMetricValue(sequence, eMetric); }
                catch { continue; }
                if (double.IsNaN(v)) continue;
                samples.Add((id, session.Info?.CreationDate ?? DateTime.MinValue, Math.Round(v, rounding)));

                var w = FrametimeSequenceHelper.GetSourceWarning(sessionRun, _stats, useDisplay);
                if (!string.IsNullOrEmpty(w))
                {
                    if (firstWarning == null) firstWarning = w;
                    warningCount++;
                }
            }

            string aggregatedWarning = firstWarning;
            if (firstWarning != null && warningCount > 1)
                aggregatedWarning = $"{warningCount} of {samples.Count} records trigger this warning. Sample: {firstWarning}";
            var result = new OutliersResult
            {
                ProcessName = processName,
                Metric = eMetric.ToString(),
                ZThreshold = zThreshold,
                TotalSamples = samples.Count,
                MetricSource = FrametimeSequenceHelper.SourceLabel(useDisplay),
                MetricSourceWarning = aggregatedWarning,
            };
            if (samples.Count < 4) return result;  // not enough to detect outliers reliably

            var values = samples.Select(s => s.value).OrderBy(v => v).ToList();
            double median = values[values.Count / 2];
            var mad = values.Select(v => Math.Abs(v - median)).OrderBy(v => v).ToList();
            double madValue = mad[mad.Count / 2];
            // Standard 1.4826 scale factor to make MAD ~ stdev for normal data.
            double scale = madValue > 1e-9 ? 1.4826 * madValue : 0;

            foreach (var s in samples)
            {
                double z = scale > 0 ? (s.value - median) / scale : 0;
                if (Math.Abs(z) <= zThreshold) continue;
                result.Outliers.Add(new OutlierSample
                {
                    RecordId = s.id,
                    RecordedAt = s.when,
                    Value = s.value,
                    ZScore = Math.Round(z, 3),
                });
            }
            result.Outliers = result.Outliers.OrderByDescending(o => Math.Abs(o.ZScore)).ToList();
            result.Median = median;
            result.MedianAbsDeviation = Math.Round(madValue, rounding);
            return result;
        }

        // ─── helpers ─────────────────────────────────────────────────────────

        private List<(string id, ISession session)> LoadRecordsForProcess(string processName)
        {
            var dir = _recordTools.ResolveRecordsDirectory();
            var hits = new List<(string, ISession)>();
            if (dir == null || !dir.Exists) return hits;

            foreach (var fi in dir.EnumerateFiles("*.json", SearchOption.TopDirectoryOnly))
            {
                var session = _recordTools.SafeLoad(fi.FullName);
                if (session?.Info == null) continue;
                if (!string.Equals(session.Info.ProcessName, processName, StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(session.Info.GameName, processName, StringComparison.OrdinalIgnoreCase))
                    continue;
                hits.Add((fi.FullName, session));
            }
            return hits;
        }
    }
}
