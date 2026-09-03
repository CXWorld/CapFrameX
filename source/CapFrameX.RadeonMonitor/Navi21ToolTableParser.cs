using System.Globalization;

namespace CapFrameX.RadeonMonitor
{
    internal static class Navi21ToolTableParser
    {
        public const uint SupportedVersion = 0x003A0010;

        private const int MinimumTableSize = 0x3A0 + sizeof(uint);

        public static RadeonToolTableTelemetry Parse(RadeonToolTableSnapshot snapshot)
        {
            ArgumentNullException.ThrowIfNull(snapshot);

            if (snapshot.Version != SupportedVersion)
            {
                throw new NotSupportedException(
                    $"Navi21 SMU tool-table version 0x{snapshot.Version:X8} has no verified sensor map. " +
                    $"Version 0x{SupportedVersion:X8} is currently supported.");
            }

            if (snapshot.Dwords.Length * sizeof(uint) < MinimumTableSize)
            {
                throw new ArgumentException(
                    $"The Navi21 SMU tool table contains only {snapshot.Dwords.Length * sizeof(uint)} bytes; " +
                    $"at least {MinimumTableSize} are required for version 0x{SupportedVersion:X8}.",
                    nameof(snapshot));
            }

            TableReader reader = new(snapshot.Dwords);
            List<MetricReading> readings = new();

            Add(readings, reader, "Temperature", "GPU Temperature", 0x01C, "\u00B0C", 1, 0.1, 200.0);
            Add(readings, reader, "Temperature", "GPU Hot Spot Temperature", 0x020, "\u00B0C", 1, 0.1, 200.0);
            Add(readings, reader, "Temperature", "GPU Memory Junction Temperature", 0x024, "\u00B0C", 1, 0.1, 200.0);
            Add(readings, reader, "Temperature", "GPU VR VDDC Temperature", 0x068, "\u00B0C", 1, 0.1, 200.0);
            Add(readings, reader, "Temperature", "GPU VR SoC Temperature", 0x06C, "\u00B0C", 1, 0.1, 200.0);
            Add(readings, reader, "Temperature", "GPU VR VDDIO Temperature", 0x070, "\u00B0C", 1, 0.1, 200.0);
            Add(readings, reader, "Temperature", "GPU VR VDDCI Temperature", 0x074, "\u00B0C", 1, 0.1, 200.0);

            Add(readings, reader, "Voltage", "GPU Core Voltage (VDDCR_GFX)", 0x0C0, "V", 3, 0.1, 2.0);
            Add(readings, reader, "Voltage", "GPU Memory Voltage (VDDIO)", 0x100, "V", 3, 0.1, 2.0);
            Add(readings, reader, "Voltage", "GPU SoC Voltage (VDDCR_SOC)", 0x0E0, "V", 3, 0.1, 2.0);
            Add(readings, reader, "Voltage", "GPU Memory Voltage (VDDCI_MEM)", 0x120, "V", 3, 0.1, 2.0);

            Add(readings, reader, "Current", "GPU Core Current (VDDCR_GFX)", 0x0CC, "A", 3, 0.0, 1000.0);
            Add(readings, reader, "Current", "GPU Memory Current (VDDIO)", 0x10C, "A", 3, 0.0, 1000.0);
            Add(readings, reader, "Current", "GPU SoC Current (VDDCR_SOC)", 0x0EC, "A", 3, 0.0, 1000.0);
            Add(readings, reader, "Current", "GPU Memory Current (VDDCI_MEM)", 0x12C, "A", 3, 0.0, 1000.0);

            Add(readings, reader, "Power", "GPU Core Power (VDDCR_GFX)", 0x0D8, "W", 3, 0.0, 1000.0);
            Add(readings, reader, "Power", "GPU Memory Power (VDDIO)", 0x118, "W", 3, 0.0, 1000.0);
            Add(readings, reader, "Power", "GPU SoC Power (VDDCR_SOC)", 0x0F8, "W", 3, 0.0, 1000.0);
            Add(readings, reader, "Power", "GPU Memory Power (VDDCI_MEM)", 0x138, "W", 3, 0.0, 1000.0);
            Add(readings, reader, "Power", "Total Graphics Power (TGP)", 0x008, "W", 3, 0.0, 1000.0);
            Add(readings, reader, "Power", "GPU PPT", 0x2C8, "W", 3, 0.0, 1000.0);

            Add(readings, reader, "Clocks", "GPU Clock", 0x29C, "MHz", 1, 0.0, 5000.0);
            Add(readings, reader, "Clocks", "GPU Clock (Effective)", 0x2A0, "MHz", 1, 0.0, 5000.0);
            Add(readings, reader, "Clocks", "GPU Memory Clock", 0x280, "MHz", 1, 0.0, 5000.0);
            Add(readings, reader, "Fan", "GPU Fan", 0x2BC, "RPM", 0, 0.0, 10000.0);
            Add(readings, reader, "Activity", "GPU Utilization", 0x39C, "%", 1, 0.0, 100.0);

            Add(readings, reader, "Limits", "GPU PPT Limit", 0x2C4, "W", 3, 0.1, 1000.0);
            Add(readings, reader, "Limits", "GPU Core TDC Limit", 0x2F0, "A", 3, 0.1, 2000.0);
            Add(readings, reader, "Limits", "GPU SoC TDC Limit", 0x2E8, "A", 3, 0.1, 2000.0);

            AddRatio(readings, reader, "Limit utilization", "GPU PPT Limit", 0x2C8, 0x2C4);
            AddRatio(readings, reader, "Limit utilization", "GPU Core TDC Limit", 0x2F4, 0x2F0);
            AddRatio(readings, reader, "Limit utilization", "GPU SoC TDC Limit", 0x2EC, 0x2E8);
            AddRatio(readings, reader, "Thermal limits", "GPU Edge Thermal Limit", 0x2FC, 0x2F8);
            AddRatio(readings, reader, "Thermal limits", "GPU Hotspot Thermal Limit", 0x304, 0x300);
            AddRatio(readings, reader, "Thermal limits", "GPU Memory Thermal Limit", 0x30C, 0x308);
            AddRatio(readings, reader, "Thermal limits", "GPU VR GFX Thermal Limit", 0x314, 0x310);
            AddRatio(readings, reader, "Thermal limits", "GPU VR SOC Thermal Limit", 0x31C, 0x318);
            AddRatio(readings, reader, "Thermal limits", "GPU VR MEM Thermal Limit", 0x324, 0x320);

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
            TableValue value = reader.Read(valueOffset, 0.0, 10000.0);
            TableValue limit = reader.Read(limitOffset, 0.1, 10000.0);
            double? percentage = value.Value is double numerator && limit.Value is double denominator
                ? numerator * 100.0 / denominator
                : null;
            if (percentage is < 0.0 or > 1000.0 || !double.IsFinite(percentage ?? 0.0))
            {
                percentage = null;
            }

            readings.Add(CreateReading(
                group,
                name,
                percentage,
                "%",
                1,
                $"+0x{valueOffset:X3}/+0x{limitOffset:X3}"));
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

        private readonly record struct TableValue(uint Raw, double? Value);
    }

}
