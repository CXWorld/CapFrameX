using CapFrameX.Data.Session.Contracts;
using CapFrameX.Mcp.Attributes;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;

namespace CapFrameX.Mcp.Tools
{
    [McpServerToolType]
    public class BottleneckTools
    {
        // Thresholds (conservative, reviewer-grade defaults).
        private const double GpuThermalLimitC = 83.0;   // most modern GPUs throttle around here
        private const double CpuThermalLimitC = 95.0;   // typical TjMax safety margin
        private const double GpuSaturatedPct = 95.0;
        private const double GpuRelaxedPct = 90.0;
        private const double CpuMaxThreadHotPct = 80.0;
        private const double CpuMaxThreadCoolPct = 70.0;
        private const double PowerLimitMaterialPct = 5.0;  // % of samples at full power-cap

        private readonly RecordTools _recordTools;

        public BottleneckTools(RecordTools recordTools)
        {
            _recordTools = recordTools;
        }

        [McpServerTool(Name = "cfx_analyze_bottleneck",
            Description = "Classifies the run as cpu-bound, gpu-bound, balanced, thermal-throttling, " +
                "power-limited, or unknown using sensor data. Returns verdict, confidence, " +
                "human-readable reasoning, and the supporting numeric signals.")]
        public BottleneckResult AnalyzeBottleneck(
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
            var sensors = run.SensorData2;

            var signals = ExtractSignals(sensors);
            var (verdict, confidence, reasoning) = Classify(signals);

            return new BottleneckResult
            {
                RecordId = id,
                RunIndex = runIndex,
                Verdict = verdict,
                Confidence = confidence,
                Reasoning = reasoning,
                Signals = signals,
            };
        }

        private static BottleneckSignals ExtractSignals(ISessionSensorData2 data)
        {
            var s = new BottleneckSignals();
            if (data == null) return s;

            foreach (var kvp in data)
            {
                var entry = kvp.Value;
                if (entry?.Values == null || entry.Values.Count == 0) continue;

                var name = entry.Name ?? string.Empty;
                var type = entry.Type ?? string.Empty;

                bool isGpuCore = name.IndexOf("GPU Core", StringComparison.OrdinalIgnoreCase) >= 0;
                bool isCpuMax = name.IndexOf("CPU Max", StringComparison.OrdinalIgnoreCase) >= 0;
                bool isCpuTotal = name.IndexOf("CPU Total", StringComparison.OrdinalIgnoreCase) >= 0;
                bool isCpuPackage = name.IndexOf("CPU Package", StringComparison.OrdinalIgnoreCase) >= 0
                                    || name.IndexOf("CPU", StringComparison.OrdinalIgnoreCase) >= 0;
                bool isGpuPowerLimit = name.IndexOf("GPU Power Limit", StringComparison.OrdinalIgnoreCase) >= 0;
                bool isVramDed = name.IndexOf("GPU Memory Dedicated", StringComparison.OrdinalIgnoreCase) >= 0;

                if (isGpuCore && type == "Load")
                {
                    var stats = Aggregate(entry.Values);
                    s.GpuLoadAvg = Round(stats.avg);
                    s.GpuLoadMax = Round(stats.max);
                }
                else if (isGpuCore && type == "Temperature")
                {
                    var stats = Aggregate(entry.Values);
                    s.GpuTempAvg = Round(stats.avg);
                    s.GpuTempMax = Round(stats.max);
                }
                else if (isCpuMax && type == "Load")
                {
                    s.CpuMaxThreadLoadAvg = Round(Aggregate(entry.Values).avg);
                }
                else if (isCpuTotal && type == "Load")
                {
                    s.CpuTotalLoadAvg = Round(Aggregate(entry.Values).avg);
                }
                else if (isCpuPackage && type == "Temperature" && s.CpuTempAvg == null)
                {
                    var stats = Aggregate(entry.Values);
                    s.CpuTempAvg = Round(stats.avg);
                    s.CpuTempMax = Round(stats.max);
                }
                else if (isGpuPowerLimit && (type == "Factor" || type == "SmallData"))
                {
                    int total = entry.Values.Count;
                    int hits = entry.Values.Count(v => v >= 0.99);
                    s.GpuPowerLimitHitsPct = total > 0 ? Round(100.0 * hits / total) : (double?)null;
                }
                else if (isVramDed && type == "Data")
                {
                    s.VramUsageMaxGB = Round(Aggregate(entry.Values).max);
                }
            }
            return s;
        }

        private static (double avg, double min, double max) Aggregate(LinkedList<double> values)
        {
            double min = double.MaxValue, max = double.MinValue, sum = 0.0;
            int count = 0;
            foreach (var v in values)
            {
                if (double.IsNaN(v)) continue;
                if (v < min) min = v;
                if (v > max) max = v;
                sum += v;
                count++;
            }
            return count == 0 ? (0, 0, 0) : (sum / count, min, max);
        }

        private static double Round(double v) => Math.Round(v, 2);

        private static (string verdict, string confidence, string reasoning) Classify(BottleneckSignals s)
        {
            var notes = new List<string>();

            // 1. Power-limited (high priority — GPU is artificially capped)
            if (s.GpuPowerLimitHitsPct.HasValue && s.GpuPowerLimitHitsPct.Value >= PowerLimitMaterialPct)
            {
                notes.Add(FormattableString.Invariant($"GPU power-limit reached on {s.GpuPowerLimitHitsPct:F1}% of samples"));
                var conf = s.GpuPowerLimitHitsPct.Value >= 25 ? "high" : "medium";
                return ("power-limited", conf, string.Join("; ", notes));
            }

            // 2. Thermal throttling
            if (s.GpuTempMax.HasValue && s.GpuTempMax.Value >= GpuThermalLimitC)
            {
                notes.Add(FormattableString.Invariant($"GPU peak temperature {s.GpuTempMax:F1}°C ≥ {GpuThermalLimitC}°C throttle threshold"));
                return ("thermal-throttling", "high", string.Join("; ", notes));
            }
            if (s.CpuTempMax.HasValue && s.CpuTempMax.Value >= CpuThermalLimitC)
            {
                notes.Add(FormattableString.Invariant($"CPU peak temperature {s.CpuTempMax:F1}°C ≥ {CpuThermalLimitC}°C throttle threshold"));
                return ("thermal-throttling", "high", string.Join("; ", notes));
            }

            // 3. Load-based bottleneck classification
            if (!s.GpuLoadAvg.HasValue || !s.CpuMaxThreadLoadAvg.HasValue)
            {
                notes.Add("Insufficient GPU load and/or CPU max-thread load samples");
                return ("unknown", "low", string.Join("; ", notes));
            }

            double gpuL = s.GpuLoadAvg.Value;
            double cpuL = s.CpuMaxThreadLoadAvg.Value;
            notes.Add(FormattableString.Invariant($"GPU load avg {gpuL:F1}%, CPU max-thread load avg {cpuL:F1}%"));

            if (gpuL >= GpuSaturatedPct && cpuL < CpuMaxThreadCoolPct)
            {
                var conf = (gpuL >= 98 && cpuL < 60) ? "high" : "medium";
                return ("gpu-bound", conf, string.Join("; ", notes) + " — GPU saturated, CPU has headroom");
            }

            if (gpuL < GpuRelaxedPct && cpuL > CpuMaxThreadHotPct)
            {
                var conf = (gpuL < 80 && cpuL > 90) ? "high" : "medium";
                return ("cpu-bound", conf, string.Join("; ", notes) + " — GPU underutilized while CPU is busy on at least one thread");
            }

            if (gpuL >= GpuRelaxedPct && cpuL >= CpuMaxThreadCoolPct && cpuL <= CpuMaxThreadHotPct)
            {
                return ("balanced", "medium", string.Join("; ", notes) + " — GPU and CPU both engaged without clear winner");
            }

            // Edge cases — partial pattern, low confidence
            if (gpuL >= GpuRelaxedPct)
                return ("gpu-bound", "low", string.Join("; ", notes) + " — borderline GPU saturation");
            if (cpuL >= CpuMaxThreadCoolPct)
                return ("cpu-bound", "low", string.Join("; ", notes) + " — borderline CPU pressure");

            return ("unknown", "low", string.Join("; ", notes) + " — neither GPU nor CPU pressure clearly elevated");
        }
    }
}
