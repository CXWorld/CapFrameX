namespace CapFrameX.Service.Application.Settings;

/// <summary>
/// The settings as they sit in the file.
/// </summary>
/// <remarks>
/// Flat and mutable with a default on every property, so a file written by an older or newer
/// service still loads: a field that is missing keeps its default rather than making the whole file
/// unreadable and throwing the user's configuration away. It is deliberately not the wire shape -
/// the API groups the settings for the UI, and the two are free to change apart.
/// </remarks>
public sealed class PersistedSettings
{
    /// <summary>How far above its local average a frame counts as stutter.</summary>
    public double StutteringFactor { get; set; } = 2.5;

    /// <summary>Frame rate below which a frame counts as low FPS.</summary>
    public double StutteringThreshold { get; set; } = 25d;

    /// <summary>Samples in the moving average.</summary>
    public int MovingAverageWindowSize { get; set; } = 100;

    /// <summary>Milliseconds in the time-based average window.</summary>
    public int IntervalAverageWindowTime { get; set; } = 500;

    /// <summary>Decimal places every frame-rate metric is rounded to.</summary>
    public int FpsValuesRoundingDigits { get; set; } = 1;

    /// <summary>How outliers are removed unless a request says otherwise.</summary>
    public string OutlierMethod { get; set; } = "None";

    /// <summary>The tiles the analysis view opens with, in order.</summary>
    public List<string> Metrics { get; set; } = [];

    /// <summary>Whether the L-shape is drawn over frame times or frame rate.</summary>
    public string LShapeMetric { get; set; } = "Frametimes";

    /// <summary>
    /// The folder the service watches, or <c>null</c> to follow the platform's own.
    /// </summary>
    /// <remarks>
    /// Null rather than the resolved path, so a portable installation moved to another drive - or
    /// a profile on another machine - does not carry a path that no longer exists.
    /// </remarks>
    public string? CaptureDirectory { get; set; }

    /// <summary>How the frontend presents itself.</summary>
    public string Theme { get; set; } = "system";

    /// <summary>
    /// Whether the user has been offered the first import.
    /// </summary>
    /// <remarks>
    /// Asked once. Somebody who said no to importing their CapFrameX 1.x captures should not be
    /// asked again every time they open the application.
    /// </remarks>
    public bool ImportOffered { get; set; }
}
