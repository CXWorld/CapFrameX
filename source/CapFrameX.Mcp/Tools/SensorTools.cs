using CapFrameX.Mcp.Attributes;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;

namespace CapFrameX.Mcp.Tools
{
    [McpServerToolType]
    public class SensorTools
    {
        // Sensor keys to skip from aggregation (timing channels, not measurements).
        private static readonly HashSet<string> SkipKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "MeasureTime", "BetweenMeasureTime",
        };

        private readonly RecordTools _recordTools;

        public SensorTools(RecordTools recordTools)
        {
            _recordTools = recordTools;
        }

        [McpServerTool(Name = "cfx_get_sensor_summary",
            Description = "Returns per-sensor aggregates (avg/min/max) for a record run. " +
                "Includes CPU/GPU/RAM/VRAM channels collected during the capture.")]
        public SensorSummaryResult GetSensorSummary(
            [Description("Record id (absolute file path) from cfx_list_records")] string id,
            [Description("Run index within the record (0-based). Default: 0.")] int runIndex = 0)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("id must be provided", nameof(id));
            if (!File.Exists(id))
                throw new FileNotFoundException("Record not found", id);

            var session = _recordTools.SafeLoad(id);
            if (session?.Runs == null || session.Runs.Count == 0)
                throw new InvalidOperationException("Record has no runs: " + id);
            if (runIndex < 0 || runIndex >= session.Runs.Count)
                throw new ArgumentOutOfRangeException(nameof(runIndex),
                    $"runIndex {runIndex} out of range; record has {session.Runs.Count} run(s)");

            var run = session.Runs[runIndex];
            var sensorData = run.SensorData2;

            var result = new SensorSummaryResult { RecordId = id, RunIndex = runIndex };
            if (sensorData == null) return result;

            foreach (var kvp in sensorData)
            {
                if (SkipKeys.Contains(kvp.Key)) continue;

                var entry = kvp.Value;
                if (entry?.Values == null || entry.Values.Count == 0) continue;

                double min = double.MaxValue, max = double.MinValue, sum = 0.0;
                int count = 0;
                foreach (var v in entry.Values)
                {
                    if (double.IsNaN(v)) continue;
                    if (v < min) min = v;
                    if (v > max) max = v;
                    sum += v;
                    count++;
                }
                if (count == 0) continue;

                result.Sensors.Add(new SensorAggregate
                {
                    Name = string.IsNullOrEmpty(entry.Name) ? kvp.Key : entry.Name,
                    Type = entry.Type,
                    Average = Math.Round(sum / count, 2),
                    Min = Math.Round(min, 2),
                    Max = Math.Round(max, 2),
                    SampleCount = count,
                });
            }

            result.Sensors = result.Sensors.OrderBy(s => s.Name).ThenBy(s => s.Type).ToList();
            return result;
        }
    }
}
