namespace CapFrameX.Core.Configuration;

/// <summary>
/// Application settings model persisted to JSON
/// </summary>
public class AppSettings
{
    /// <summary>
    /// Capture hotkey (e.g., "F12", "Control+F12", "Shift+Alt+F11")
    /// </summary>
    public string CaptureHotkey { get; set; } = "F12";

    /// <summary>
    /// Auto-stop capture duration in seconds. 0 = disabled (manual stop only)
    /// </summary>
    public int CaptureDurationSeconds { get; set; } = 0;

    /// <summary>
    /// Whether auto-stop is enabled
    /// </summary>
    public bool AutoStopEnabled { get; set; } = false;
}
