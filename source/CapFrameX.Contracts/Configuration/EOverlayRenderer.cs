namespace CapFrameX.Contracts.Configuration
{
    /// <summary>
    /// The overlay renderer chosen under OSD options. Exactly one is selected at a time.
    /// </summary>
    public enum EOverlayRenderer
    {
        /// <summary>Rivatuner Statistics Server draws the overlay.</summary>
        Rtss = 0,
        /// <summary>The in-game hook (or the Vulkan layer) draws inside the game's swapchain.</summary>
        InGame = 1,
        /// <summary>A DWM/DirectComposition window draws above the game; nothing is injected.</summary>
        HookFree = 2,
        /// <summary>
        /// No overlay at all: nothing is injected, no overlay window is created and RTSS is not fed.
        /// Captures, sensors and the remote API keep working.
        /// </summary>
        Off = 3
    }
}
