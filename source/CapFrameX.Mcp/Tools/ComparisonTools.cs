using CapFrameX.Mcp.Attributes;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace CapFrameX.Mcp.Tools
{
    [McpServerToolType]
    public class ComparisonTools
    {
        private readonly MetricsTools _metricsTools;

        public ComparisonTools(MetricsTools metricsTools)
        {
            _metricsTools = metricsTools;
        }

        [McpServerTool(Name = "cfx_compare_records",
            Description = "Compares the same metrics across multiple records. " +
                "First record is treated as the baseline; subsequent records get absolute and percentage deltas. " +
                "Compares the run at 'runIndex' (default 0) of each record.")]
        public ComparisonResult CompareRecords(
            [Description("Record ids (absolute file paths) to compare. At least two.")] string[] ids,
            [Description("Metric names. Omit for the default set used by cfx_get_metrics.")] string[] metrics = null,
            [Description("Run index within each record (0-based). Default: 0.")] int runIndex = 0)
        {
            if (ids == null || ids.Length < 2)
                throw new ArgumentException("Provide at least two record ids to compare", nameof(ids));

            // Collect single-run metric results per record.
            var perRecord = new List<(string id, RunMetrics run)>(ids.Length);
            foreach (var id in ids)
            {
                var rec = _metricsTools.GetMetrics(id, metrics, runIndex);
                var run = rec.Runs.FirstOrDefault();
                if (run == null)
                    throw new InvalidOperationException($"Record has no metrics for runIndex {runIndex}: {id}");
                perRecord.Add((rec.RecordId, run));
            }

            // Use the metric names from the baseline (first record) for row order.
            var (baselineId, baselineRun) = perRecord[0];
            var result = new ComparisonResult { BaselineId = baselineId };

            foreach (var baselineMetric in baselineRun.Metrics)
            {
                var row = new ComparisonRow { Metric = baselineMetric.Metric, Unit = baselineMetric.Unit };
                double? baselineVal = double.IsNaN(baselineMetric.Value) ? (double?)null : baselineMetric.Value;

                for (int i = 0; i < perRecord.Count; i++)
                {
                    var (recId, run) = perRecord[i];
                    var match = run.Metrics.FirstOrDefault(m =>
                        string.Equals(m.Metric, baselineMetric.Metric, StringComparison.OrdinalIgnoreCase));
                    double? val = (match == null || double.IsNaN(match.Value)) ? (double?)null : match.Value;

                    var cell = new ComparisonCell { RecordId = recId, Value = val };
                    if (i > 0 && val.HasValue && baselineVal.HasValue)
                    {
                        cell.DeltaAbs = Math.Round(val.Value - baselineVal.Value, 2);
                        if (baselineVal.Value != 0)
                            cell.DeltaPct = Math.Round((val.Value - baselineVal.Value) / baselineVal.Value * 100.0, 2);
                    }
                    row.Values.Add(cell);
                }
                result.Rows.Add(row);
            }

            return result;
        }
    }
}
