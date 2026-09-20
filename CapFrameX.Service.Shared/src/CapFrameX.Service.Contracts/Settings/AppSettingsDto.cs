namespace CapFrameX.Service.Contracts.Settings;

/// <summary>
/// The options every analysis is computed with.
/// </summary>
/// <remarks>
/// The defaults are the ones CapFrameX 1.x ships, so a record analysed in either application gives
/// the same numbers until the user says otherwise.
/// </remarks>
/// <param name="StutteringFactor">How far above its local average a frame counts as stutter.</param>
/// <param name="StutteringThreshold">Frame rate below which a frame counts as low FPS.</param>
/// <param name="MovingAverageWindowSize">Samples in the moving average.</param>
/// <param name="IntervalAverageWindowTime">Milliseconds in the time-based average window.</param>
/// <param name="FpsValuesRoundingDigits">Decimal places every frame-rate metric is rounded to.</param>
/// <param name="OutlierMethod">How outliers are removed unless a request says otherwise.</param>
/// <param name="Metrics">The tiles the analysis view opens with, in order.</param>
/// <param name="LShapeMetric">Whether the L-shape is drawn over frame times or frame rate.</param>
public sealed record AnalysisSettingsDto(
    double StutteringFactor,
    double StutteringThreshold,
    int MovingAverageWindowSize,
    int IntervalAverageWindowTime,
    int FpsValuesRoundingDigits,
    string OutlierMethod,
    IReadOnlyList<string> Metrics,
    string LShapeMetric);

/// <summary>Where the service looks for captures.</summary>
/// <param name="CaptureDirectory">The folder it watches, including its subfolders.</param>
public sealed record PathSettingsDto(string CaptureDirectory);

/// <summary>How the frontend presents itself.</summary>
/// <remarks>
/// Kept by the service rather than in the browser so the choice follows the user between the full
/// window, the capture window and the overlay editor, which are separate pages.
/// </remarks>
/// <param name="Theme">One of <see cref="AppearanceThemes"/>.</param>
public sealed record AppearanceSettingsDto(string Theme);

/// <summary>The themes the frontend offers.</summary>
public static class AppearanceThemes
{
    /// <summary>Follow the operating system.</summary>
    public const string System = "system";

    /// <summary>Always light.</summary>
    public const string Light = "light";

    /// <summary>Always dark.</summary>
    public const string Dark = "dark";

    /// <summary>All of them.</summary>
    public static IReadOnlyList<string> All { get; } = [System, Light, Dark];
}

/// <summary>What the service remembers about importing.</summary>
/// <param name="Offered">
/// Whether the user has been offered the first import. Asked once, whichever way they answered.
/// </param>
public sealed record ImportSettingsDto(bool Offered);

/// <summary>Everything the user can set.</summary>
/// <param name="Analysis">The options every analysis is computed with.</param>
/// <param name="Paths">Where the service looks for captures.</param>
/// <param name="Appearance">How the frontend presents itself.</param>
/// <param name="Import">What the service remembers about importing.</param>
public sealed record AppSettingsDto(
    AnalysisSettingsDto Analysis,
    PathSettingsDto Paths,
    AppearanceSettingsDto Appearance,
    ImportSettingsDto Import);
