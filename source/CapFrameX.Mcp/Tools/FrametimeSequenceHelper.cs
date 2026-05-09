using CapFrameX.Data.Session.Contracts;
using CapFrameX.Statistics.NetStandard.Contracts;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace CapFrameX.Mcp.Tools
{
    /// <summary>
    /// Centralizes the choice of frametime source (MsBetweenPresents vs MsBetweenDisplayChange)
    /// so MCP tool outputs match the Analysis tab when AppSettings.UseDisplayChangeMetrics is on.
    /// </summary>
    internal static class FrametimeSequenceHelper
    {
        // When Present-AdaptiveStd / Display-AdaptiveStd reaches this ratio, the run almost
        // certainly involves Frame Generation or extreme frametime jitter — Display-times
        // are then the correct view of the actual gaming experience.
        private const double DisplayRecommendedRatio = 2.0;
        /// <summary>
        /// Returns a clean FPS-ready frametime sequence. With <paramref name="useDisplay"/> = true,
        /// uses MsBetweenDisplayChange and drops 0 / NaN entries (dropped frames). Falls back to
        /// MsBetweenPresents when display data is unavailable (e.g. some Vulkan/OpenGL paths).
        /// </summary>
        public static List<double> ResolveSequence(ISessionRun run, bool useDisplay)
        {
            if (useDisplay)
            {
                var display = run?.CaptureData?.MsBetweenDisplayChange;
                if (display != null && display.Length > 0)
                {
                    var cleaned = new List<double>(display.Length);
                    for (int i = 0; i < display.Length; i++)
                    {
                        var v = display[i];
                        if (v > 0 && !double.IsNaN(v)) cleaned.Add(v);
                    }
                    if (cleaned.Count > 0) return cleaned;
                }
                // Fall through to present-times.
            }
            var presents = run?.CaptureData?.MsBetweenPresents;
            return presents == null ? null : presents.ToList();
        }

        /// <summary>
        /// Returns the per-frame array parallel to TimeInSeconds (no filtering).
        /// Caller must skip zero / NaN entries when iterating if <paramref name="useDisplay"/> is true.
        /// </summary>
        public static double[] ResolveRawSequence(ISessionRun run, bool useDisplay)
        {
            if (useDisplay)
            {
                var display = run?.CaptureData?.MsBetweenDisplayChange;
                if (display != null && display.Length > 0) return display;
            }
            return run?.CaptureData?.MsBetweenPresents;
        }

        /// <summary>Label used in result DTOs to make the source explicit.</summary>
        public static string SourceLabel(bool useDisplay) =>
            useDisplay ? "MsBetweenDisplayChange" : "MsBetweenPresents";

        /// <summary>
        /// Diagnoses whether the chosen FPS source is appropriate. Returns a human-readable
        /// warning string when the user is on present-times but the data shape (Frame Generation,
        /// extreme jitter) calls for display-times — or null when the choice is fine.
        /// Also warns if useDisplay was requested but display data is unavailable.
        /// </summary>
        public static string GetSourceWarning(ISessionRun run, IStatisticProvider stats, bool useDisplay)
        {
            if (run?.CaptureData == null) return null;

            // User explicitly requested display-times but data is unusable -> warn.
            if (useDisplay)
            {
                var dt = run.CaptureData.MsBetweenDisplayChange;
                bool anyDisplay = dt != null && dt.Length > 0 && dt.Any(v => v > 0 && !double.IsNaN(v));
                if (!anyDisplay)
                {
                    return "Display-change times requested but unavailable for this record (typical for Vulkan/OpenGL captures). " +
                           "The result was computed from MsBetweenPresents instead.";
                }
                return null;
            }

            // User is on present-times. Check whether display-times would be a better fit.
            var presentSeq = ResolveSequence(run, useDisplay: false);
            var displaySeq = ResolveSequence(run, useDisplay: true);
            if (presentSeq == null || presentSeq.Count < 30) return null;
            if (displaySeq == null || displaySeq.Count < 30) return null;

            // Use the same FPS-AdaptiveStd path that cfx_get_metrics reports — apples to apples.
            double presentStd, displayStd;
            try
            {
                presentStd = stats.GetFpsMetricValue(presentSeq, EMetric.AdaptiveStd);
                displayStd = stats.GetFpsMetricValue(displaySeq, EMetric.AdaptiveStd);
            }
            catch
            {
                return null;
            }

            if (double.IsNaN(presentStd) || double.IsNaN(displayStd) || displayStd <= 0.001)
                return null;

            double ratio = presentStd / displayStd;
            if (ratio < DisplayRecommendedRatio) return null;

            // Strong indicator of Frame Generation or extreme jitter — present-time metrics
            // overstate the perceived smoothness asymmetry.
            return string.Format(CultureInfo.InvariantCulture,
                "Present-time AdaptiveStd ({0:F1} fps) is {1:F1}× higher than Display-time AdaptiveStd ({2:F1} fps). " +
                "This suggests Frame Generation (DLSS 3 / FSR 3 / XeSS-FG) or extreme frametime jitter — " +
                "in those cases Display-change metrics better reflect the actual perceived smoothness. " +
                "Enable AppSettings.UseDisplayChangeMetrics in CapFrameX (or pass useDisplayChangeMetrics=true) " +
                "to see the gameplay-experience view.",
                presentStd, ratio, displayStd);
        }
    }
}
