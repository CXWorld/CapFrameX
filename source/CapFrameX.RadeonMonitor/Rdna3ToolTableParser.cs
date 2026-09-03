using System.Globalization;

namespace CapFrameX.RadeonMonitor
{
    internal static class Rdna3ToolTableParser
    {
        public const uint SupportedVersion = 0x004E000C;

        private const int MinimumTableSize = 0x874 + sizeof(uint);

        private static readonly int[] ShaderClockOffsets =
        {
            0x334,
            0x338,
            0x33C,
            0x340,
            0x344,
            0x348
        };

        private static readonly int[] EffectiveShaderClockOffsets =
        {
            0x350,
            0x354,
            0x358,
            0x35C,
            0x360,
            0x364
        };

        public static RadeonToolTableTelemetry Parse(RadeonToolTableSnapshot snapshot)
        {
            ArgumentNullException.ThrowIfNull(snapshot);

            if (snapshot.Version != SupportedVersion)
            {
                throw new NotSupportedException(
                    $"RDNA3 SMU tool-table version 0x{snapshot.Version:X8} has no verified sensor map. " +
                    $"Version 0x{SupportedVersion:X8} is currently supported.");
            }

            if (snapshot.Dwords.Length * sizeof(uint) < MinimumTableSize)
            {
                throw new ArgumentException(
                    $"The RDNA3 SMU tool table contains only {snapshot.Dwords.Length * sizeof(uint)} bytes; " +
                    $"at least {MinimumTableSize} are required for version 0x{SupportedVersion:X8}.",
                    nameof(snapshot));
            }

            TableReader reader = new(snapshot.Dwords);
            List<MetricReading> readings = new();

            Add(readings, reader, "Temperature", "GPU Temperature", 0x01C, "\u00B0C", 1, 0.1, 200.0);
            Add(readings, reader, "Temperature", "GPU Memory Junction Temperature", 0x030, "\u00B0C", 1, 0.1, 200.0);
            Add(readings, reader, "Temperature", "GPU VR VDDC Temperature", 0x034, "\u00B0C", 1, 0.1, 200.0);
            Add(readings, reader, "Temperature", "GPU Hot Spot Temperature", 0x020, "\u00B0C", 1, 0.1, 200.0);
            Add(readings, reader, "Temperature", "GPU VR SoC Temperature", 0x040, "\u00B0C", 1, 0.1, 200.0);
            Add(readings, reader, "Temperature", "GPU VR VDDIO Temperature", 0x038, "\u00B0C", 1, 0.1, 200.0);
            Add(readings, reader, "Temperature", "GPU VR VDDCI Temperature", 0x03C, "\u00B0C", 1, 0.1, 200.0);
            Add(readings, reader, "Temperature", "GPU GCD Hotspot", 0x028, "\u00B0C", 1, 0.1, 200.0);
            for (int index = 0; index < 6; index++)
            {
                Add(
                    readings,
                    reader,
                    "Temperature",
                    $"GPU MCD{index + 1} Hotspot",
                    0x120 + index * sizeof(uint),
                    "\u00B0C",
                    1,
                    0.1,
                    200.0);
            }

            Add(readings, reader, "Voltage", "GPU Core Voltage (VDDCR_GFX)", 0x1F8, "V", 3, 0.1, 2.0);
            Add(readings, reader, "Voltage", "GPU Memory Voltage (VDDIO)", 0x230, "V", 3, 0.1, 2.0);
            Add(readings, reader, "Voltage", "GPU SoC Voltage (VDDCR_SOC)", 0x214, "V", 3, 0.1, 2.0);
            Add(readings, reader, "Voltage", "GPU Memory Voltage (VDDCI_MEM)", 0x24C, "V", 3, 0.1, 2.0);

            Add(readings, reader, "Fan", "GPU Fan", 0x7E0, "RPM", 0, 0.0, 10000.0);

            Add(readings, reader, "Current", "GPU Core Current (VDDCR_GFX)", 0x204, "A", 3, 0.0, 2000.0);
            Add(readings, reader, "Current", "GPU Memory Current (VDDIO)", 0x23C, "A", 3, 0.0, 2000.0);
            Add(readings, reader, "Current", "GPU SoC Current (VDDCR_SOC)", 0x220, "A", 3, 0.0, 2000.0);
            Add(readings, reader, "Current", "GPU Memory Current (VDDCI_MEM)", 0x258, "A", 3, 0.0, 2000.0);

            Add(readings, reader, "Limits", "GPU Core TDC Limit", 0x818, "A", 3, 0.1, 2000.0);
            Add(readings, reader, "Limits", "GPU SOC TDC Limit", 0x808, "A", 3, 0.1, 2000.0);
            Add(readings, reader, "Limits", "GPU USR TDC Limit", 0x810, "A", 3, 0.1, 2000.0);

            Add(readings, reader, "Power", "GPU Core Power (VDDCR_GFX)", 0x210, "W", 3, 0.0, 2000.0);
            Add(readings, reader, "Power", "GPU Memory Power (VDDIO)", 0x248, "W", 3, 0.0, 2000.0);
            Add(readings, reader, "Power", "GPU SoC Power (VDDCR_SOC)", 0x22C, "W", 3, 0.0, 2000.0);
            Add(readings, reader, "Power", "GPU Memory Power (VDDCI_MEM)", 0x264, "W", 3, 0.0, 2000.0);
            Add(readings, reader, "Power", "GPU USR Power (VDDCR_USR)", 0x280, "W", 3, 0.0, 2000.0);
            Add(readings, reader, "Power", "Total Graphics Power (TGP)", 0x000, "W", 3, 0.0, 2000.0);
            Add(readings, reader, "Power", "Total Board Power (TBP)", 0x014, "W", 3, 0.0, 2000.0);
            Add(readings, reader, "Power", "GPU Power Maximum", 0x004, "W", 3, 0.0, 2000.0);
            Add(readings, reader, "Power", "GPU PPT (Sustained)", 0x7EC, "W", 3, 0.0, 2000.0);

            Add(readings, reader, "Limits", "GPU PPT Limit (Sustained)", 0x7E8, "W", 3, 0.1, 2000.0);
            Add(readings, reader, "Limits", "GPU PPT Limit (Short)", 0x7F0, "W", 3, 0.1, 2000.0);

            Add(readings, reader, "Clocks", "GPU Front End Clock", 0x314, "MHz", 1, 0.0, 5000.0);
            Add(readings, reader, "Clocks", "GPU Front End Clock (Effective)", 0x34C, "MHz", 1, 0.0, 5000.0);
            Add(readings, reader, "Clocks", "GPU Memory Clock", 0x2A4, "MHz", 1, 0.0, 5000.0);
            Add(readings, reader, "Clocks", "GPU SoC Clock", 0x284, "MHz", 1, 0.0, 5000.0);
            Add(readings, reader, "Clocks", "GPU FCLK", 0x298, "MHz", 1, 0.0, 5000.0);
            Add(readings, reader, "Clocks", "GPU FCLK (Effective)", 0x2E0, "MHz", 1, 0.0, 5000.0);
            for (int index = 0; index < ShaderClockOffsets.Length; index++)
            {
                Add(
                    readings,
                    reader,
                    "Clocks",
                    $"GPU Shader {index + 1} Clock",
                    ShaderClockOffsets[index],
                    "MHz",
                    1,
                    0.0,
                    5000.0);
            }

            for (int index = 0; index < EffectiveShaderClockOffsets.Length; index++)
            {
                Add(
                    readings,
                    reader,
                    "Clocks",
                    $"GPU Shader {index + 1} Clock (Effective)",
                    EffectiveShaderClockOffsets[index],
                    "MHz",
                    1,
                    0.0,
                    5000.0);
            }

            Add(readings, reader, "Clocks", "GPU Shader Clock Frequency Limit", 0x5F8, "MHz", 1, 0.0, 5000.0);
            Add(readings, reader, "Clocks", "GPU Shader Clock Frequency Limit (User)", 0x5FC, "MHz", 1, 0.0, 5000.0);
            Add(readings, reader, "Activity", "GPU Utilization", 0x874, "%", 1, 0.0, 100.0);

            AddRatio(readings, reader, "Limit utilization", "GPU PPT Limit (Sustained)", 0x7EC, 0x7E8);
            AddRatio(readings, reader, "Limit utilization", "GPU Core TDC Limit", 0x81C, 0x818);
            AddRatio(readings, reader, "Limit utilization", "GPU SOC TDC Limit", 0x80C, 0x808);

            RatioValue hotspotThermal = ReadRatio(reader, 0x70C, 0x708);
            RatioValue memoryThermal = ReadRatio(reader, 0x724, 0x720);
            RatioValue vrGfxThermal = ReadRatio(reader, 0x72C, 0x728);
            RatioValue vrSocThermal = ReadRatio(reader, 0x734, 0x730);
            RatioValue vrMemThermal = ReadRatio(reader, 0x744, 0x740);
            AddRatio(readings, "Thermal limits", "GPU Hotspot Thermal Limit", hotspotThermal);
            AddRatio(readings, "Thermal limits", "GPU Memory Thermal Limit", memoryThermal);
            AddRatio(readings, "Thermal limits", "GPU VR GFX Thermal Limit", vrGfxThermal);
            AddRatio(readings, "Thermal limits", "GPU VR SOC Thermal Limit", vrSocThermal);
            AddRatio(readings, "Thermal limits", "GPU VR MEM Thermal Limit", vrMemThermal);

            return new RadeonToolTableTelemetry(
                readings,
                readings.Count(reading => reading.NumericValue is null));
        }

        private static void Add(
            ICollection<MetricReading> readings,
            TableReader reader,
            string group,
            string name,
            int offset,
            string unit,
            int decimalPlaces,
            double minimum,
            double maximum)
        {
            TableValue sample = reader.Read(offset, minimum, maximum);
            readings.Add(CreateReading(
                group,
                name,
                sample.Value,
                unit,
                decimalPlaces,
                $"+0x{offset:X3}=0x{sample.Raw:X8}"));
        }

        private static void AddRatio(
            ICollection<MetricReading> readings,
            TableReader reader,
            string group,
            string name,
            int valueOffset,
            int limitOffset)
        {
            AddRatio(readings, group, name, ReadRatio(reader, valueOffset, limitOffset));
        }

        private static void AddRatio(
            ICollection<MetricReading> readings,
            string group,
            string name,
            RatioValue ratio)
        {
            readings.Add(CreateReading(group, name, ratio.Value, "%", 1, ratio.Raw));
        }

        private static RatioValue ReadRatio(TableReader reader, int valueOffset, int limitOffset)
        {
            TableValue value = reader.Read(valueOffset, 0.0, 10000.0);
            TableValue limit = reader.Read(limitOffset, 0.1, 10000.0);
            double? percentage = value.Value is double numerator && limit.Value is double denominator
                ? numerator * 100.0 / denominator
                : null;
            if (percentage is < 0.0 or > 1000.0 || !double.IsFinite(percentage ?? 0.0))
            {
                percentage = null;
            }

            return new RatioValue(percentage, $"+0x{valueOffset:X3}/+0x{limitOffset:X3}");
        }

        private static MetricReading CreateReading(
            string group,
            string name,
            double? value,
            string unit,
            int decimalPlaces,
            string raw)
        {
            return new MetricReading(
                group,
                name,
                value?.ToString($"F{decimalPlaces}", CultureInfo.InvariantCulture) ?? "\u2014",
                unit,
                raw,
                value,
                decimalPlaces);
        }

        private sealed class TableReader
        {
            private readonly IReadOnlyList<uint> dwords;

            public TableReader(IReadOnlyList<uint> dwords)
            {
                this.dwords = dwords;
            }

            public TableValue Read(int byteOffset, double minimum, double maximum)
            {
                if ((byteOffset & 3) != 0 || byteOffset < 0 || byteOffset / sizeof(uint) >= dwords.Count)
                {
                    throw new ArgumentOutOfRangeException(nameof(byteOffset));
                }

                uint raw = dwords[byteOffset / sizeof(uint)];
                float decoded = BitConverter.UInt32BitsToSingle(raw);
                double? value = float.IsFinite(decoded) && decoded >= minimum && decoded <= maximum
                    ? decoded
                    : null;
                return new TableValue(raw, value);
            }
        }

        private readonly record struct RatioValue(double? Value, string Raw);

        private readonly record struct TableValue(uint Raw, double? Value);
    }
}
