using CapFrameX.Contracts.Overlay;
using CapFrameX.Contracts.Sensor;
using LibreHardwareMonitor.Hardware;
using System;

namespace CapFrameX.Overlay
{
    /// <summary>
    /// Maps a hardware sensor onto the overlay entry that presents it: identifier, version-stable
    /// identifier, description with unit, default group name and the value formats.
    /// </summary>
    /// <remarks>
    /// This is the single source of the description and group-name scheme that the overlay
    /// templates and the profile reconciliation in <see cref="OverlayEntryProvider"/> key on
    /// ("Core #7 E (MHz)" is a core clock, "Core #7 E" its group). The integration tests build
    /// their entries through it as well, so a change here is exercised against the templates.
    /// </remarks>
    public static class SensorOverlayEntryFactory
    {
        public static IOverlayEntry Create(ISensorEntry sensor)
        {
            return new OverlayEntryWrapper(sensor.Identifier.ToString())
            {
                StableIdentifier = SensorIdentifierHelper.BuildStableIdentifier(sensor),
                SortKey = sensor.SortKey,
                Description = GetDescription(sensor),
                OverlayEntryType = MapType(sensor.HardwareType),
                GroupName = GetGroupName(sensor),
                ShowGraph = false,
                ShowGraphIsEnabled = false,
                ShowOnOverlayIsEnabled = true,
                ShowOnOverlay = sensor.IsPresentationDefault,
                Value = 0,
                ValueUnitFormat = GetValueUnitString(sensor.SensorType),
                ValueAlignmentAndDigits = GetValueAlignmentAndDigitsString(sensor.SensorType)
            };
        }

        public static string GetValueAlignmentAndDigitsString(string sensorTypeString)
        {
            string formatString = "{0}";
            Enum.TryParse(sensorTypeString, out SensorType sensorType);
            switch (sensorType)
            {
                case SensorType.Current:
                    formatString = "{0,5:F1}";
                    break;
                case SensorType.Voltage:
                    formatString = "{0,5:F2}";
                    break;
                case SensorType.Clock:
                    formatString = "{0,5:F0}";
                    break;
                case SensorType.Temperature:
                    formatString = "{0,5:F0}";
                    break;
                case SensorType.Load:
                    formatString = "{0,5:F0}";
                    break;
                case SensorType.Fan:
                    formatString = "{0,5:F0}";
                    break;
                case SensorType.Flow:
                    formatString = "{0,5:F0}";
                    break;
                case SensorType.Control:
                    formatString = "{0,5:F0}";
                    break;
                case SensorType.Level:
                    formatString = "{0,5:F0}";
                    break;
                case SensorType.Factor:
                    formatString = "{0,5:F0}";
                    break;
                case SensorType.Power:
                    formatString = "{0,5:F1}";
                    break;
                case SensorType.Data:
                    formatString = "{0,5:F2}";
                    break;
                case SensorType.SmallData:
                    formatString = "{0,5:F0}";
                    break;
                case SensorType.Throughput:
                    formatString = "{0,5:F1}";
                    break;
                case SensorType.Frequency:
                    formatString = "{0,5:F0}";
                    break;
                case SensorType.DataRate:
                    formatString = "{0,5:F0}";
                    break;
                case SensorType.Timing:
                    formatString = "{0,5:F1}";
                    break;
                case SensorType.Latency:
                    formatString = "{0,5:F1}";
                    break;
            }

            return formatString;
        }

        public static string GetValueUnitString(string sensorTypeString)
        {
            string formatString = "{0}";
            Enum.TryParse(sensorTypeString, out SensorType sensorType);
            switch (sensorType)
            {
                case SensorType.Current:
                    formatString = "A  ";
                    break;
                case SensorType.Voltage:
                    formatString = "V  ";
                    break;
                case SensorType.Clock:
                    formatString = "MHz";
                    break;
                case SensorType.Temperature:
                    formatString = "°C ";
                    break;
                case SensorType.Load:
                    formatString = "%  ";
                    break;
                case SensorType.Fan:
                    formatString = "RPM";
                    break;
                case SensorType.Flow:
                    formatString = "L/h";
                    break;
                case SensorType.Control:
                    formatString = "%  ";
                    break;
                case SensorType.Level:
                    formatString = "%  ";
                    break;
                case SensorType.Factor:
                    formatString = "   ";
                    break;
                case SensorType.Power:
                    formatString = "W  ";
                    break;
                case SensorType.Data:
                    formatString = "GB ";
                    break;
                case SensorType.SmallData:
                    formatString = "MB ";
                    break;
                case SensorType.Throughput:
                    formatString = "GB/s";
                    break;
                case SensorType.Frequency:
                    formatString = "Hz ";
                    break;
                case SensorType.DataRate:
                    formatString = "MT/s";
                    break;
                case SensorType.Timing:
                    formatString = "ns ";
                    break;
                case SensorType.Latency:
                    formatString = "ms ";
                    break;
            }

            return formatString;
        }

        public static string GetGroupName(ISensorEntry sensor)
        {
            var name = sensor.Name;
            if (name.Contains("CPU Core #"))
            {
                name = name.Replace("Core #", "").Trim();
            }
            else if (name.Contains("CPU Max Clock"))
            {
                name = name.Replace("CPU Max Clock", "CPU Max");
            }
            else if (name.Contains("CPU Max Core Temp"))
            {
                name = name.Replace("Max Core Temp", "Max");
            }
            else if (name.Contains("GPU Core"))
            {
                name = name.Replace(" Core", "");
            }
            else if (name.Contains("Memory Controller"))
            {
                name = name.Replace("Memory Controller", "MemCtrl");
            }
            else if (name.Contains("Memory"))
            {
                name = name.Replace("Memory", "Mem");

                if (name.Contains("Dedicated"))
                    name = name.Replace("GPU Mem Dedicated", "GPU Mem");

                else if (name.Contains("Shared"))
                    name = name.Replace("GPU Mem Shared", "GPU Mem");
            }
            else if (name.Contains("Power Limit"))
            {
                name = name.Replace("Power Limit", "PL");
            }
            else if (name.Contains("Thermal Limit"))
            {
                name = name.Replace("Thermal Limit", "TL");
            }
            else if (name.Contains("Voltage Limit"))
            {
                name = name.Replace("Voltage Limit", "VL");
            }

            if (name.Contains("D3D"))
            {
                if (name.Contains("D3D Dedicated"))
                    name = name.Replace("D3D Dedicated", "Dedicated");

                if (name.Contains("D3D Shared"))
                    name = name.Replace("D3D Shared", "Shared");
            }

            if (name.Contains(" - Thread #1"))
            {
                name = name.Replace(" - Thread #1", "").Trim();
            }

            if (name.Contains(" - Thread #2"))
            {
                name = name.Replace(" - Thread #2", "").Trim();
            }

            if (name.Contains("Thread #1"))
            {
                name = name.Replace("Thread #1", "").Trim();
            }

            if (name.Contains("Thread #2"))
            {
                name = name.Replace("Thread #2", "").Trim();
            }

            if (name.Contains("Monitor Refresh Rate"))
            {
                name = "MRR";
            }

            if (name.Contains("GPU Mem Junction"))
            {
                name = "VRAM Hot Spot";
            }

            return name;
        }

        public static string GetDescription(ISensorEntry sensor)
        {
            string description = string.Empty;
            Enum.TryParse(sensor.SensorType, out SensorType sensorType);
            switch (sensorType)
            {
                case SensorType.Current:
                    description = $"{sensor.Name} (A)";
                    break;
                case SensorType.Voltage:
                    description = $"{sensor.Name} (V)";
                    break;
                case SensorType.Clock:
                    description = $"{sensor.Name} (MHz)";
                    break;
                case SensorType.Temperature:
                    description = $"{sensor.Name} (°C)";
                    break;
                case SensorType.Load:
                    description = $"{sensor.Name} (%)";
                    break;
                case SensorType.Fan:
                    description = $"{sensor.Name} (RPM)";
                    break;
                case SensorType.Flow:
                    description = $"{sensor.Name} (L/h)";
                    break;
                case SensorType.Control:
                    description = $"{sensor.Name} (%)";
                    break;
                case SensorType.Level:
                    description = $"{sensor.Name} (%)";
                    break;
                case SensorType.Factor:
                    description = sensor.Name;
                    break;
                case SensorType.Power:
                    description = $"{sensor.Name} (W)";
                    break;
                case SensorType.Data:
                    description = $"{sensor.Name} (GB)";
                    break;
                case SensorType.SmallData:
                    description = $"{sensor.Name} (MB)";
                    break;
                case SensorType.Throughput:
                    description = $"{sensor.Name} (GB/s)";
                    break;
                case SensorType.Frequency:
                    description = $"{sensor.Name} (Hz)";
                    break;
                case SensorType.DataRate:
                    description = $"{sensor.Name} (MT/s)";
                    break;
                case SensorType.Timing:
                    description = $"{sensor.Name} (ns)";
                    break;
                case SensorType.Latency:
                    description = $"{sensor.Name} (ms)";
                    break;
            }

            return description;
        }

        public static EOverlayEntryType MapType(string hardwareTypeString)
        {
            EOverlayEntryType type = EOverlayEntryType.Undefined;
            Enum.TryParse(hardwareTypeString, out HardwareType hardwareType);
            switch (hardwareType)
            {
                case HardwareType.Motherboard:
                    type = EOverlayEntryType.Mainboard;
                    break;
                case HardwareType.SuperIO:
                    type = EOverlayEntryType.Undefined;
                    break;
                case HardwareType.Cpu:
                    type = EOverlayEntryType.CPU;
                    break;
                case HardwareType.Memory:
                    type = EOverlayEntryType.RAM;
                    break;
                case HardwareType.GpuNvidia:
                    type = EOverlayEntryType.GPU;
                    break;
                case HardwareType.GpuAmd:
                    type = EOverlayEntryType.GPU;
                    break;
                case HardwareType.GpuIntel:
                    type = EOverlayEntryType.GPU;
                    break;
                case HardwareType.Storage:
                    type = EOverlayEntryType.HDD;
                    break;
            }

            return type;
        }
    }
}
