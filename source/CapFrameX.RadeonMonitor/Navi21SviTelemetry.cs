using System.Globalization;
using System.Text;

namespace CapFrameX.RadeonMonitor
{
    internal static class Navi21SviTelemetry
    {
        private const uint RegisterBase = 0x5A00C;
        private const int RegisterCount = 4;

        private static readonly RailDefinition[] PhysicalRails =
        {
            new(0, "SVI0 plane 1"),
            new(1, "SVI0 plane 0"),
            new(2, "SVI1 plane 0"),
            new(3, "SVI1 plane 1")
        };

        private static readonly CalibrationProfile[] CalibrationProfiles =
        {
            // Board-specific ATOM SMC_DPM_Info v4.9 calibration.
            new(
                SubsystemVendorId: 0x148C,
                SubsystemDeviceId: 0x2406,
                Name: "PowerColor Navi21 148C:2406",
                Rails: new[]
                {
                    new RailDefinition(
                        1,
                        "GPU Core",
                        "VDDCR_GFX",
                        MaximumCurrent: 714,
                        CurrentOffset: 0),
                    new RailDefinition(
                        2,
                        "GPU Memory",
                        "VDDIO",
                        MaximumCurrent: 128,
                        CurrentOffset: 0),
                    new RailDefinition(
                        0,
                        "GPU SoC",
                        "VDDCR_SOC",
                        MaximumCurrent: 99,
                        CurrentOffset: 0),
                    new RailDefinition(
                        3,
                        "GPU Memory",
                        "VDDCI_MEM",
                        MaximumCurrent: 64,
                        CurrentOffset: 0)
                })
        };

        public static bool IsSupportedDevice(ushort deviceId)
        {
            return deviceId is >= 0x73A0 and <= 0x73BF;
        }

        public static Navi21SviSnapshot Parse(
            IReadOnlyList<uint> registers,
            RadeonDeviceInfo deviceInfo)
        {
            ArgumentNullException.ThrowIfNull(registers);
            ArgumentNullException.ThrowIfNull(deviceInfo);

            if (registers.Count != RegisterCount)
            {
                throw new ArgumentException(
                    $"Navi21 SVI telemetry contains {registers.Count} DWORDs; {RegisterCount} were expected.",
                    nameof(registers));
            }

            if (!IsSupportedDevice(deviceInfo.DeviceId))
            {
                throw new ArgumentException(
                    $"PCI device 0x{deviceInfo.DeviceId:X4} is not a supported Navi21 device.",
                    nameof(deviceInfo));
            }

            CalibrationProfile? profile = CalibrationProfiles.FirstOrDefault(candidate =>
                candidate.SubsystemVendorId == deviceInfo.SubsystemVendorId &&
                candidate.SubsystemDeviceId == deviceInfo.SubsystemDeviceId);
            IReadOnlyList<RailDefinition> rails = profile?.Rails ?? PhysicalRails;

            List<DecodedRail> decodedRails = new(rails.Count);
            foreach (RailDefinition rail in rails)
            {
                uint raw = registers[rail.RegisterIndex];
                byte voltageId = (byte)(raw >> 16);
                byte currentId = (byte)raw;
                double decodedVoltage = (6200.0 - voltageId * 25.0) / 4000.0;

                // VID 0 marks an unavailable plane; retain the stable row.
                double? voltage = voltageId == 0 || decodedVoltage is < 0.2 or > 1.55
                    ? null
                    : decodedVoltage;

                double? current = rail.MaximumCurrent is ushort maximumCurrent
                    ? Math.Max(0.0, currentId * maximumCurrent / 255.0 + rail.CurrentOffset)
                    : null;
                decodedRails.Add(new DecodedRail(rail, raw, voltage, current));
            }

            List<MetricReading> readings = new(decodedRails.Count * (profile is null ? 1 : 3));
            foreach (DecodedRail rail in decodedRails)
            {
                readings.Add(CreateReading(
                    "Voltage",
                    rail.Definition.GetMetricName("Voltage"),
                    rail.Voltage,
                    "V",
                    rail.Raw));
            }

            foreach (DecodedRail rail in decodedRails.Where(rail => rail.Current is not null))
            {
                readings.Add(CreateReading(
                    "Current",
                    rail.Definition.GetMetricName("Current"),
                    rail.Current!.Value,
                    "A",
                    rail.Raw));
            }

            foreach (DecodedRail rail in decodedRails.Where(rail => rail.Current is not null))
            {
                readings.Add(CreateReading(
                    "Power",
                    rail.Definition.GetMetricName("Power"),
                    rail.Voltage is double voltage
                        ? voltage * rail.Current!.Value
                        : null,
                    "W",
                    rail.Raw));
            }

            return new Navi21SviSnapshot(
                readings,
                FormatRegisterDump(registers, decodedRails, deviceInfo, profile),
                profile?.Name,
                decodedRails.Count(rail => rail.Voltage is null));
        }

        private static MetricReading CreateReading(
            string group,
            string name,
            double? value,
            string unit,
            uint raw)
        {
            return new MetricReading(
                group,
                name,
                value?.ToString("F3", CultureInfo.InvariantCulture) ?? "—",
                unit,
                $"0x{raw:X8}",
                value,
                DecimalPlaces: 3);
        }

        private static string FormatRegisterDump(
            IReadOnlyList<uint> registers,
            IReadOnlyList<DecodedRail> decodedRails,
            RadeonDeviceInfo deviceInfo,
            CalibrationProfile? profile)
        {
            string[] physicalPlaneNames =
            {
                "SVI0 plane 1",
                "SVI0 plane 0",
                "SVI1 plane 0",
                "SVI1 plane 1"
            };
            Dictionary<int, DecodedRail> railsByRegister = decodedRails.ToDictionary(
                rail => rail.Definition.RegisterIndex);

            StringBuilder builder = new();
            builder.AppendLine("Address   Physical plane  Raw         IDD  VID  Voltage  Decoded rail");
            for (int index = 0; index < registers.Count; index++)
            {
                uint raw = registers[index];
                byte currentId = (byte)raw;
                byte voltageId = (byte)(raw >> 16);
                bool railWasDecoded = railsByRegister.TryGetValue(index, out DecodedRail? rail);
                string decodedName = railWasDecoded ? rail!.Definition.DisplayName : "unmapped";
                string voltageText = railWasDecoded && rail!.Voltage is double voltage
                    ? voltage.ToString("F4", CultureInfo.InvariantCulture)
                    : "invalid";

                builder.Append("0x");
                builder.Append((RegisterBase + index * sizeof(uint)).ToString("X6", CultureInfo.InvariantCulture));
                builder.Append("  ");
                builder.Append(physicalPlaneNames[index].PadRight(14));
                builder.Append("  0x");
                builder.Append(raw.ToString("X8", CultureInfo.InvariantCulture));
                builder.Append("  ");
                builder.Append(currentId.ToString(CultureInfo.InvariantCulture).PadLeft(3));
                builder.Append("  ");
                builder.Append(voltageId.ToString(CultureInfo.InvariantCulture).PadLeft(3));
                builder.Append("  ");
                builder.Append(voltageText.PadLeft(7));
                builder.Append(railWasDecoded && rail!.Voltage is not null ? " V  " : "    ");
                builder.AppendLine(decodedName);
            }

            int invalidVoltageCount = decodedRails.Count(rail => rail.Voltage is null);
            if (invalidVoltageCount > 0)
            {
                builder.AppendLine();
                builder.Append(invalidVoltageCount);
                builder.Append(invalidVoltageCount == 1
                    ? " transient/invalid voltage sample; sensor rows retained."
                    : " transient/invalid voltage samples; sensor rows retained.");
            }

            if (profile is null)
            {
                builder.AppendLine();
                builder.Append("No board calibration profile for subsystem ");
                builder.Append($"{deviceInfo.SubsystemVendorId:X4}:{deviceInfo.SubsystemDeviceId:X4}");
                builder.Append("; physical-plane voltages only.");
            }
            else
            {
                builder.AppendLine();
                builder.Append("Calibration: ");
                builder.Append(profile.Name);
                builder.Append("; current = IDD * Imax / 255 + offset.");
            }

            return builder.ToString().TrimEnd();
        }

        private sealed record CalibrationProfile(
            ushort SubsystemVendorId,
            ushort SubsystemDeviceId,
            string Name,
            IReadOnlyList<RailDefinition> Rails);

        private sealed record RailDefinition(
            int RegisterIndex,
            string DisplayName,
            string? RailName = null,
            ushort? MaximumCurrent = null,
            sbyte CurrentOffset = 0)
        {
            public string GetMetricName(string quantity)
            {
                return RailName is null
                    ? $"{DisplayName} {quantity}"
                    : $"{DisplayName} {quantity} ({RailName})";
            }
        }

        private sealed record DecodedRail(
            RailDefinition Definition,
            uint Raw,
            double? Voltage,
            double? Current);
    }

    internal sealed record Navi21SviSnapshot(
        IReadOnlyList<MetricReading> Readings,
        string RegisterDump,
        string? CalibrationProfileName,
        int InvalidVoltageCount)
    {
        public bool IsCurrentCalibrated => CalibrationProfileName is not null;
    }
}
