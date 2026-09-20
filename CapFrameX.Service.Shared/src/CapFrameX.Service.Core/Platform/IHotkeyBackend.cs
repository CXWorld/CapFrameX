namespace CapFrameX.Service.Core.Platform;

/// <summary>
/// Platform port for global hotkeys. Availability is part of the contract because Wayland grants
/// global shortcuts only through a portal, and some sessions offer none at all.
/// </summary>
public interface IHotkeyBackend : IAsyncDisposable
{
    /// <summary>Whether global hotkeys work in this session, with a reason when they do not.</summary>
    PlatformAvailability Availability { get; }

    /// <summary>
    /// Registers a hotkey. The returned registration unregisters it when disposed.
    /// </summary>
    /// <param name="gesture">Platform-neutral gesture description, for example <c>Ctrl+F11</c>.</param>
    /// <param name="onPressed">Invoked when the gesture fires; never on the caller's thread.</param>
    /// <param name="cancellationToken">Cancels the registration attempt.</param>
    ValueTask<IAsyncDisposable> RegisterAsync(
        string gesture,
        Action onPressed,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Display name for a gesture, because key names and available modifiers differ per platform.
    /// </summary>
    /// <param name="gesture">Gesture to describe.</param>
    string Describe(string gesture);
}
