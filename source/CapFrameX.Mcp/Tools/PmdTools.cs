using CapFrameX.Data.Session.Contracts;
using CapFrameX.Mcp.Attributes;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;

namespace CapFrameX.Mcp.Tools
{
    [McpServerToolType]
    public class PmdTools
    {
        // Channel ids exposed to callers — match the canonical SessionRun.Pmd*Power array names.
        private const string ChannelGpu = "gpu";
        private const string ChannelCpu = "cpu";
        private const string ChannelSystem = "system";
        private const string PowerUnit = "W";

        private readonly RecordTools _recordTools;

        public PmdTools(RecordTools recordTools)
        {
            _recordTools = recordTools;
        }

        // ─── cfx_get_pmd_summary ─────────────────────────────────────────────

        [McpServerTool(Name = "cfx_get_pmd_summary",
            Description = "Returns per-channel aggregates (avg/min/max in Watts) for the PMD power streams of a record run. " +
                "Channels: gpu, cpu, system. " +
                "PMD data only exists if a PMD device (Powenetics or ElmorLabs Benchlab) was active during capture; " +
                "otherwise hasPmdData=false and channels is empty.")]
        public PmdSummaryResult GetPmdSummary(
            [Description("Record id (absolute file path) from cfx_list_records")] string id,
            [Description("Run index within the record (0-based). Default: 0.")] int runIndex = 0)
        {
            var run = LoadRun(id, runIndex);
            var result = new PmdSummaryResult
            {
                RecordId = id,
                RunIndex = runIndex,
                SampleTimeMs = run.SampleTime,
            };

            int maxLen = 0;
            foreach (var ch in EnumerateChannels(run))
            {
                if (ch.Values == null || ch.Values.Length == 0) continue;
                if (ch.Values.Length > maxLen) maxLen = ch.Values.Length;

                double min = double.MaxValue, max = double.MinValue, sum = 0.0;
                int count = 0;
                foreach (var v in ch.Values)
                {
                    if (float.IsNaN(v)) continue;
                    if (v < min) min = v;
                    if (v > max) max = v;
                    sum += v;
                    count++;
                }
                if (count == 0) continue;

                result.Channels.Add(new PmdChannelAggregate
                {
                    Channel = ch.Name,
                    Unit = PowerUnit,
                    Average = Math.Round(sum / count, 2),
                    Min = Math.Round(min, 2),
                    Max = Math.Round(max, 2),
                    SampleCount = count,
                });
            }

            result.HasPmdData = result.Channels.Count > 0;
            result.DurationSec = result.HasPmdData && run.SampleTime > 0
                ? Math.Round((maxLen - 1) * run.SampleTime / 1000.0, 3)
                : 0.0;
            return result;
        }

        // ─── cfx_get_pmd_time_series ─────────────────────────────────────────

        [McpServerTool(Name = "cfx_get_pmd_time_series",
            Description = "Returns per-channel PMD power time series (Watts) for a record run, derived from the PmdGpuPower/PmdCpuPower/PmdSystemPower arrays. " +
                "Time axis is built from SampleTime (ms) — typical PMD rate is 100 Hz (SampleTime=10). " +
                "Optional bucket downsampling collapses each bucket to its mean. " +
                "PMD data only exists if a PMD device was active during capture; otherwise hasPmdData=false and channels is empty.")]
        public PmdTimeSeriesResult GetPmdTimeSeries(
            [Description("Record id (absolute file path) from cfx_list_records")] string id,
            [Description("Run index within the record (0-based). Default: 0.")] int runIndex = 0,
            [Description("Channels to include: 'gpu', 'cpu', 'system'. Empty/null = all.")] string[] channels = null,
            [Description("Downsampled rate in Hz. 0 = no downsampling. PMD typically samples at 100 Hz.")] double downsampleHz = 0)
        {
            var run = LoadRun(id, runIndex);
            var result = new PmdTimeSeriesResult
            {
                RecordId = id,
                RunIndex = runIndex,
                SampleTimeMs = run.SampleTime,
                DownsampleHz = downsampleHz,
            };

            // Time axis cannot be reconstructed without a positive SampleTime.
            if (run.SampleTime <= 0)
                return result;

            var channelFilter = (channels == null || channels.Length == 0)
                ? null
                : new HashSet<string>(channels.Where(c => !string.IsNullOrEmpty(c)), StringComparer.OrdinalIgnoreCase);

            double sampleTimeSec = run.SampleTime / 1000.0;

            foreach (var ch in EnumerateChannels(run))
            {
                if (ch.Values == null || ch.Values.Length == 0) continue;
                if (channelFilter != null && !channelFilter.Contains(ch.Name)) continue;

                var series = new PmdChannelSeries
                {
                    Channel = ch.Name,
                    Unit = PowerUnit,
                    SampleCount = ch.Values.Length,
                };

                if (downsampleHz <= 0)
                {
                    for (int i = 0; i < ch.Values.Length; i++)
                    {
                        var v = ch.Values[i];
                        if (float.IsNaN(v)) continue;
                        series.Points.Add(new PmdTimePoint
                        {
                            TSec = Math.Round(i * sampleTimeSec, 4),
                            Value = Math.Round(v, 3),
                        });
                    }
                }
                else
                {
                    double bucket = 1.0 / downsampleHz;
                    double duration = (ch.Values.Length - 1) * sampleTimeSec;
                    int bucketCount = Math.Max(1, (int)Math.Ceiling(duration * downsampleHz));
                    var sum = new double[bucketCount];
                    var cnt = new int[bucketCount];
                    for (int i = 0; i < ch.Values.Length; i++)
                    {
                        var v = ch.Values[i];
                        if (float.IsNaN(v)) continue;
                        int b = (int)((i * sampleTimeSec) / bucket);
                        if (b < 0) b = 0;
                        if (b >= bucketCount) b = bucketCount - 1;
                        sum[b] += v;
                        cnt[b]++;
                    }
                    for (int b = 0; b < bucketCount; b++)
                    {
                        if (cnt[b] == 0) continue;
                        series.Points.Add(new PmdTimePoint
                        {
                            TSec = Math.Round(b * bucket, 4),
                            Value = Math.Round(sum[b] / cnt[b], 3),
                        });
                    }
                }

                result.Channels.Add(series);
            }

            result.HasPmdData = result.Channels.Count > 0;
            return result;
        }

        // ─── helpers ─────────────────────────────────────────────────────────

        private struct PmdChannel
        {
            public string Name;
            public float[] Values;
        }

        private static IEnumerable<PmdChannel> EnumerateChannels(ISessionRun run)
        {
            yield return new PmdChannel { Name = ChannelGpu, Values = run.PmdGpuPower };
            yield return new PmdChannel { Name = ChannelCpu, Values = run.PmdCpuPower };
            yield return new PmdChannel { Name = ChannelSystem, Values = run.PmdSystemPower };
        }

        private ISessionRun LoadRun(string id, int runIndex)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("id must be provided", nameof(id));
            if (!File.Exists(id)) throw new FileNotFoundException("Record not found", id);
            var session = _recordTools.SafeLoad(id);
            if (session?.Runs == null || session.Runs.Count == 0)
                throw new InvalidOperationException("Record has no runs: " + id);
            if (runIndex < 0 || runIndex >= session.Runs.Count)
                throw new ArgumentOutOfRangeException(nameof(runIndex),
                    $"runIndex {runIndex} out of range; record has {session.Runs.Count} run(s)");
            return session.Runs[runIndex];
        }
    }
}
