using CapFrameX.Contracts.Configuration;
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
        private static readonly string[] DefaultMetrics =
            { "Average", "P1", "P0dot2", "Min", "Max", "AdaptiveStd" };

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
            Description = "Computes FPS-based statistical metrics for a record. " +
                "Available metric names: Average, P99, P95, P5, P1, P0dot2, P0dot1, Median, Min, Max, AdaptiveStd, " +
                "OnePercentLowAverage, ZerodotOnePercentLowAverage, ZerodotTwoPercentLowAverage, " +
                "OnePercentLowIntegral, ZerodotOnePercentLowIntegral, ZerodotTwoPercentLowIntegral. " +
                "If 'metrics' is omitted, a sensible default set is returned. " +
                "If 'runIndex' is omitted, metrics for ALL runs in the record are returned.")]
        public RecordMetricsResult GetMetrics(
            [Description("Record id (absolute file path) from cfx_list_records")] string id,
            [Description("Metric names to compute. Omit for the default set.")] string[] metrics = null,
            [Description("Run index within the record (0-based). Omit to get metrics for all runs.")] int? runIndex = null)
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
            var result = new RecordMetricsResult
            {
                RecordId = id,
                Game = session.Info?.GameName,
            };

            int rounding = _config.FpsValuesRoundingDigits > 0 ? _config.FpsValuesRoundingDigits : 2;

            int from = runIndex ?? 0;
            int to = runIndex ?? session.Runs.Count - 1;
            for (int i = from; i <= to; i++)
            {
                var run = session.Runs[i];
                var frametimes = run.CaptureData?.MsBetweenPresents;
                if (frametimes == null || frametimes.Length == 0) continue;

                var runMetrics = new RunMetrics { RunIndex = i };
                var sequence = frametimes.ToList();

                foreach (var name in requested)
                {
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
    }
}
