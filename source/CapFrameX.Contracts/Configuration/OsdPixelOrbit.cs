using System;

namespace CapFrameX.Contracts.Configuration
{
    /// <summary>
    /// OLED burn-in protection for the CapFrameX renderers: the overlay moves by one pixel per
    /// interval along a serpentine walk over a (radius + 1)² square, so static text never stays
    /// on the same subpixels. Offsets point away from the anchored edge and are never negative,
    /// which keeps the configured margin as the minimum distance to the screen edge.
    /// </summary>
    public static class OsdPixelOrbit
    {
        public const int DefaultIntervalSeconds = 180;
        public const int MinimumIntervalSeconds = 10;
        public const int MaximumIntervalSeconds = 3600;
        public const int DefaultRadius = 6;
        public const int MinimumRadius = 1;
        public const int MaximumRadius = 32;

        public static int ClampInterval(int seconds)
            => Math.Max(MinimumIntervalSeconds, Math.Min(MaximumIntervalSeconds, seconds));

        public static int ClampRadius(int pixels)
            => Math.Max(MinimumRadius, Math.Min(MaximumRadius, pixels));

        /// <summary>
        /// Offset for a monotonic time in milliseconds. Every caller that uses the same clock
        /// gets the same position, so all renderers agree without shared state.
        /// </summary>
        public static (int X, int Y) GetOffset(int intervalSeconds, int radius, long timeMilliseconds)
        {
            int side = ClampRadius(radius) + 1;
            long step = Math.Max(0, timeMilliseconds) / (ClampInterval(intervalSeconds) * 1000L);
            int index = (int)(step % (side * side));
            int row = index / side;
            int column = index % side;
            // Reverse every other row: consecutive positions are always one pixel apart.
            if (row % 2 == 1)
                column = side - 1 - column;
            return (column, row);
        }
    }
}
