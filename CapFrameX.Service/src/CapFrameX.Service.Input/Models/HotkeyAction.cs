namespace CapFrameX.Service.Input.Models;

/// <summary>
/// Defines available hotkey actions in CapFrameX.
/// </summary>
public enum HotkeyAction
{
    /// <summary>
    /// Start or stop frame capture.
    /// </summary>
    Capture,

    /// <summary>
    /// Toggle overlay visibility.
    /// </summary>
    ToggleOverlay,

    /// <summary>
    /// Open overlay configuration.
    /// </summary>
    ConfigureOverlay,

    /// <summary>
    /// Reset capture history.
    /// </summary>
    ResetHistory,

    /// <summary>
    /// Toggle thread affinity.
    /// </summary>
    ToggleThreadAffinity,

    /// <summary>
    /// Reset metrics.
    /// </summary>
    ResetMetrics
}
