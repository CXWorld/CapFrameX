using System;
using System.Collections.Generic;
using System.Linq;

namespace CapFrameX.OSD.Integration
{
    /// <summary>A reading a portable design names by meaning; it binds to the first listed sensor a device reports.</summary>
    public sealed record OverlaySemanticKeyRule(string Key, string SensorType, OverlayDeviceKind Device, IReadOnlyList<string> Names);

    /// <summary>
    /// Semantic keys ("gpuLoad", "cpuTemp") and the sensor names that report them on each vendor's hardware.
    /// The telemetry catalog assigns them; importing a design uses them to find a reading another
    /// model of the same vendor names differently ("GPU TBP" or "GPU Power").
    /// </summary>
    public static class OverlaySemanticKeys
    {
        public static IReadOnlyList<OverlaySemanticKeyRule> Rules { get; } = new[]
        {
            Rule("gpuLoad", "Load", OverlayDeviceKind.Gpu, "GPU Core"),
            Rule("gpuTemp", "Temperature", OverlayDeviceKind.Gpu, "GPU Core"),
            Rule("gpuPower", "Power", OverlayDeviceKind.Gpu, "GPU TBP", "GPU Power", "GPU Total", "GPU TDP"),
            Rule("cpuLoad", "Load", OverlayDeviceKind.Cpu, "CPU Total"),
            Rule("cpuTemp", "Temperature", OverlayDeviceKind.Cpu, "CPU Package (Tctl/Tdie)", "CPU Package", "CPU Max Core Temp"),
            Rule("ramUsed", "Data", OverlayDeviceKind.Memory, "RAM Used", "Memory Used"),
            Rule("ramTotal", "Data", OverlayDeviceKind.Memory, "RAM Total", "Memory Total"),
            Rule("vramUsed", "Data", OverlayDeviceKind.Gpu, "GPU Memory Dedicated", "GPU Memory Used", "GPU Memory Allocated"),
            Rule("vramTotal", "Data", OverlayDeviceKind.Gpu, "GPU Memory Total"),
            Rule("gpuClock", "Clock", OverlayDeviceKind.Gpu, "GPU Core"),
            Rule("gpuMemoryClock", "Clock", OverlayDeviceKind.Gpu, "GPU Memory"),
            Rule("gpuFan", "Fan", OverlayDeviceKind.Gpu, "GPU Fan", "GPU Fan 1", "GPU"),
            Rule("gpuFanPercent", "Control", OverlayDeviceKind.Gpu, "GPU Fan", "GPU Fan 1", "GPU"),
            Rule("vramLoad", "Load", OverlayDeviceKind.Gpu, "GPU Memory"),
            Rule("cpuClock", "Clock", OverlayDeviceKind.Cpu, "CPU Clock", "CPU Max", "CPU Max Clock"),
            // Hybrid CPUs report the fastest core of each type, so designs can name P- and E-cores on any model.
            Rule("cpuPCoreClock", "Clock", OverlayDeviceKind.Cpu, "CPU P Max"),
            Rule("cpuECoreClock", "Clock", OverlayDeviceKind.Cpu, "CPU E Max"),
            Rule("cpuPower", "Power", OverlayDeviceKind.Cpu, "CPU Package"),
            Rule("ramLoad", "Load", OverlayDeviceKind.Memory, "RAM Usage", "RAM", "Memory")
        };

        private static OverlaySemanticKeyRule Rule(string key, string sensorType, OverlayDeviceKind device, params string[] names)
            => new OverlaySemanticKeyRule(key, sensorType, device, names);

        /// <summary>The rule a sensor of that device kind satisfies, if any.</summary>
        public static OverlaySemanticKeyRule FindRule(OverlayDeviceKind device, string sensorType, string sensorName)
            => Rules.FirstOrDefault(rule => rule.Device == device && string.Equals(rule.SensorType, sensorType, StringComparison.OrdinalIgnoreCase)
                && rule.Names.Contains(sensorName, StringComparer.Ordinal));

        /// <summary>Gives each key to the one source that reports it; several devices with the reading leave it unassigned.</summary>
        internal static void Assign(IList<OverlayTelemetrySource> sources)
        {
            foreach (var rule in Rules)
            {
                var matching = sources.Where(source => (source.IsHardwareSensor || source.SumSourceIdA != null)
                    && OverlaySensorSources.DeviceKind(source.HardwareType) == rule.Device
                    && source.SensorType == rule.SensorType && rule.Names.Contains(source.Name, StringComparer.Ordinal)).ToArray();
                if (matching.Select(source => source.HardwareName).Distinct(StringComparer.Ordinal).Take(2).Count() > 1)
                    continue;
                foreach (var name in rule.Names)
                {
                    var candidates = matching.Where(source => source.Name == name).ToArray();
                    if (candidates.Length == 0) continue;
                    if (candidates.Length == 1 && !candidates[0].IsStableIdentifierAmbiguous)
                        candidates[0].SemanticKey = rule.Key;
                    break;
                }
            }
        }
    }
}
