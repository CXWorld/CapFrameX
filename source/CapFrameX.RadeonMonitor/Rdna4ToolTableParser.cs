using System.Globalization;

namespace CapFrameX.RadeonMonitor
{
    internal static class Rdna4ToolTableParser
    {
        public static RadeonToolTableTelemetry Parse(RadeonToolTableSnapshot snapshot)
        {
            ArgumentNullException.ThrowIfNull(snapshot);

            ClockOffsets offsets = snapshot.Version switch
            {
                0x00660001 or 0x00660002 or 0x00660003 => new(0x294, 0x238),
                0x00660004 => new(0x290, 0x234),
                0x00660005 or 0x00660006 => new(0x1F8, 0x1CC),
                _ => throw new NotSupportedException(
                    $"RDNA4 SMU tool-table version 0x{snapshot.Version:X8} has no verified sensor map.")
            };

            int requiredSize = Math.Max(offsets.GfxEffective, offsets.FclkEffective) + sizeof(uint);
            if (snapshot.Dwords.Length * sizeof(uint) < requiredSize)
            {
                throw new ArgumentException(
                    $"The RDNA4 SMU tool table contains only {snapshot.Dwords.Length * sizeof(uint)} bytes; " +
                    $"at least {requiredSize} are required for version 0x{snapshot.Version:X8}.",
                    nameof(snapshot));
            }

            List<MetricReading> readings = new(2)
            {
                ReadClock(snapshot.Dwords, "GPU Clock (Effective)", offsets.GfxEffective),
                ReadClock(snapshot.Dwords, "GPU FCLK (Effective)", offsets.FclkEffective)
            };

            return new RadeonToolTableTelemetry(
                readings,
                readings.Count(reading => reading.NumericValue is null));
        }

        private static MetricReading ReadClock(
            IReadOnlyList<uint> dwords,
            string name,
            int byteOffset)
        {
            uint raw = dwords[byteOffset / sizeof(uint)];
            float decoded = BitConverter.UInt32BitsToSingle(raw);
            double? value = float.IsFinite(decoded) && decoded >= 0.0f && decoded <= 5000.0f
                ? decoded
                : null;

            return new MetricReading(
                "Clocks",
                name,
                value?.ToString("F1", CultureInfo.InvariantCulture) ?? "\u2014",
                "MHz",
                $"+0x{byteOffset:X3}=0x{raw:X8}",
                value,
                DecimalPlaces: 1);
        }

        private readonly record struct ClockOffsets(int GfxEffective, int FclkEffective);
    }
}
