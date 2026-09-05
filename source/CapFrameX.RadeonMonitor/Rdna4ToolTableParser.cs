using System.Globalization;

namespace CapFrameX.RadeonMonitor
{
    internal static class Rdna4ToolTableParser
    {
        public static RadeonToolTableTelemetry Parse(RadeonToolTableSnapshot snapshot)
        {
            ArgumentNullException.ThrowIfNull(snapshot);
            ArgumentNullException.ThrowIfNull(snapshot.Dwords);

            // HWiNFO 8.32-5840's RDNA4 tool-table getters; offsets depend on the full version.
            SensorOffsets offsets = snapshot.Version switch
            {
                0x00660001 or 0x00660002 or 0x00660003 => new(0x294, 0x238, 0x188),
                0x00660004 => new(0x290, 0x234, 0x184),
                0x00660005 or 0x00660006 => new(0x1F8, 0x1CC, 0x11C),
                _ => throw new NotSupportedException(
                    $"RDNA4 SMU tool-table version 0x{snapshot.Version:X8} has no verified sensor map.")
            };

            int requiredSize = Math.Max(
                Math.Max(offsets.GfxEffective, offsets.FclkEffective), offsets.GfxCurrent) + sizeof(uint);
            if (snapshot.Dwords.Length * sizeof(uint) < requiredSize)
            {
                throw new ArgumentException(
                    $"The RDNA4 SMU tool table contains only {snapshot.Dwords.Length * sizeof(uint)} bytes; " +
                    $"at least {requiredSize} are required for version 0x{snapshot.Version:X8}.",
                    nameof(snapshot));
            }

            return CreateTelemetry(snapshot.Dwords, offsets);
        }

        public static RadeonToolTableTelemetry CreateUnavailable()
        {
            return CreateTelemetry(null, default);
        }

        private static RadeonToolTableTelemetry CreateTelemetry(
            IReadOnlyList<uint>? dwords,
            SensorOffsets offsets)
        {
            List<MetricReading> readings = new(3)
            {
                ReadValue(dwords, "Clocks", "GPU Clock (Effective)", offsets.GfxEffective, "MHz", 1, 5000.0),
                ReadValue(dwords, "Clocks", "GPU FCLK (Effective)", offsets.FclkEffective, "MHz", 1, 5000.0),
                // Already amperes, unlike the public uint16 AvgCurrent field. Match the
                // other private parsers' 0..2000 A plausibility check, not a scale factor.
                ReadValue(dwords, "Current", "GPU Core Current (VDDCR_GFX)", offsets.GfxCurrent, "A", 3, 2000.0)
            };

            return new RadeonToolTableTelemetry(
                readings,
                readings.Count(reading => reading.NumericValue is null));
        }

        private static MetricReading ReadValue(
            IReadOnlyList<uint>? dwords,
            string group,
            string name,
            int byteOffset,
            string unit,
            int decimalPlaces,
            double maximum)
        {
            uint? raw = dwords?[byteOffset / sizeof(uint)];
            float decoded = raw is uint bits ? BitConverter.UInt32BitsToSingle(bits) : float.NaN;
            double? value = float.IsFinite(decoded) && decoded >= 0.0f && decoded <= maximum
                ? decoded
                : null;

            return new MetricReading(
                group,
                name,
                value?.ToString($"F{decimalPlaces}", CultureInfo.InvariantCulture) ?? "\u2014",
                unit,
                raw is uint rawValue ? $"+0x{byteOffset:X3}=0x{rawValue:X8}" : "unavailable",
                value,
                DecimalPlaces: decimalPlaces);
        }

        private readonly record struct SensorOffsets(int GfxEffective, int FclkEffective, int GfxCurrent);
    }
}
