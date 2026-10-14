using System;
using Serilog;

namespace CapFrameX.OSD.Integration
{
    /// <summary>
    /// Withdraws CapFrameX's overlay renderers when the application exits. The order is
    /// load-bearing: the in-game hook and the Vulkan layer draw the classic rows as soon as the
    /// saved design disappears while their row feed is still valid, which is exactly the switch
    /// from the tile to the row overlay. Any other order shows the rows until the hook notices
    /// that CapFrameX has exited.
    /// </summary>
    public static class OverlayRendererShutdown
    {
        /// <summary>
        /// Withdraws the row feed, then the design, then the remaining renderer components in
        /// the given order. Call it before any other shutdown step: stopping PresentMon or the
        /// sensors can withdraw the design on its own. A failing component does not stop the rest.
        /// </summary>
        /// <param name="rowFeed">Feeds the classic rows to the in-game hook and the Vulkan layer.</param>
        /// <param name="designFeed">Publishes the saved tile design to the same renderers.</param>
        /// <param name="remaining">Further renderer components; null entries are skipped.</param>
        public static void Run(IDisposable rowFeed, IDisposable designFeed, params IDisposable[] remaining)
        {
            Dispose(rowFeed);
            Dispose(designFeed);
            foreach (var component in remaining ?? Array.Empty<IDisposable>())
                Dispose(component);
        }

        private static void Dispose(IDisposable component)
        {
            try
            {
                component?.Dispose();
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Overlay: failed to shut down {Component}", component.GetType().Name);
            }
        }
    }
}
