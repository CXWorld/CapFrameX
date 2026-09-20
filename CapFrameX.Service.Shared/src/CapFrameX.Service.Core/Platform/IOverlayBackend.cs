namespace CapFrameX.Service.Core.Platform;

/// <summary>
/// Platform port for the in-game overlay. Both platforms render the same scene description through
/// CapFrameX.OSD; what differs is the delivery path - hook-free window, DXGI hook and Vulkan layer
/// on Windows, the Vulkan layer on Linux.
/// </summary>
public interface IOverlayBackend : IAsyncDisposable
{
    /// <summary>Whether an overlay can be shown at all, with a reason when it cannot.</summary>
    PlatformAvailability Availability { get; }

    /// <summary>Applies an overlay scene description; replaces whatever was applied before.</summary>
    /// <param name="templateJson">Scene description in the CapFrameX.OSD template format.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    ValueTask ApplyAsync(string templateJson, CancellationToken cancellationToken = default);

    /// <summary>Shows or hides the overlay without discarding the applied scene.</summary>
    /// <param name="visible">Whether the overlay should be visible.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    ValueTask SetVisibleAsync(bool visible, CancellationToken cancellationToken = default);

    /// <summary>
    /// Renders one frame of a scene off screen, for the editor preview. Returns PNG bytes, or
    /// <c>null</c> when the platform cannot render a preview yet.
    /// </summary>
    /// <param name="templateJson">Scene description to render.</param>
    /// <param name="width">Preview width in pixels.</param>
    /// <param name="height">Preview height in pixels.</param>
    /// <param name="cancellationToken">Cancels the render.</param>
    ValueTask<byte[]?> RenderPreviewAsync(
        string templateJson,
        int width,
        int height,
        CancellationToken cancellationToken = default);
}
