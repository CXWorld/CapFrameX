using CapFrameX.Contracts.Configuration;
using Moq;

namespace CapFrameX.Test.Mocks
{
    /// <summary>
    /// Gives a mocked <see cref="IAppConfiguration"/> the renderer semantics of
    /// <c>CapFrameXConfiguration</c>: the renderer and its two flag mirrors stay consistent,
    /// whichever of them a test or the code under test writes. Call it after
    /// <c>SetupAllProperties</c>, which replaces earlier property setups.
    /// </summary>
    internal static class OverlayRendererConfigurationMock
    {
        internal static Mock<IAppConfiguration> WireOverlayRenderer(this Mock<IAppConfiguration> configuration,
            EOverlayRenderer renderer)
        {
            var current = renderer;
            configuration.SetupGet(c => c.OverlayRenderer).Returns(() => current);
            configuration.SetupSet(c => c.OverlayRenderer = It.IsAny<EOverlayRenderer>())
                .Callback<EOverlayRenderer>(value => current = value);
            configuration.SetupGet(c => c.EnableHookOverlay)
                .Returns(() => OverlayRendererSelection.EnablesHookOverlay(current));
            configuration.SetupSet(c => c.EnableHookOverlay = It.IsAny<bool>())
                .Callback<bool>(value => current = OverlayRendererSelection.WithHookOverlay(current, value));
            configuration.SetupGet(c => c.EnableHookFreeOverlay)
                .Returns(() => OverlayRendererSelection.EnablesHookFreeOverlay(current));
            configuration.SetupSet(c => c.EnableHookFreeOverlay = It.IsAny<bool>())
                .Callback<bool>(value => current = OverlayRendererSelection.WithHookFreeOverlay(current, value));
            return configuration;
        }
    }
}
