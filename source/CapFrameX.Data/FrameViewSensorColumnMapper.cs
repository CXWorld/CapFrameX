using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace CapFrameX.Data
{
    /// <summary>
    /// Maps the sampled hardware sensor columns of NVIDIA FrameView log files
    /// (FrameView__YYYY_MM_DDTHHMMSS_Log.csv, including FrameView 2.x) onto CapFrameX
    /// session sensor entries. This lets imported records show the same sensor data
    /// (GPU/CPU load, clocks, temperatures, power, battery) as CapFrameX' own captures.
    /// Column headers are normalized (lower-cased, whitespace removed) before matching, which
    /// makes the mapping robust against the header layout changes between FrameView versions
    /// (e.g. "GPU0Clk(MHz)" in 1.2 vs "GPU0Clk (MHz)" in 1.4+).
    /// Unknown columns are ignored, so files without embedded sensor data (PresentMon,
    /// MangoHud, ...) are unaffected.
    /// </summary>
    public static class FrameViewSensorColumnMapper
    {
        /// <summary>A recognized sensor column of a FrameView log file.</summary>
        public sealed class SensorColumn
        {
            /// <summary>Index of the column within the CSV header.</summary>
            public int ColumnIndex { get; set; }

            /// <summary>Unique key of the sensor entry inside the session sensor data.</summary>
            public string Key { get; set; }

            /// <summary>Sensor name, following the LibreHardwareMonitor naming CapFrameX uses
            /// for its own captures (e.g. "GPU Core", "CPU Total", "CPU Package").</summary>
            public string Name { get; set; }

            /// <summary>Sensor type (e.g. "Load", "Clock", "Temperature", "Power").</summary>
            public string Type { get; set; }

            /// <summary>Index of the GPU a GPU#... column reports, -1 for every other column.</summary>
            public int GpuIndex { get; set; } = -1;

            /// <summary>The part of the GPU a GPU#... column reports ("Core" or "Memory"), null for
            /// every other column.</summary>
            public string GpuPart { get; set; }

            /// <summary>Factor every value of the column is multiplied with on import.</summary>
            public double Factor { get; set; } = 1;

            /// <summary>True for a measured value (PCAT), which is added ahead of the driver values
            /// of the same name, so the analysis takes it whatever the column order.</summary>
            public bool IsMeasurement { get; set; }
        }

        private sealed record SensorDefinition(string Name, string Type, double Factor = 1, bool IsMeasurement = false);

        // GPU#Clk (MHz), GPU#MemClock (MHz), GPU#Util (%), GPU#Temp (C) - [# = GPU index]
        private static readonly Regex GpuSensorColumnRegex = new Regex(
            @"^gpu(\d+)(memclk|memclock|clk|util|temp)(?:\(mhz\)|\(%\)|\(c\)|\(°c\))?$",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);

        // Fixed sensor columns. Keys are normalized header names (see NormalizeHeader).
        private static readonly Dictionary<string, SensorDefinition> SensorColumns =
            new Dictionary<string, SensorDefinition>(StringComparer.Ordinal)
            {
                // GPU power. PCAT is an actual measurement, so it is named to match the "GPU Power"
                // lookup too and wins over the board power the driver reports.
                ["pcatpowertotal(w)"] = new("GPU Power (PCAT)", "Power", IsMeasurement: true),
                ["nvpwr(w)(api)"] = new("GPU Power", "Power"),
                ["nv-total-usbcpwr(w)(api)"] = new("GPU Board Power (excl. USB-C)", "Power"),
                ["gpuonlypwr(w)(api)"] = new("GPU Chip Power", "Power"),
                // Imported as FrameView reports it. On a Ryzen with integrated graphics it belongs to the
                // iGPU, not to the card that renders (RX 9070 XT: 79 W at full load against 292 W TBP).
                ["amdpwr(w)(api)"] = new("GPU Power", "Power"),

                // Per the FrameView user guide (1.2/1.4), "CPUClk" is the average CPU
                // frequency - not the per-core maximum CapFrameX names "CPU Max".
                ["cpuclk(mhz)"] = new("CPU Average", "Clock"),
                ["cpuutil(%)"] = new("CPU Total", "Load"),
                ["cpuutil%"] = new("CPU Total", "Load"),
                ["cpupackagetemp(c)"] = new("CPU Package", "Temperature"),
                ["cputemp(c)"] = new("CPU Package", "Temperature"),
                ["cpupackagepower(w)"] = new("CPU Package", "Power"),
                ["cpupackagepower(watts)"] = new("CPU Package", "Power"),
                // Per-cluster core power (FrameView 1.7+): E-Core and P-Core are reported as separate
                // sensors. No platform-specific merge is performed on import; "CPU Package Power(W)"
                // (mapped just above) remains the CPU power source where FrameView provides it.
                ["e-corepower(w)"] = new("E-Core Power", "Power"),
                ["p-corepower(w)"] = new("P-Core Power", "Power"),

                // LibreHardwareMonitor's battery sensors (Battery.cs) are "Charge Level" and, while
                // discharging, "Discharge Rate" as a positive value. FrameView reports the drain rate
                // negative while discharging, so its sign is flipped.
                ["batterypercentage"] = new("Charge Level", "Level"),
                ["batterydrainrate(w)"] = new("Discharge Rate", "Power", Factor: -1),
            };

        /// <summary>
        /// Finds all FrameView sensor columns within a CSV header. The GPU#... columns of the lowest
        /// GPU index get the names the analysis reads; see <see cref="NameGpuColumns"/>.
        /// </summary>
        /// <param name="metrics">The CSV header columns.</param>
        /// <returns>The recognized sensor columns, empty if the file has no embedded sensor data.</returns>
        public static List<SensorColumn> MapColumns(IEnumerable<string> metrics)
        {
            var sensorColumns = new List<SensorColumn>();
            if (metrics == null)
                return sensorColumns;

            var usedKeys = new HashSet<string>(StringComparer.Ordinal);
            int columnIndex = 0;
            foreach (var metric in metrics)
            {
                var normalizedHeader = NormalizeHeader(metric);
                if (normalizedHeader.Length != 0)
                {
                    var match = GpuSensorColumnRegex.Match(normalizedHeader);
                    if (match.Success)
                    {
                        var (part, type) = GetGpuSensorPartAndType(match.Groups[2].Value);
                        var sensorColumn = AddSensorColumn(sensorColumns, usedKeys, normalizedHeader, columnIndex,
                            new SensorDefinition(null, type));
                        if (sensorColumn != null)
                        {
                            sensorColumn.GpuIndex = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
                            sensorColumn.GpuPart = part;
                        }
                    }
                    else if (SensorColumns.TryGetValue(normalizedHeader, out var sensor))
                    {
                        AddSensorColumn(sensorColumns, usedKeys, normalizedHeader, columnIndex, sensor);
                    }
                }

                columnIndex++;
            }

            var gpuIndices = sensorColumns.Where(column => column.GpuIndex >= 0).Select(column => column.GpuIndex);
            if (gpuIndices.Any())
                NameGpuColumns(sensorColumns, gpuIndices.Min());

            return sensorColumns;
        }

        /// <summary>
        /// Picks the GPU the analysis shows. FrameView logs every GPU in columns of its own, and the
        /// analysis reads the first entry named "GPU Core", so only one GPU may carry that name. Of the
        /// GPUs that reported a value, the one with the highest mean load wins: the GPU that rendered
        /// the game, which on a laptop is not necessarily GPU0. Without load values the lowest index
        /// wins, ties go to the lower index.
        /// </summary>
        /// <param name="sensorColumns">The sensor columns of the file.</param>
        /// <param name="values">The values of every column, in the order of <paramref name="sensorColumns"/>;
        /// NaN where the column had no value.</param>
        /// <returns>The index of the primary GPU, -1 when no GPU reported a value.</returns>
        public static int SelectPrimaryGpu(IReadOnlyList<SensorColumn> sensorColumns, IReadOnlyList<double[]> values)
        {
            int primaryGpu = -1;
            double primaryLoad = double.NaN;

            foreach (var gpuIndex in sensorColumns.Where(column => column.GpuIndex >= 0)
                .Select(column => column.GpuIndex).Distinct().OrderBy(index => index))
            {
                bool hasValues = false;
                double load = double.NaN;
                for (int i = 0; i < sensorColumns.Count; i++)
                {
                    if (sensorColumns[i].GpuIndex != gpuIndex)
                        continue;

                    var readings = values[i].Where(double.IsFinite).ToArray();
                    if (readings.Length == 0)
                        continue;

                    hasValues = true;
                    if (sensorColumns[i].Type == "Load")
                        load = readings.Average();
                }

                if (!hasValues)
                    continue;

                bool higherLoad = !double.IsNaN(load) && (double.IsNaN(primaryLoad) || load > primaryLoad);
                if (primaryGpu < 0 || higherLoad)
                {
                    primaryGpu = gpuIndex;
                    primaryLoad = load;
                }
            }

            return primaryGpu;
        }

        /// <summary>
        /// Names the GPU#... columns: those of the primary GPU "GPU Core" and "GPU Memory" like a native
        /// capture, those of every other GPU "GPU1 Core", "GPU1 Memory", ..., which the analysis does
        /// not read.
        /// </summary>
        public static void NameGpuColumns(IEnumerable<SensorColumn> sensorColumns, int primaryGpuIndex)
        {
            foreach (var sensorColumn in sensorColumns.Where(column => column.GpuIndex >= 0))
            {
                sensorColumn.Name = sensorColumn.GpuIndex == primaryGpuIndex
                    ? $"GPU {sensorColumn.GpuPart}"
                    : $"GPU{sensorColumn.GpuIndex} {sensorColumn.GpuPart}";
            }
        }

        private static SensorColumn AddSensorColumn(List<SensorColumn> sensorColumns, HashSet<string> usedKeys,
            string key, int columnIndex, SensorDefinition sensor)
        {
            // Duplicate keys cannot be represented as distinct session sensor entries,
            // keep the first occurrence
            if (!usedKeys.Add(key))
                return null;

            var sensorColumn = new SensorColumn
            {
                ColumnIndex = columnIndex,
                Key = key,
                Name = sensor.Name,
                Type = sensor.Type,
                Factor = sensor.Factor,
                IsMeasurement = sensor.IsMeasurement
            };
            sensorColumns.Add(sensorColumn);
            return sensorColumn;
        }

        private static (string Part, string Type) GetGpuSensorPartAndType(string kind)
        {
            switch (kind)
            {
                case "clk": return ("Core", "Clock");
                case "memclk":
                case "memclock": return ("Memory", "Clock");
                case "util": return ("Core", "Load");
                case "temp": return ("Core", "Temperature");
                default: return (null, null);
            }
        }

        private static string NormalizeHeader(string header)
        {
            if (string.IsNullOrEmpty(header))
                return string.Empty;

            var normalized = new StringBuilder(header.Length);
            foreach (var character in header)
            {
                if (!char.IsWhiteSpace(character))
                    normalized.Append(char.ToLowerInvariant(character));
            }

            return normalized.ToString();
        }
    }
}
