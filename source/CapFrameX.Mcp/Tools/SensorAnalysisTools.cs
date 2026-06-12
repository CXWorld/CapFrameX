using CapFrameX.Contracts.Sensor;
using CapFrameX.Data.Session.Contracts;
using CapFrameX.Mcp.Attributes;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace CapFrameX.Mcp.Tools
{
    [McpServerToolType]
    public class SensorAnalysisTools
    {
        // Sensor key channels we never align/correlate on (they're the time grid itself).
        private static readonly HashSet<string> TimeKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "MeasureTime", "BetweenMeasureTime",
        };

        private readonly RecordTools _recordTools;
        private readonly ISensorService _sensorService;

        public SensorAnalysisTools(RecordTools recordTools, ISensorService sensorService)
        {
            _recordTools = recordTools;
            _sensorService = sensorService;
        }

        // ─── cfx_get_sensor_time_series ──────────────────────────────────────

        [McpServerTool(Name = "cfx_get_sensor_time_series",
            Description = "Returns per-sensor time series for a record run. Sensor key list defaults to 'all'; pass identifiers from cfx_get_sensor_summary.name to narrow. " +
                "Optional bucket downsampling collapses each bucket to its mean.")]
        public SensorTimeSeriesResult GetSensorTimeSeries(
            [Description("Record id (absolute file path) from cfx_list_records")] string id,
            [Description("Run index within the record (0-based). Default: 0.")] int runIndex = 0,
            [Description("Sensor names to include. Match is case-insensitive substring on the sensor's Name. Empty/null = all.")] string[] sensors = null,
            [Description("Downsampled rate in Hz. 0 = no downsampling. Sensor logging is typically 1 Hz already.")] double downsampleHz = 0,
            [Description("Maximum number of sensors returned. 0 = unlimited.")] int maxSensors = 12)
        {
            var run = LoadRun(id, runIndex);
            var sensorData = run.SensorData2;

            var result = new SensorTimeSeriesResult
            {
                RecordId = id,
                RunIndex = runIndex,
                DownsampleHz = downsampleHz,
            };
            if (sensorData == null) return result;

            var measureTime = ExtractMeasureTime(sensorData);
            if (measureTime.Count == 0) return result;
            result.SampleCount = measureTime.Count;

            var nameFilter = (sensors == null || sensors.Length == 0) ? null : sensors;

            foreach (var kvp in sensorData)
            {
                if (TimeKeys.Contains(kvp.Key)) continue;
                var entry = kvp.Value;
                if (entry?.Values == null || entry.Values.Count == 0) continue;

                var displayName = string.IsNullOrEmpty(entry.Name) ? kvp.Key : entry.Name;
                if (nameFilter != null &&
                    !nameFilter.Any(n => !string.IsNullOrEmpty(n) && displayName.IndexOf(n, StringComparison.OrdinalIgnoreCase) >= 0))
                    continue;

                var values = entry.Values.ToArray();
                var series = new SensorTimeSeries
                {
                    Identifier = kvp.Key,
                    Name = displayName,
                    Type = entry.Type,
                };

                if (downsampleHz <= 0)
                {
                    int n = Math.Min(values.Length, measureTime.Count);
                    for (int i = 0; i < n; i++)
                        series.Points.Add(new SensorTimePoint
                        {
                            TSec = Math.Round(measureTime[i], 4),
                            Value = double.IsNaN(values[i]) ? values[i] : Math.Round(values[i], 3),
                        });
                }
                else
                {
                    double bucket = 1.0 / downsampleHz;
                    double duration = measureTime[measureTime.Count - 1];
                    int bucketCount = Math.Max(1, (int)Math.Ceiling(duration * downsampleHz));
                    var sum = new double[bucketCount];
                    var cnt = new int[bucketCount];
                    int n = Math.Min(values.Length, measureTime.Count);
                    for (int i = 0; i < n; i++)
                    {
                        if (double.IsNaN(values[i])) continue;
                        int b = (int)(measureTime[i] / bucket);
                        if (b < 0) b = 0;
                        if (b >= bucketCount) b = bucketCount - 1;
                        sum[b] += values[i];
                        cnt[b]++;
                    }
                    for (int b = 0; b < bucketCount; b++)
                    {
                        if (cnt[b] == 0) continue;
                        series.Points.Add(new SensorTimePoint
                        {
                            TSec = Math.Round(b * bucket, 4),
                            Value = Math.Round(sum[b] / cnt[b], 3),
                        });
                    }
                }

                result.Sensors.Add(series);
                if (maxSensors > 0 && result.Sensors.Count >= maxSensors) break;
            }
            return result;
        }

        // ─── cfx_correlate_sensors_with_frametime ────────────────────────────

        [McpServerTool(Name = "cfx_correlate_sensors_with_frametime",
            Description = "Computes Pearson correlation between each sensor channel and the frametime curve, aligned on the sensor sample grid (frametimes averaged per sensor sample). " +
                "Returns sensors sorted by absolute correlation strength — top entries are the channels that best 'explain' frametime variance. " +
                "Use to find which sensor (e.g. CPU power, GPU temp) tracks stuttering.")]
        public CorrelationResult CorrelateSensorsWithFrametime(
            [Description("Record id (absolute file path) from cfx_list_records")] string id,
            [Description("Run index within the record (0-based). Default: 0.")] int runIndex = 0,
            [Description("Return at most N sensors, sorted by |pearson|. Default 15.")] int topN = 15,
            [Description("Minimum overlapping sample count for a correlation to be reported. Default 8.")] int minOverlap = 8)
        {
            var run = LoadRun(id, runIndex);
            var ft = run.CaptureData?.MsBetweenPresents;
            if (ft == null || ft.Length == 0)
                throw new InvalidOperationException("Record has no frametime data: " + id);

            var sensorData = run.SensorData2;
            var result = new CorrelationResult { RecordId = id, RunIndex = runIndex, Method = "pearson" };
            if (sensorData == null) return result;

            var measureTime = ExtractMeasureTime(sensorData);
            if (measureTime.Count < 2) return result;

            var ftTimes = FrametimeTools.BuildTimeAxis(run.CaptureData);
            // Resample frametimes onto the sensor grid: for each sensor sample i, average frametimes in [t_{i-1}, t_i].
            var frametimePerSample = new double[measureTime.Count];
            int j = 0;
            for (int i = 0; i < measureTime.Count; i++)
            {
                double tStart = i == 0 ? 0.0 : measureTime[i - 1];
                double tEnd = measureTime[i];
                double sum = 0;
                int cnt = 0;
                while (j < ft.Length && ftTimes[j] < tStart) j++;
                int k = j;
                while (k < ft.Length && ftTimes[k] <= tEnd)
                {
                    if (!double.IsNaN(ft[k])) { sum += ft[k]; cnt++; }
                    k++;
                }
                frametimePerSample[i] = cnt > 0 ? sum / cnt : double.NaN;
            }

            foreach (var kvp in sensorData)
            {
                if (TimeKeys.Contains(kvp.Key)) continue;
                var entry = kvp.Value;
                if (entry?.Values == null || entry.Values.Count == 0) continue;

                var values = entry.Values.ToArray();
                int n = Math.Min(values.Length, frametimePerSample.Length);
                var pairsSensor = new List<double>(n);
                var pairsFt = new List<double>(n);
                for (int i = 0; i < n; i++)
                {
                    if (double.IsNaN(values[i]) || double.IsNaN(frametimePerSample[i])) continue;
                    pairsSensor.Add(values[i]);
                    pairsFt.Add(frametimePerSample[i]);
                }
                if (pairsSensor.Count < minOverlap) continue;

                var pearson = Pearson(pairsSensor, pairsFt);
                if (double.IsNaN(pearson)) continue;

                result.Correlations.Add(new SensorCorrelation
                {
                    Identifier = kvp.Key,
                    Name = string.IsNullOrEmpty(entry.Name) ? kvp.Key : entry.Name,
                    Type = entry.Type,
                    Pearson = Math.Round(pearson, 4),
                    SampleCount = pairsSensor.Count,
                });
            }

            result.Correlations = result.Correlations
                .OrderByDescending(c => Math.Abs(c.Pearson))
                .Take(topN > 0 ? topN : 15)
                .ToList();
            return result;
        }

        // ─── cfx_get_live_sensor_snapshot ────────────────────────────────────

        [McpServerTool(Name = "cfx_get_live_sensor_snapshot",
            Description = "Returns the current value of every detected hardware sensor (CPU/GPU/RAM/HDD channels) live. " +
                "Differs from cfx_get_sensor_summary which reads aggregates from saved records.")]
        public async Task<LiveSensorSnapshotResult> GetLiveSensorSnapshot(
            [Description("Optional case-insensitive substring filter on sensor name or hardware name.")] string filter = null,
            [Description("Maximum entries returned. 0 = unlimited.")] int maxEntries = 0)
        {
            var entries = (await _sensorService.GetSensorEntries().ConfigureAwait(false))
                ?? Enumerable.Empty<ISensorEntry>();

            var result = new LiveSensorSnapshotResult { CapturedAt = DateTime.UtcNow };
            foreach (var e in entries)
            {
                if (e == null) continue;
                if (!string.IsNullOrEmpty(filter))
                {
                    bool match = (e.Name ?? "").IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0
                              || (e.HardwareName ?? "").IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0;
                    if (!match) continue;
                }

                double? num = null;
                string text = null;
                switch (e.Value)
                {
                    case null: break;
                    case float f: num = double.IsNaN(f) ? (double?)null : Math.Round(f, 3); break;
                    case double d: num = double.IsNaN(d) ? (double?)null : Math.Round(d, 3); break;
                    case int i: num = i; break;
                    default: text = e.Value.ToString(); break;
                }

                result.Sensors.Add(new LiveSensorEntry
                {
                    Identifier = e.Identifier,
                    Name = e.Name,
                    SensorType = e.SensorType,
                    HardwareType = e.HardwareType,
                    HardwareName = e.HardwareName,
                    Value = num,
                    ValueText = text,
                });

                if (maxEntries > 0 && result.Sensors.Count >= maxEntries) break;
            }

            result.Sensors = result.Sensors
                .OrderBy(s => s.HardwareType ?? string.Empty)
                .ThenBy(s => s.HardwareName ?? string.Empty)
                .ThenBy(s => s.Name ?? string.Empty)
                .ToList();
            result.Count = result.Sensors.Count;
            return result;
        }

        // ─── cfx_list_sensor_sources ─────────────────────────────────────────

        [McpServerTool(Name = "cfx_list_sensor_sources",
            Description = "Lists the sensor sources/providers active in the current CapFrameX instance, with sensor and hardware counts. " +
                "Use this to answer 'why are GPU power values missing?' — if a provider is absent here, its channels won't be in records.")]
        public async Task<SensorSourcesResult> ListSensorSources()
        {
            var entries = (await _sensorService.GetSensorEntries().ConfigureAwait(false))
                ?? Enumerable.Empty<ISensorEntry>();

            var grouped = entries
                .Where(e => e != null)
                .GroupBy(e => e.HardwareType ?? "Unknown")
                .Select(g => new SensorSource
                {
                    HardwareType = g.Key,
                    HardwareCount = g.Select(e => e.HardwareName ?? string.Empty).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
                    SensorCount = g.Count(),
                    HardwareNames = g.Select(e => e.HardwareName ?? string.Empty)
                        .Where(n => !string.IsNullOrEmpty(n))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .OrderBy(n => n)
                        .ToList(),
                })
                .OrderBy(s => s.HardwareType)
                .ToList();

            return new SensorSourcesResult
            {
                Sources = grouped,
                TotalSensorCount = grouped.Sum(s => s.SensorCount),
                CpuVendor = SafeName(() => _sensorService.GetCpuVendor().ToString()),
                GpuVendor = SafeName(() => _sensorService.GetGpuVendor().ToString()),
                CpuName = SafeName(() => _sensorService.GetCpuName()),
                GpuName = SafeName(() => _sensorService.GetGpuName()),
                GpuDriverVersion = SafeName(() => _sensorService.GetGpuDriverVersion()),
                DetectedGpus = SafeList(() => _sensorService.GetDetectedGpus()),
            };
        }

        // ─── helpers ─────────────────────────────────────────────────────────

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

        private static IList<double> ExtractMeasureTime(ISessionSensorData2 data)
        {
            var measure = data?.MeasureTime;
            if (measure?.Values == null || measure.Values.Count == 0) return Array.Empty<double>();
            // MeasureTime samples are seconds since capture start.
            return measure.Values.ToArray();
        }

        private static double Pearson(IList<double> x, IList<double> y)
        {
            int n = Math.Min(x.Count, y.Count);
            if (n < 2) return double.NaN;
            double sumX = 0, sumY = 0;
            for (int i = 0; i < n; i++) { sumX += x[i]; sumY += y[i]; }
            double meanX = sumX / n, meanY = sumY / n;

            double num = 0, denX = 0, denY = 0;
            for (int i = 0; i < n; i++)
            {
                double dx = x[i] - meanX;
                double dy = y[i] - meanY;
                num += dx * dy;
                denX += dx * dx;
                denY += dy * dy;
            }
            double den = Math.Sqrt(denX * denY);
            return den < 1e-12 ? double.NaN : num / den;
        }

        private static string SafeName(Func<string> fn) { try { return fn(); } catch { return null; } }
        private static List<string> SafeList(Func<IEnumerable<string>> fn) { try { return fn()?.ToList(); } catch { return new List<string>(); } }
    }
}
