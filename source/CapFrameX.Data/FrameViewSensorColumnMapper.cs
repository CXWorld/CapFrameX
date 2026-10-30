using System;
using System.Collections.Generic;
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
        }

        // GPU#Clk (MHz), GPU#MemClock (MHz), GPU#Util (%), GPU#Temp (C) - [# = GPU index]
        private static readonly Regex GpuSensorColumnRegex = new Regex(
            @"^gpu(\d+)(memclk|memclock|clk|util|temp)(?:\(mhz\)|\(%\)|\(c\)|\(°c\))?$",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);

        // Fixed sensor columns. Keys are normalized header names (see NormalizeHeader).
        private static readonly Dictionary<string, (string Name, string Type)> SensorColumns =
            new Dictionary<string, (string Name, string Type)>(StringComparer.Ordinal)
            {
                // NVIDIA board power - the GPU power metric used by the CapFrameX analysis
                ["nvpwr(w)(api)"] = ("GPU Power", "Power"),
                ["nv-total-usbcpwr(w)(api)"] = ("GPU Board Power (excl. USB-C)", "Power"),
                ["gpuonlypwr(w)(api)"] = ("GPU Chip Power", "Power"),
                ["amdpwr(w)(api)"] = ("AMD Board Power", "Power"),
                // "AMDPtr (W) (API)" as printed in the FrameView 1.7 user guide
                ["amdptr(w)(api)"] = ("AMD Board Power", "Power"),
                ["amdwptr(w)(api)"] = ("AMD Board Power", "Power"),
                ["pcatpowertotal(w)"] = ("PCAT Power Total", "Power"),

                // Per the FrameView user guide (1.2/1.4), "CPUClk" is the average CPU
                // frequency - not the per-core maximum CapFrameX names "CPU Max".
                ["cpuclk(mhz)"] = ("CPU Average", "Clock"),
                ["cpuutil(%)"] = ("CPU Total", "Load"),
                ["cpuutil%"] = ("CPU Total", "Load"),
                ["cpupackagetemp(c)"] = ("CPU Package", "Temperature"),
                ["cputemp(c)"] = ("CPU Package", "Temperature"),
                ["cpupackagepower(w)"] = ("CPU Package", "Power"),
                ["cpupackagepower(watts)"] = ("CPU Package", "Power"),
                // Intel hybrid CPUs (FrameView 1.7+): per-cluster core power. "CPU Package Power(W)"
                // often stays all-NA on these platforms, so the two entries are also merged into a
                // "CPU Package (E+P Cores)" power entry by the import (see RecordManager). The name
                // is deliberately kept distinct from a real package power reading, while still
                // matching the "CPU Package" lookup of SessionSensorData2.CpuPower.
                ["e-corepower(w)"] = ("E-Core Power", "Power"),
                ["p-corepower(w)"] = ("P-Core Power", "Power"),

                ["batterypercentage"] = ("Battery Charge Level", "Level"),
                ["batterydrainrate(w)"] = ("Battery Drain Rate", "Power"),
                ["batterychargerate(w)"] = ("Battery Charge Rate", "Power"),
            };

        /// <summary>
        /// Finds all FrameView sensor columns within a CSV header.
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
                        var (name, type) = GetGpuSensorNameAndType(match.Groups[2].Value);
                        AddSensorColumn(sensorColumns, usedKeys, normalizedHeader, columnIndex, name, type);
                    }
                    else if (SensorColumns.TryGetValue(normalizedHeader, out var sensor))
                    {
                        AddSensorColumn(sensorColumns, usedKeys, normalizedHeader, columnIndex, sensor.Name, sensor.Type);
                    }
                }

                columnIndex++;
            }

            return sensorColumns;
        }

        private static void AddSensorColumn(List<SensorColumn> sensorColumns, HashSet<string> usedKeys,
            string key, int columnIndex, string name, string type)
        {
            // Duplicate keys cannot be represented as distinct session sensor entries,
            // keep the first occurrence
            if (!usedKeys.Add(key))
                return;

            sensorColumns.Add(new SensorColumn
            {
                ColumnIndex = columnIndex,
                Key = key,
                Name = name,
                Type = type
            });
        }

        private static (string Name, string Type) GetGpuSensorNameAndType(string kind)
        {
            switch (kind)
            {
                case "clk": return ("GPU Core", "Clock");
                case "memclk":
                case "memclock": return ("GPU Memory", "Clock");
                case "util": return ("GPU Core", "Load");
                case "temp": return ("GPU Core", "Temperature");
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
