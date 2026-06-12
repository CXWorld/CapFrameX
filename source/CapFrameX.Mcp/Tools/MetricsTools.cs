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
    public class MetricsTools
    {
        private const string AnimationErrorP99Metric = "AnimationErrorP99";
        private const string AnimationErrorAverageMetric = "AnimationErrorAverage";

        private static readonly string[] DefaultMetrics =
            { "Average", "P1", "P0dot2", "Min", "Max",
              AnimationErrorP99Metric, AnimationErrorAverageMetric, "AdaptiveStd",
              "OnePercentLowAverage", "ZerodotOnePercentLowAverage" };

        private readonly RecordTools _recordTools;
        private readonly IStatisticProvider _stats;
        private readonly IAppConfiguration _config;

        public MetricsTools(RecordTools recordTools, IStatisticProvider stats, IAppConfiguration config)
        {
            _recordTools = recordTools;
            _stats = stats;
            _config = config;
        }

        [McpServerTool(Name = "cfx_get_metrics",
            Description = "Computes FPS-based statistical metrics and Animation Error metrics for a record. " +
                "Available metric names: Average, P99, P95, P5, P1, P0dot2, P0dot1, Median, Min, Max, AdaptiveStd, " +
                "OnePercentLowAverage, ZerodotOnePercentLowAverage, ZerodotTwoPercentLowAverage, " +
                "OnePercentLowIntegral, ZerodotOnePercentLowIntegral, ZerodotTwoPercentLowIntegral, " +
                "AnimationErrorP99, AnimationErrorAverage. Animation Error metrics use absolute values and are returned in ms. " +
                "If 'metrics' is omitted, a sensible default set is returned. " +
                "If 'runIndex' is omitted, metrics for ALL runs in the record are returned. " +
                "By default the FPS source follows AppSettings.UseDisplayChangeMetrics (matches the Analysis tab); " +
                "set 'useDisplayChangeMetrics' to override (true = MsBetweenDisplayChange, false = MsBetweenPresents).")]
        public RecordMetricsResult GetMetrics(
            [Description("Record id (absolute file path) from cfx_list_records")] string id,
            [Description("Metric names to compute. Omit for the default set.")] string[] metrics = null,
            [Description("Run index within the record (0-based). Omit to get metrics for all runs.")] int? runIndex = null,
            [Description("Override the FPS source. true = display-change times, false = present times, omit = follow AppSettings.UseDisplayChangeMetrics.")] bool? useDisplayChangeMetrics = null)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("id must be provided", nameof(id));
            if (!File.Exists(id))
                throw new FileNotFoundException("Record not found", id);

            var session = _recordTools.SafeLoad(id);
            if (session?.Runs == null || session.Runs.Count == 0)
                throw new InvalidOperationException("Record has no runs: " + id);
            if (runIndex.HasValue && (runIndex < 0 || runIndex >= session.Runs.Count))
                throw new ArgumentOutOfRangeException(nameof(runIndex),
                    $"runIndex {runIndex} out of range; record has {session.Runs.Count} run(s)");

            var requested = (metrics == null || metrics.Length == 0) ? DefaultMetrics : metrics;
            bool useDisplay = useDisplayChangeMetrics ?? _config.UseDisplayChangeMetrics;

            var result = new RecordMetricsResult
            {
                RecordId = id,
                Game = session.Info?.GameName,
                MetricSource = FrametimeSequenceHelper.SourceLabel(useDisplay),
            };

            int rounding = _config.FpsValuesRoundingDigits > 0 ? _config.FpsValuesRoundingDigits : 2;

            int from = runIndex ?? 0;
            int to = runIndex ?? session.Runs.Count - 1;
            // Surface a source-fit warning based on the first analyzed run (settings apply per record).
            result.MetricSourceWarning = FrametimeSequenceHelper.GetSourceWarning(
                session.Runs[from], _stats, useDisplay);
            for (int i = from; i <= to; i++)
            {
                var run = session.Runs[i];
                var sequence = FrametimeSequenceHelper.ResolveSequence(run, useDisplay);
                if (sequence == null || sequence.Count == 0) continue;

                var runMetrics = new RunMetrics { RunIndex = i };

                foreach (var name in requested)
                {
                    if (TryGetAnimationErrorMetricName(name, out var animationErrorMetricName))
                    {
                        runMetrics.Metrics.Add(GetAnimationErrorMetricResult(run, animationErrorMetricName, rounding));
                        continue;
                    }

                    if (!Enum.TryParse(name, ignoreCase: true, out EMetric metric) || metric == EMetric.None)
                    {
                        runMetrics.Metrics.Add(new MetricResult { Metric = name, Value = double.NaN, Unit = "unsupported" });
                        continue;
                    }

                    double value;
                    try { value = _stats.GetFpsMetricValue(sequence, metric); }
                    catch { value = double.NaN; }

                    runMetrics.Metrics.Add(new MetricResult
                    {
                        Metric = metric.ToString(),
                        Value = double.IsNaN(value) ? value : Math.Round(value, rounding),
                        Unit = "fps",
                    });
                }
                result.Runs.Add(runMetrics);
            }

            return result;
        }

        private MetricResult GetAnimationErrorMetricResult(ISessionRun run, string metricName, int rounding)
        {
            var animationErrors = run.CaptureData?.AnimationError?
                .Where(animationError => !double.IsNaN(animationError))
                .Select(animationError => Math.Abs(animationError))
                .ToList();

            if (animationErrors == null || animationErrors.Count == 0)
            {
                return new MetricResult
                {
                    Metric = metricName,
                    Value = double.NaN,
                    Unit = "ms",
                };
            }

            double metricValue = string.Equals(metricName, AnimationErrorP99Metric, StringComparison.OrdinalIgnoreCase)
                ? _stats.GetPQuantileSequence(animationErrors, 0.99)
                : animationErrors.Average();

            return new MetricResult
            {
                Metric = metricName,
                Value = double.IsNaN(metricValue) ? metricValue : Math.Round(metricValue, rounding),
                Unit = "ms",
            };
        }

        private static bool TryGetAnimationErrorMetricName(string metricName, out string canonicalMetricName)
        {
            canonicalMetricName = null;

            if (string.Equals(metricName, AnimationErrorP99Metric, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(metricName, "P99AnimationError", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(metricName, "MsAnimationErrorP99", StringComparison.OrdinalIgnoreCase))
            {
                canonicalMetricName = AnimationErrorP99Metric;
                return true;
            }

            if (string.Equals(metricName, AnimationErrorAverageMetric, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(metricName, "AverageAnimationError", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(metricName, "MsAnimationErrorAverage", StringComparison.OrdinalIgnoreCase))
            {
                canonicalMetricName = AnimationErrorAverageMetric;
                return true;
            }

            return false;
        }
    }
}
