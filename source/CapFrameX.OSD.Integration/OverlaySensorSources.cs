using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace CapFrameX.OSD.Integration
{
    public enum OverlayDeviceKind { Other, Cpu, Gpu, Memory }

    /// <summary>A CPU core or thread reading of a repeated sensor family, as the telemetry catalog groups it.</summary>
    public readonly record struct OverlayCoreReading(string Family, string GroupName, int Order, string CoreType, int CoreIndex, int? ThreadIndex);

    /// <summary>
    /// Identifiers of hardware sources. A sensor ID names the device, the sensor type and the sensor
    /// ("sensor:" + escaped "NVIDIA GeForce RTX 5090/load/GPU Core"), a device key the hardware type and
    /// the device ("device:" + escaped "GpuNvidia/NVIDIA GeForce RTX 5090"). Both stay the same on one
    /// machine; another machine can only match them by device kind, vendor and sensor name.
    /// </summary>
    public static class OverlaySensorSources
    {
        public const string SensorPrefix = "sensor:", UnnamedSensorPrefix = "sensor-id:", DevicePrefix = "device:";
        private const string RuntimeSeparator = ":runtime:";
        // LibreHardwareMonitor's sensor types, lower case as stable identifiers spell them.
        private static readonly string[] SensorTypes =
        {
            "voltage", "current", "power", "clock", "temperature", "load", "frequency", "fan", "flow", "control", "level",
            "factor", "data", "smalldata", "throughput", "timespan", "timing", "energy", "noise", "conductivity",
            "humidity", "latency", "datarate"
        };

        // Native CPU sensor names are defined by GenericCpu, IntelCpu and Amd17Cpu.
        // Restrict suffixes so temperatures, effective clocks and aggregates cannot mix.
        private static readonly Regex CoreSensorName = new Regex(
            @"^(?:CPU\s+)?Core\s+#(?<core>\d{1,5})(?:\s+(?<type>LPE|LP|P|E|D))?(?:\s+Thread\s+#(?<thread>\d{1,3}))?\s*(?<suffix>\(Effective\)|Distance to TjMax|\(SMU\)|VID)?$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        public static string SensorId(string stableIdentifier, string runtimeIdentifier = null)
            => SensorPrefix + Uri.EscapeDataString(stableIdentifier)
                + (runtimeIdentifier == null ? string.Empty : RuntimeSeparator + Uri.EscapeDataString(runtimeIdentifier));

        public static string UnnamedSensorId(string identifier) => UnnamedSensorPrefix + Uri.EscapeDataString(identifier);

        public static string DeviceKey(string hardwareType, string hardwareName, string runtimeDevice = null)
            => DevicePrefix + Uri.EscapeDataString((hardwareType ?? string.Empty) + "/" + (hardwareName ?? string.Empty))
                + (runtimeDevice == null ? string.Empty : RuntimeSeparator + Uri.EscapeDataString(runtimeDevice));

        /// <summary>Reads device, sensor type (lower case) and sensor name from a sensor ID.</summary>
        public static bool TryParseSensorId(string id, out string hardwareName, out string sensorType, out string sensorName)
        {
            hardwareName = sensorType = sensorName = null;
            if (id == null || !id.StartsWith(SensorPrefix, StringComparison.Ordinal)) return false;
            string stable = Uri.UnescapeDataString(WithoutRuntime(id.Substring(SensorPrefix.Length)));
            // The first "/<type>/" ends the device name; a sensor name may contain a slash ("CPU Package (Tctl/Tdie)").
            int separator = -1;
            foreach (string type in SensorTypes)
            {
                int index = stable.IndexOf("/" + type + "/", StringComparison.Ordinal);
                if (index > 0 && (separator < 0 || index < separator))
                {
                    separator = index;
                    sensorType = type;
                }
            }
            if (separator < 0) return false;
            hardwareName = stable.Substring(0, separator);
            sensorName = stable.Substring(separator + sensorType.Length + 2);
            return sensorName.Length > 0;
        }

        /// <summary>Reads hardware type and device name from a device key.</summary>
        public static bool TryParseDeviceKey(string key, out string hardwareType, out string hardwareName)
        {
            hardwareType = hardwareName = null;
            if (key == null || !key.StartsWith(DevicePrefix, StringComparison.Ordinal)) return false;
            string device = Uri.UnescapeDataString(WithoutRuntime(key.Substring(DevicePrefix.Length)));
            int separator = device.IndexOf('/');
            if (separator <= 0) return false;
            hardwareType = device.Substring(0, separator);
            hardwareName = device.Substring(separator + 1);
            return hardwareName.Length > 0;
        }

        private static string WithoutRuntime(string value)
        {
            int runtime = value.IndexOf(RuntimeSeparator, StringComparison.Ordinal);
            return runtime < 0 ? value : value.Substring(0, runtime);
        }

        /// <summary>The family of a CPU core or thread sensor ("Core #3 E", "Core #1 P Thread #2 (Effective)").</summary>
        public static bool TryGetCoreReading(string sensorType, string sensorName, out OverlayCoreReading reading)
        {
            reading = default;
            if (string.IsNullOrWhiteSpace(sensorName)) return false;
            var match = CoreSensorName.Match(sensorName.Trim());
            if (!match.Success) return false;
            int core = int.Parse(match.Groups["core"].Value, CultureInfo.InvariantCulture);
            int thread = match.Groups["thread"].Success
                ? int.Parse(match.Groups["thread"].Value, CultureInfo.InvariantCulture) : 0;
            string suffix = match.Groups["suffix"].Value;
            var family = (sensorType?.ToLowerInvariant(), suffix) switch
            {
                ("load", "") => ("cpu-loads", "CPU core / thread loads"),
                ("clock", "") => ("cpu-clocks", "CPU core clocks"),
                ("clock", "(Effective)") => ("cpu-effective-clocks", "CPU effective clocks"),
                ("temperature", "") => ("cpu-temperatures", "CPU core temperatures"),
                ("temperature", "Distance to TjMax") => ("cpu-thermal-headroom", "CPU thermal headroom"),
                ("power", "(SMU)") => ("cpu-core-power", "CPU core power"),
                ("voltage", "VID") => ("cpu-core-vid", "CPU core VID"),
                _ => ((string)null, (string)null)
            };
            if (family.Item1 == null) return false;
            // These labels come from native CPU topology. Never infer a core type from
            // the presence of SMT threads, the model name or a clock reading.
            reading = new OverlayCoreReading(family.Item1, family.Item2, core * 1024 + thread,
                match.Groups["type"].Success ? match.Groups["type"].Value : null, core,
                match.Groups["thread"].Success ? thread : null);
            return true;
        }

        public static string NormalizeCoreType(string coreType) => coreType?.Trim().ToUpperInvariant() switch
        {
            "P" => "P", "E" => "E", "LPE" => "LPE", "D" => "D", "LP" => "LP", _ => null
        };

        public static OverlayDeviceKind DeviceKind(string hardwareType) => hardwareType switch
        {
            "Cpu" => OverlayDeviceKind.Cpu,
            "Memory" => OverlayDeviceKind.Memory,
            _ when hardwareType?.StartsWith("Gpu", StringComparison.Ordinal) == true => OverlayDeviceKind.Gpu,
            _ => OverlayDeviceKind.Other
        };

        /// <summary>The vendor of a CPU or GPU: a GPU's hardware type names it, a CPU's name does.</summary>
        public static string Vendor(string hardwareType, string hardwareName) => DeviceKind(hardwareType) switch
        {
            OverlayDeviceKind.Gpu => hardwareType switch
            {
                "GpuNvidia" => "NVIDIA", "GpuAmd" => "AMD", "GpuIntel" => "Intel",
                _ => InferVendor(OverlayDeviceKind.Gpu, hardwareName)
            },
            OverlayDeviceKind.Cpu => InferVendor(OverlayDeviceKind.Cpu, hardwareName),
            _ => null
        };

        /// <summary>The vendor of a device known only by its name, as a sensor ID names it.</summary>
        public static string InferVendor(OverlayDeviceKind kind, string hardwareName)
        {
            if (string.IsNullOrWhiteSpace(hardwareName)) return null;
            bool Has(params string[] words) => words.Any(word => Regex.IsMatch(hardwareName,
                @"\b" + Regex.Escape(word) + @"\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant));
            return kind switch
            {
                OverlayDeviceKind.Gpu when Has("NVIDIA", "GeForce", "Quadro", "TITAN") => "NVIDIA",
                OverlayDeviceKind.Gpu when Has("AMD", "Radeon", "ATI") => "AMD",
                OverlayDeviceKind.Gpu when Has("Intel", "Arc", "Iris", "UHD") => "Intel",
                OverlayDeviceKind.Cpu when Has("Intel", "Xeon", "Pentium", "Celeron") => "Intel",
                OverlayDeviceKind.Cpu when Has("AMD", "Ryzen", "Threadripper", "EPYC", "Athlon") => "AMD",
                _ => null
            };
        }

        /// <summary>
        /// The kind of a device known only by its name and the sensors a design reads from it: GPU sensors
        /// start with "GPU", CPU sensors with "CPU" or a core; the name decides only when they do not tell.
        /// </summary>
        public static OverlayDeviceKind InferDeviceKind(string hardwareName, IEnumerable<string> sensorNames)
        {
            var names = (sensorNames ?? Enumerable.Empty<string>()).Where(name => !string.IsNullOrWhiteSpace(name)).ToArray();
            if (names.Any(name => name.StartsWith("GPU", StringComparison.OrdinalIgnoreCase) || name.StartsWith("D3D", StringComparison.OrdinalIgnoreCase)))
                return OverlayDeviceKind.Gpu;
            if (names.Any(name => name.StartsWith("CPU", StringComparison.OrdinalIgnoreCase) || name.StartsWith("CCD", StringComparison.OrdinalIgnoreCase)
                    || CoreSensorName.IsMatch(name.Trim())))
                return OverlayDeviceKind.Cpu;
            if (string.IsNullOrWhiteSpace(hardwareName)) return OverlayDeviceKind.Other;
            // CPU names first: an APU is "AMD Ryzen 7 8700G w/ Radeon 780M Graphics", no GPU name says Ryzen or Core.
            if (Regex.IsMatch(hardwareName, @"\b(?:Ryzen|Threadripper|EPYC|Athlon|Core|Xeon|Pentium|Celeron)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
                return OverlayDeviceKind.Cpu;
            if (Regex.IsMatch(hardwareName, @"\b(?:GeForce|Quadro|TITAN|Radeon|Arc|Iris|UHD|Graphics)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
                return OverlayDeviceKind.Gpu;
            return hardwareName.EndsWith("Memory", StringComparison.OrdinalIgnoreCase) ? OverlayDeviceKind.Memory : OverlayDeviceKind.Other;
        }
    }
}
