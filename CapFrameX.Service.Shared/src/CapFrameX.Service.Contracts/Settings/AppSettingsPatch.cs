namespace CapFrameX.Service.Contracts.Settings;

/// <summary>Changes to the analysis options; a field left out is left alone.</summary>
/// <param name="StutteringFactor">How far above its local average a frame counts as stutter.</param>
/// <param name="StutteringThreshold">Frame rate below which a frame counts as low FPS.</param>
/// <param name="MovingAverageWindowSize">Samples in the moving average.</param>
/// <param name="IntervalAverageWindowTime">Milliseconds in the time-based average window.</param>
/// <param name="FpsValuesRoundingDigits">Decimal places every frame-rate metric is rounded to.</param>
/// <param name="OutlierMethod">How outliers are removed unless a request says otherwise.</param>
/// <param name="Metrics">The tiles the analysis view opens with, in order.</param>
/// <param name="LShapeMetric">Whether the L-shape is drawn over frame times or frame rate.</param>
public sealed record AnalysisSettingsPatch(
    double? StutteringFactor = null,
    double? StutteringThreshold = null,
    int? MovingAverageWindowSize = null,
    int? IntervalAverageWindowTime = null,
    int? FpsValuesRoundingDigits = null,
    string? OutlierMethod = null,
    IReadOnlyList<string>? Metrics = null,
    string? LShapeMetric = null);

/// <summary>Changes to where the service looks for captures.</summary>
/// <param name="CaptureDirectory">The folder it watches.</param>
public sealed record PathSettingsPatch(string? CaptureDirectory = null);

/// <summary>Changes to how the frontend presents itself.</summary>
/// <param name="Theme">One of <see cref="AppearanceThemes"/>.</param>
public sealed record AppearanceSettingsPatch(string? Theme = null);

/// <summary>Changes to what the service remembers about importing.</summary>
/// <param name="Offered">Whether the user has been offered the first import.</param>
public sealed record ImportSettingsPatch(bool? Offered = null);

/// <summary>
/// Changes to the settings.
/// </summary>
/// <remarks>
/// A section left out is left alone, and so is a field inside one. The whole patch is checked
/// before any of it is applied, so a request with one bad value changes nothing rather than half
/// of what it asked for.
/// </remarks>
/// <param name="Analysis">Changes to the analysis options.</param>
/// <param name="Paths">Changes to where the service looks for captures.</param>
/// <param name="Appearance">Changes to how the frontend presents itself.</param>
/// <param name="Import">Changes to what the service remembers about importing.</param>
public sealed record AppSettingsPatch(
    AnalysisSettingsPatch? Analysis = null,
    PathSettingsPatch? Paths = null,
    AppearanceSettingsPatch? Appearance = null,
    ImportSettingsPatch? Import = null)
{
    /// <summary>Whether the patch asks for anything at all.</summary>
    public bool IsEmpty => Analysis is null && Paths is null && Appearance is null && Import is null;
}
