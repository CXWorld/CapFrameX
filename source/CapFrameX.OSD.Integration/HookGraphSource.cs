using CapFrameX.Contracts.Configuration;

namespace CapFrameX.OSD.Integration
{
    /// <summary>
    /// Where the in-game renderers (DXGI hook, Vulkan layer) take the frames of their charts from:
    /// their own presents, or PresentMon's rows through <see cref="HookFrametimeChannel"/>.
    /// Only PresentMon knows display times, so an active design with a display-time chart (a
    /// series, a line or a stutter threshold) takes PresentMon whatever
    /// <see cref="IAppConfiguration.HookOverlayUsePresentMonFrametimes"/> says; with the hook's
    /// own presents such a chart stayed empty. The frame time charts of that design then replay
    /// PresentMon's frames too, as with the option on.
    /// </summary>
    public static class HookGraphSource
    {
        public static bool UsesPresentMon(IAppConfiguration configuration, OverlayRuntimeDesign activeDesign)
            => configuration.HookOverlayUsePresentMonFrametimes || activeDesign?.NeedsDisplayTimes == true;
    }
}
