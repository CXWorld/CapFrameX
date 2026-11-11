using System;
using System.IO;
using Newtonsoft.Json.Linq;

namespace CapFrameX.OSD.Integration
{
    /// <summary>What hands a design snapshot to the hook-free renderer.</summary>
    internal enum HookFreeDesignTrigger
    {
        /// <summary>The design publisher delivered a snapshot: telemetry, its heartbeat, a design change.</summary>
        Snapshot,

        /// <summary>The OSD refresh, or the overlay being shown again.</summary>
        Refresh
    }

    /// <summary>What the hook-free renderer takes over from a design snapshot.</summary>
    internal enum HookFreeDesignUpdate
    {
        /// <summary>Nothing: the renderer is up to date, or the values wait for the OSD refresh.</summary>
        None,

        /// <summary>The design was removed: the renderer returns to the classic rows.</summary>
        Clear,

        /// <summary>A new or changed design, with its values.</summary>
        Template,

        /// <summary>New values for the loaded design.</summary>
        Metrics
    }

    /// <summary>
    /// When the hook-free renderer takes over what a design snapshot carries. A design that is new,
    /// changed or removed changes the scene and is applied at once. Values alone wait for the OSD
    /// refresh: the publisher delivers a snapshot with every sensor poll (twice per second while a
    /// capture logs sensors) and with its own 500 ms heartbeat, and each one that reached the
    /// renderer made it repaint and present the root surface of its window, which the game below
    /// pays for with a late frame. With one update per <c>OSDRefreshPeriod</c> the tiles change as
    /// often as the classic rows always did. The publisher itself keeps its cadence: it also feeds
    /// the in-game renderers and the heatmap averages.
    /// </summary>
    internal static class HookFreeDesignFeed
    {
        /// <param name="trigger">What hands the snapshot over.</param>
        /// <param name="hasDesign">The snapshot carries a design.</param>
        /// <param name="designLoaded">The renderer shows a design.</param>
        /// <param name="designChanged">The snapshot's design is not the one the renderer shows.</param>
        /// <param name="metricsChanged">The snapshot's values are not the ones the renderer has.</param>
        /// <param name="everySnapshot">
        /// The former cadence, for A/B measurements (<see cref="AppliesEverySnapshot"/>): values
        /// reach the renderer with every snapshot.
        /// </param>
        internal static HookFreeDesignUpdate Decide(HookFreeDesignTrigger trigger, bool hasDesign,
            bool designLoaded, bool designChanged, bool metricsChanged, bool everySnapshot = false)
        {
            if (!hasDesign) return designLoaded ? HookFreeDesignUpdate.Clear : HookFreeDesignUpdate.None;
            if (!designLoaded || designChanged) return HookFreeDesignUpdate.Template;
            if (!metricsChanged) return HookFreeDesignUpdate.None;
            return trigger == HookFreeDesignTrigger.Refresh || everySnapshot
                ? HookFreeDesignUpdate.Metrics
                : HookFreeDesignUpdate.None;
        }

        /// <summary>
        /// True while OsdDebug.json asks for the former behavior (<c>"hookFreeRepaint": "legacy"</c>,
        /// the native core's switch for the former repaint rule): the measurement baseline then also
        /// has the former feed cadence. Read when the renderer starts and whenever it is shown, as
        /// the native core reads it.
        /// </summary>
        internal static bool AppliesEverySnapshot(string debugConfigurationPath = null)
        {
            try
            {
                string path = debugConfigurationPath ?? Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "CapFrameX", "Configuration", "OsdDebug.json");
                if (!File.Exists(path)) return false;
                var mode = JObject.Parse(File.ReadAllText(path))["hookFreeRepaint"];
                return mode != null && mode.Type == JTokenType.String &&
                    string.Equals((string)mode, "legacy", StringComparison.Ordinal);
            }
            catch (Exception)
            {
                // A debug switch in a file somebody edits by hand: an unreadable one is not set.
                return false;
            }
        }
    }
}
