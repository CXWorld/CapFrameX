using CapFrameX.Contracts.Overlay;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace CapFrameX.Overlay
{
    /// <summary>
    /// Shared active-display detection for the hook-free display selector and display-resolution
    /// overlay entries. Keeping both consumers on <see cref="Screen.AllScreens"/> prevents them
    /// from assigning different names or bounds to the same display topology.
    /// </summary>
    public static class DisplayDetection
    {
        public static IReadOnlyList<DetectedDisplay> GetDisplays()
        {
            return Screen.AllScreens
                .Select(screen => new DetectedDisplay(
                    screen.DeviceName,
                    screen.Bounds.Width,
                    screen.Bounds.Height,
                    screen.Primary))
                .ToArray();
        }

        public static string GetShortName(string deviceName)
        {
            if (string.IsNullOrWhiteSpace(deviceName))
            {
                return "Display";
            }

            return deviceName.StartsWith(@"\\.\", StringComparison.OrdinalIgnoreCase)
                ? deviceName.Substring(4)
                : deviceName;
        }

        public static string GetLabel(string deviceName)
        {
            string shortName = GetShortName(deviceName);
            const string displayPrefix = "DISPLAY";
            if (shortName.StartsWith(displayPrefix, StringComparison.OrdinalIgnoreCase)
                && int.TryParse(shortName.Substring(displayPrefix.Length), out int displayNumber))
            {
                return $"Display {displayNumber}";
            }

            return shortName;
        }
    }
}
