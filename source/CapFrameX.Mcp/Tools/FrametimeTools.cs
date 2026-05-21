using CapFrameX.Contracts.Configuration;
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
    public class FrametimeTools
    {
        private readonly RecordTools _recordTools;
        private readonly IStatisticProvider _stats;
        private readonly IAppConfiguration _config;

        public FrametimeTools(RecordTools recordTools, IStatisticProvider stats, IAppConfiguration config)
        {
            _recordTools = recordTools;
            _stats = stats;
            _config = config;
        }

        // ─── cfx_get_frametimes ──────────────────────────────────────────────

        [McpServerTool(Name = "cfx_get_frametimes",
            Description = "Returns the frametime time series of a record run. Optional downsampling collapses each bucket to its mean. " +
                "Use this when aggregate metrics aren't enough — e.g. to find when stutters happened or to inspect the curve shape. " +
                "Defaults to MsBetweenPresents (engine pipeline view); pass useDisplayChangeMetrics=true for the display-side view.")]
        public FrametimesResult GetFrametimes(
            [Description("Record id (absolute file path) from cfx_list_records")] string id,
            [Description("Run index within the record (0-based). Default: 0.")] int runIndex = 0,
            [Description("Downsampled rate in Hz (samples per second). 0 = no downsampling. Recommended 30 for AI consumption.")] double downsampleHz = 0,
            [Description("Override the source. true = MsBetweenDisplayChange, false (default) = MsBetweenPresents.")] bool useDisplayChangeMetrics = false)
        {
            var (run, _) = LoadRun(id, runIndex);
            bool useDisplay = useDisplayChangeMetrics;
            var frametimesMs = useDisplay
                ? (run.CaptureData?.MsBetweenDisplayChange ?? run.CaptureData?.MsBetweenPresents)
                : run.CaptureData?.MsBetweenPresents;
            if (frametimesMs == null || frametimesMs.Length == 0)
                return new FrametimesResult { RecordId = id, RunIndex = runIndex };

            var times = BuildTimeAxis(run.CaptureData);

            var result = new FrametimesResult
            {
                RecordId = id,
                RunIndex = runIndex,
                FrameCount = frametimesMs.Length,
                DurationSec = times.Count == 0 ? 0 : Math.Round(times[times.Count - 1], 3),
            };

            if (downsampleHz <= 0 || frametimesMs.Length == 0)
            {
                result.SampleHz = 0; // 0 means raw
                for (int i = 0; i < frametimesMs.Length; i++)
                    result.Points.Add(new FrametimePoint { TSec = Math.Round(times[i], 4), FrametimeMs = Math.Round(frametimesMs[i], 3) });
                return result;
            }

            double bucketSec = 1.0 / downsampleHz;
            double duration = result.DurationSec;
            int bucketCount = Math.Max(1, (int)Math.Ceiling(duration * downsampleHz));
            var sumPerBucket = new double[bucketCount];
            var countPerBucket = new int[bucketCount];

            for (int i = 0; i < frametimesMs.Length; i++)
            {
                int b = (int)(times[i] / bucketSec);
                if (b < 0) b = 0;
                if (b >= bucketCount) b = bucketCount - 1;
                sumPerBucket[b] += frametimesMs[i];
                countPerBucket[b]++;
            }

            for (int b = 0; b < bucketCount; b++)
            {
                if (countPerBucket[b] == 0) continue;
                result.Points.Add(new FrametimePoint
                {
                    TSec = Math.Round(b * bucketSec, 4),
                    FrametimeMs = Math.Round(sumPerBucket[b] / countPerBucket[b], 3),
                });
            }
            result.SampleHz = downsampleHz;
            return result;
        }

        // ─── cfx_find_stutters ───────────────────────────────────────────────

        [McpServerTool(Name = "cfx_find_stutters",
            Description = "Detects frametime spikes — frames whose time exceeds a moving-median baseline by a configurable factor. " +
                "Returns each stutter with its timestamp, frametime, and severity (= frametimeMs / movingMedian). " +
                "Default factor 2.0 and minMs 8 give good signal at typical 60–144 FPS. " +
                "FPS source follows AppSettings.UseDisplayChangeMetrics by default — important for Frame Generation captures, " +
                "where present-time spikes are dominated by alternating real/generated frames and would produce thousands of false positives. " +
                "Override with useDisplayChangeMetrics if needed.")]
        public StuttersResult FindStutters(
            [Description("Record id (absolute file path) from cfx_list_records")] string id,
            [Description("Run index within the record (0-based). Default: 0.")] int runIndex = 0,
            [Description("Spike factor relative to the rolling median. Default 2.0.")] double factorVsMedian = 2.0,
            [Description("Minimum frametime (ms) for a frame to qualify as a stutter (suppresses tiny absolute spikes). Default 8.")] double minMs = 8.0,
            [Description("Window size (frames) for the rolling median. Default 51.")] int windowFrames = 51,
            [Description("Maximum stutters returned (newest first by t). 0 = unlimited.")] int maxResults = 200,
            [Description("Override the source. true = display-change times, false = present times, omit = follow AppSettings.UseDisplayChangeMetrics.")] bool? useDisplayChangeMetrics = null)
        {
            var (run, _) = LoadRun(id, runIndex);
            bool useDisplay = useDisplayChangeMetrics ?? _config.UseDisplayChangeMetrics;
            var raw = FrametimeSequenceHelper.ResolveRawSequence(run, useDisplay);
            var rawTimes = BuildTimeAxis(run.CaptureData);
            if (raw == null || raw.Length == 0)
                return new StuttersResult
                {
                    RecordId = id, RunIndex = runIndex, FactorVsMedian = factorVsMedian, MinMs = minMs,
                    MetricSource = FrametimeSequenceHelper.SourceLabel(useDisplay),
                };

            // When using display-times, drop dropped-frame entries (value <= 0). Both arrays must
            // stay aligned for accurate timestamps in the output.
            var ft = new List<double>(raw.Length);
            var times = new List<double>(raw.Length);
            for (int i = 0; i < raw.Length; i++)
            {
                var v = raw[i];
                if (v <= 0 || double.IsNaN(v)) continue;
                ft.Add(v);
                times.Add(i < rawTimes.Count ? rawTimes[i] : 0);
            }
            if (ft.Count == 0)
                return new StuttersResult
                {
                    RecordId = id, RunIndex = runIndex, FactorVsMedian = factorVsMedian, MinMs = minMs,
                    MetricSource = FrametimeSequenceHelper.SourceLabel(useDisplay),
                };

            int half = Math.Max(1, windowFrames / 2);
            var result = new StuttersResult
            {
                RecordId = id,
                RunIndex = runIndex,
                FactorVsMedian = factorVsMedian,
                MinMs = minMs,
                WindowFrames = windowFrames,
                MetricSource = FrametimeSequenceHelper.SourceLabel(useDisplay),
                MetricSourceWarning = FrametimeSequenceHelper.GetSourceWarning(run, _stats, useDisplay),
            };

            var window = new List<double>(windowFrames);
            for (int i = 0; i < ft.Count; i++)
            {
                int from = Math.Max(0, i - half);
                int to = Math.Min(ft.Count - 1, i + half);
                window.Clear();
                for (int j = from; j <= to; j++) window.Add(ft[j]);
                window.Sort();
                double median = window[window.Count / 2];

                double cur = ft[i];
                if (cur < minMs) continue;
                if (median <= 0) continue;
                if (cur < factorVsMedian * median) continue;

                result.Stutters.Add(new StutterEvent
                {
                    TSec = Math.Round(times[i], 4),
                    FrametimeMs = Math.Round(cur, 3),
                    MovingMedianMs = Math.Round(median, 3),
                    Severity = Math.Round(cur / median, 3),
                });
            }

            result.TotalCount = result.Stutters.Count;
            if (maxResults > 0 && result.Stutters.Count > maxResults)
            {
                result.Stutters = result.Stutters
                    .OrderByDescending(s => s.Severity)
                    .Take(maxResults)
                    .OrderBy(s => s.TSec)
                    .ToList();
            }
            return result;
        }

        // ─── cfx_find_freezes ────────────────────────────────────────────────

        [McpServerTool(Name = "cfx_find_freezes",
            Description = "Returns frames whose frametime exceeds an absolute threshold — i.e. real stalls (loading hitches, shader compilation, alt-tab freezes). " +
                "Differs from cfx_find_stutters by using an absolute ms cutoff rather than a relative spike. " +
                "FPS source follows AppSettings.UseDisplayChangeMetrics by default; override with useDisplayChangeMetrics if needed.")]
        public FreezesResult FindFreezes(
            [Description("Record id (absolute file path) from cfx_list_records")] string id,
            [Description("Run index within the record (0-based). Default: 0.")] int runIndex = 0,
            [Description("Frametime threshold in ms — frames slower than this are reported. Default 100.")] double minMs = 100.0,
            [Description("Maximum freezes returned. 0 = unlimited.")] int maxResults = 100,
            [Description("Override the source. true = display-change times, false = present times, omit = follow AppSettings.UseDisplayChangeMetrics.")] bool? useDisplayChangeMetrics = null)
        {
            var (run, _) = LoadRun(id, runIndex);
            bool useDisplay = useDisplayChangeMetrics ?? _config.UseDisplayChangeMetrics;
            var raw = FrametimeSequenceHelper.ResolveRawSequence(run, useDisplay);
            var rawTimes = BuildTimeAxis(run.CaptureData);
            if (raw == null || raw.Length == 0)
                return new FreezesResult
                {
                    RecordId = id, RunIndex = runIndex, MinMs = minMs,
                    MetricSource = FrametimeSequenceHelper.SourceLabel(useDisplay),
                };

            var result = new FreezesResult
            {
                RecordId = id,
                RunIndex = runIndex,
                MinMs = minMs,
                MetricSource = FrametimeSequenceHelper.SourceLabel(useDisplay),
                MetricSourceWarning = FrametimeSequenceHelper.GetSourceWarning(run, _stats, useDisplay),
            };

            for (int i = 0; i < raw.Length; i++)
            {
                double v = raw[i];
                if (v <= 0 || double.IsNaN(v)) continue;
                if (v < minMs) continue;
                result.Freezes.Add(new FreezeEvent
                {
                    TSec = Math.Round(i < rawTimes.Count ? rawTimes[i] : 0, 4),
                    FrametimeMs = Math.Round(v, 3),
                });
            }

            result.TotalCount = result.Freezes.Count;
            if (maxResults > 0 && result.Freezes.Count > maxResults)
            {
                result.Freezes = result.Freezes
                    .OrderByDescending(f => f.FrametimeMs)
                    .Take(maxResults)
                    .OrderBy(f => f.TSec)
                    .ToList();
            }
            return result;
        }

        // ─── cfx_get_low_fps_windows ─────────────────────────────────────────

        [McpServerTool(Name = "cfx_get_low_fps_windows",
            Description = "Identifies sustained intervals where FPS dropped below a threshold for at least minDurationSec. " +
                "Uses a 1s sliding window to smooth the FPS curve and avoid single-frame false positives. " +
                "FPS source follows AppSettings.UseDisplayChangeMetrics by default (matches Analysis tab); override with useDisplayChangeMetrics.")]
        public LowFpsWindowsResult GetLowFpsWindows(
            [Description("Record id (absolute file path) from cfx_list_records")] string id,
            [Description("Run index within the record (0-based). Default: 0.")] int runIndex = 0,
            [Description("FPS threshold — windows below this count.")] double fpsThreshold = 60.0,
            [Description("Minimum sustained duration in seconds for a window to be reported. Default 1.0.")] double minDurationSec = 1.0,
            [Description("Smoothing window in seconds applied to the FPS curve. Default 1.0.")] double smoothingSec = 1.0,
            [Description("Override the FPS source. true = display-change times, false = present times, omit = follow AppSettings.UseDisplayChangeMetrics.")] bool? useDisplayChangeMetrics = null)
        {
            var (run, _) = LoadRun(id, runIndex);
            bool useDisplay = useDisplayChangeMetrics ?? _config.UseDisplayChangeMetrics;
            var raw = FrametimeSequenceHelper.ResolveRawSequence(run, useDisplay);
            // Time axis comes from the present-time index (canonical per-frame timestamps).
            var presents = run.CaptureData?.MsBetweenPresents;
            if (presents == null || presents.Length == 0 || raw == null || raw.Length == 0)
                return new LowFpsWindowsResult { RecordId = id, RunIndex = runIndex, FpsThreshold = fpsThreshold, MetricSource = FrametimeSequenceHelper.SourceLabel(useDisplay) };

            var times = BuildTimeAxis(run.CaptureData);
            var result = new LowFpsWindowsResult
            {
                RecordId = id,
                RunIndex = runIndex,
                FpsThreshold = fpsThreshold,
                MinDurationSec = minDurationSec,
                MetricSource = FrametimeSequenceHelper.SourceLabel(useDisplay),
                MetricSourceWarning = FrametimeSequenceHelper.GetSourceWarning(run, _stats, useDisplay),
            };

            // Smooth FPS using a backward-looking window of `smoothingSec`.
            // When using display-change, skip dropped frames (value <= 0) so they don't pull the avg.
            int frameCount = presents.Length;
            var smoothedFps = new double[frameCount];
            int left = 0;
            for (int i = 0; i < frameCount; i++)
            {
                while (left < i && times[i] - times[left] > smoothingSec) left++;
                double sumMs = 0;
                int n = 0;
                for (int j = left; j <= i; j++)
                {
                    double v = j < raw.Length ? raw[j] : 0;
                    if (v <= 0 || double.IsNaN(v)) continue;
                    sumMs += v;
                    n++;
                }
                double avgMs = n > 0 ? sumMs / n : 0;
                smoothedFps[i] = avgMs > 0 ? 1000.0 / avgMs : 0;
            }

            // Walk the smoothed curve and group contiguous below-threshold runs.
            int? regionStart = null;
            double regionMinFps = double.MaxValue;
            double regionSumFps = 0;
            int regionN = 0;

            void CloseRegion(int endIdx)
            {
                if (!regionStart.HasValue) return;
                int s = regionStart.Value;
                double startSec = times[s];
                double endSec = times[endIdx];
                double dur = endSec - startSec;
                if (dur >= minDurationSec)
                {
                    result.Windows.Add(new LowFpsWindow
                    {
                        StartSec = Math.Round(startSec, 3),
                        EndSec = Math.Round(endSec, 3),
                        DurationSec = Math.Round(dur, 3),
                        AvgFps = regionN > 0 ? Math.Round(regionSumFps / regionN, 2) : 0,
                        MinFps = Math.Round(regionMinFps, 2),
                    });
                }
                regionStart = null;
                regionMinFps = double.MaxValue;
                regionSumFps = 0;
                regionN = 0;
            }

            for (int i = 0; i < frameCount; i++)
            {
                bool below = smoothedFps[i] < fpsThreshold;
                if (below)
                {
                    if (!regionStart.HasValue) regionStart = i;
                    if (smoothedFps[i] < regionMinFps) regionMinFps = smoothedFps[i];
                    regionSumFps += smoothedFps[i];
                    regionN++;
                }
                else if (regionStart.HasValue)
                {
                    CloseRegion(i - 1);
                }
            }
            CloseRegion(frameCount - 1);

            return result;
        }

        // ─── cfx_get_metric_over_time ────────────────────────────────────────

        [McpServerTool(Name = "cfx_get_metric_over_time",
            Description = "Computes an FPS metric (Average, P1, P0dot2, …) inside a sliding window of `windowSec`, advanced by `stepSec`. " +
                "Use this to see how P1 / P0.2 / Average evolve over the run — spotting where consistency degrades. " +
                "FPS source follows AppSettings.UseDisplayChangeMetrics by default (matches Analysis tab); override with useDisplayChangeMetrics.")]
        public MetricOverTimeResult GetMetricOverTime(
            [Description("Record id (absolute file path) from cfx_list_records")] string id,
            [Description("Run index within the record (0-based). Default: 0.")] int runIndex = 0,
            [Description("Metric name. Same set as cfx_get_metrics (Average, P1, P0dot2, P99, …).")] string metric = "P1",
            [Description("Sliding window length in seconds. Default 5.")] double windowSec = 5.0,
            [Description("Step between windows in seconds. Default = windowSec / 5.")] double stepSec = 0,
            [Description("Override the FPS source. true = display-change times, false = present times, omit = follow AppSettings.UseDisplayChangeMetrics.")] bool? useDisplayChangeMetrics = null)
        {
            if (string.IsNullOrEmpty(metric)) metric = "P1";
            if (windowSec <= 0) windowSec = 5.0;
            if (stepSec <= 0) stepSec = Math.Max(0.1, windowSec / 5.0);

            if (!Enum.TryParse(metric, ignoreCase: true, out EMetric eMetric) || eMetric == EMetric.None)
                throw new ArgumentException($"Metric '{metric}' not supported. See cfx_get_metrics for valid names.", nameof(metric));

            var (run, _) = LoadRun(id, runIndex);
            bool useDisplay = useDisplayChangeMetrics ?? _config.UseDisplayChangeMetrics;
            var raw = FrametimeSequenceHelper.ResolveRawSequence(run, useDisplay);
            var presents = run.CaptureData?.MsBetweenPresents;
            if (presents == null || presents.Length == 0 || raw == null || raw.Length == 0)
                return new MetricOverTimeResult { RecordId = id, RunIndex = runIndex, Metric = metric, WindowSec = windowSec, MetricSource = FrametimeSequenceHelper.SourceLabel(useDisplay) };

            var times = BuildTimeAxis(run.CaptureData);
            double duration = times[times.Count - 1];
            int rounding = _config.FpsValuesRoundingDigits > 0 ? _config.FpsValuesRoundingDigits : 2;

            var result = new MetricOverTimeResult
            {
                RecordId = id,
                RunIndex = runIndex,
                Metric = eMetric.ToString(),
                WindowSec = windowSec,
                StepSec = stepSec,
                MetricSource = FrametimeSequenceHelper.SourceLabel(useDisplay),
                MetricSourceWarning = FrametimeSequenceHelper.GetSourceWarning(run, _stats, useDisplay),
            };

            var slice = new List<double>(256);
            int frameCount = presents.Length;
            for (double t = 0; t + windowSec <= duration + 1e-9; t += stepSec)
            {
                slice.Clear();
                double tEnd = t + windowSec;
                for (int i = 0; i < frameCount; i++)
                {
                    if (times[i] < t) continue;
                    if (times[i] > tEnd) break;
                    double v = i < raw.Length ? raw[i] : 0;
                    if (v <= 0 || double.IsNaN(v)) continue; // skip dropped frames in display-change mode
                    slice.Add(v);
                }
                if (slice.Count < 5) continue;

                double value;
                try { value = _stats.GetFpsMetricValue(slice, eMetric); }
                catch { value = double.NaN; }

                result.Points.Add(new MetricOverTimePoint
                {
                    TSec = Math.Round(t + windowSec / 2.0, 3),
                    Value = double.IsNaN(value) ? value : Math.Round(value, rounding),
                });
            }
            return result;
        }

        // ─── cfx_get_runs_consistency ────────────────────────────────────────

        [McpServerTool(Name = "cfx_get_runs_consistency",
            Description = "Computes run-to-run variance for a record that has multiple runs (e.g. repeated benchmark passes). " +
                "Returns mean / stdev / coefficient-of-variation per metric and the per-run values, so you can answer 'how consistent is this benchmark?'. " +
                "FPS source follows AppSettings.UseDisplayChangeMetrics by default (matches Analysis tab); override with useDisplayChangeMetrics.")]
        public RunsConsistencyResult GetRunsConsistency(
            [Description("Record id (absolute file path) from cfx_list_records")] string id,
            [Description("Metrics to evaluate. Omit for the default set used by cfx_get_metrics.")] string[] metrics = null,
            [Description("Override the FPS source. true = display-change times, false = present times, omit = follow AppSettings.UseDisplayChangeMetrics.")] bool? useDisplayChangeMetrics = null)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("id must be provided", nameof(id));
            if (!File.Exists(id)) throw new FileNotFoundException("Record not found", id);

            var session = _recordTools.SafeLoad(id);
            if (session?.Runs == null || session.Runs.Count == 0)
                throw new InvalidOperationException("Record has no runs: " + id);

            string[] requested = (metrics == null || metrics.Length == 0)
                ? new[] { "Average", "P1", "P0dot2", "Min" }
                : metrics;

            bool useDisplay = useDisplayChangeMetrics ?? _config.UseDisplayChangeMetrics;
            int rounding = _config.FpsValuesRoundingDigits > 0 ? _config.FpsValuesRoundingDigits : 2;
            var result = new RunsConsistencyResult
            {
                RecordId = id,
                RunCount = session.Runs.Count,
                MetricSource = FrametimeSequenceHelper.SourceLabel(useDisplay),
                MetricSourceWarning = FrametimeSequenceHelper.GetSourceWarning(session.Runs[0], _stats, useDisplay),
            };

            foreach (var name in requested)
            {
                if (!Enum.TryParse(name, ignoreCase: true, out EMetric m) || m == EMetric.None)
                {
                    result.Metrics.Add(new RunsConsistencyMetric { Metric = name, Mean = double.NaN, Note = "unsupported metric" });
                    continue;
                }

                var perRun = new List<double>(session.Runs.Count);
                foreach (var run in session.Runs)
                {
                    var sequence = FrametimeSequenceHelper.ResolveSequence(run, useDisplay);
                    if (sequence == null || sequence.Count == 0) { perRun.Add(double.NaN); continue; }
                    double v;
                    try { v = _stats.GetFpsMetricValue(sequence, m); }
                    catch { v = double.NaN; }
                    perRun.Add(v);
                }

                var clean = perRun.Where(v => !double.IsNaN(v)).ToList();
                double mean = clean.Count > 0 ? clean.Average() : double.NaN;
                double variance = clean.Count > 1
                    ? clean.Select(v => (v - mean) * (v - mean)).Sum() / (clean.Count - 1)
                    : 0;
                double stdev = Math.Sqrt(variance);
                double cov = (clean.Count > 1 && Math.Abs(mean) > 1e-9) ? (stdev / mean * 100.0) : double.NaN;

                result.Metrics.Add(new RunsConsistencyMetric
                {
                    Metric = m.ToString(),
                    Mean = double.IsNaN(mean) ? mean : Math.Round(mean, rounding),
                    Stdev = double.IsNaN(stdev) ? stdev : Math.Round(stdev, rounding),
                    CoefficientOfVariationPct = double.IsNaN(cov) ? cov : Math.Round(cov, 2),
                    PerRun = perRun.Select(v => double.IsNaN(v) ? v : Math.Round(v, rounding)).ToList(),
                });
            }
            return result;
        }

        // ─── helpers ─────────────────────────────────────────────────────────

        private (ISessionRun run, ISession session) LoadRun(string id, int runIndex)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("id must be provided", nameof(id));
            if (!File.Exists(id)) throw new FileNotFoundException("Record not found", id);

            var session = _recordTools.SafeLoad(id);
            if (session?.Runs == null || session.Runs.Count == 0)
                throw new InvalidOperationException("Record has no runs: " + id);
            if (runIndex < 0 || runIndex >= session.Runs.Count)
                throw new ArgumentOutOfRangeException(nameof(runIndex),
                    $"runIndex {runIndex} out of range; record has {session.Runs.Count} run(s)");
            return (session.Runs[runIndex], session);
        }

        // CaptureData.TimeInSeconds is the canonical time axis. Fall back to cumulative ms if missing.
        internal static IList<double> BuildTimeAxis(ISessionCaptureData data)
        {
            var ft = data?.MsBetweenPresents;
            if (ft == null || ft.Length == 0) return Array.Empty<double>();

            if (data.TimeInSeconds != null && data.TimeInSeconds.Length == ft.Length)
                return data.TimeInSeconds;

            var times = new double[ft.Length];
            double t = 0;
            for (int i = 0; i < ft.Length; i++)
            {
                t += ft[i] / 1000.0;
                times[i] = t;
            }
            return times;
        }
    }
}
