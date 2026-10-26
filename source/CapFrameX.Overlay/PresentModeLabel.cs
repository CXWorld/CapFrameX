using System;
using System.Collections.Generic;

namespace CapFrameX.Overlay
{
    /// <summary>
    /// Short names of PresentMon's present modes for the overlay, where "Hardware: Legacy Copy to
    /// front buffer" takes more room than the rest of a row. Each keeps the part that tells whether
    /// DWM composes the frames: "HW" (the display scans the swapchain out), "MPO" (an overlay plane
    /// of the display) or "Composed". Capture files and logs keep PresentMon's names.
    /// </summary>
    public static class PresentModeLabel
    {
        private static readonly IReadOnlyDictionary<string, string> ShortNames =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["Hardware: Legacy Flip"] = "HW: Legacy Flip",
                ["Hardware: Legacy Copy to front buffer"] = "HW: Legacy Copy",
                ["Hardware: Independent Flip"] = "HW: Independent Flip",
                ["Hardware Composed: Independent Flip"] = "MPO: Independent Flip",
                ["Composed: Flip"] = "Composed: Flip",
                ["Composed: Copy with GPU GDI"] = "Composed: GPU Copy",
                ["Composed: Copy with CPU GDI"] = "Composed: CPU Copy",
                ["Composed: Composition Atlas"] = "Composed: Atlas",
            };

        /// <summary>
        /// The short name of a PresentMon present mode. Names it does not know ("Unknown", modes of a
        /// later PresentMon) and null pass through unchanged.
        /// </summary>
        public static string Shorten(string presentMode)
            => presentMode != null && ShortNames.TryGetValue(presentMode, out string shortName)
                ? shortName : presentMode;
    }
}
