namespace CapFrameX.Service.Contracts.Analysis;

/// <summary>One point of a curve.</summary>
/// <param name="X">Horizontal value; what it means depends on the curve.</param>
/// <param name="Y">Vertical value.</param>
public readonly record struct PointDto(double X, double Y);

/// <summary>One number the analysis view shows on a tile.</summary>
/// <param name="Key">Stable identifier, which is what a saved tile layout refers to.</param>
/// <param name="Label">Short label as CapFrameX has always written it.</param>
/// <param name="Value">The value, or <c>null</c> where the capture does not allow it.</param>
/// <param name="Unit">Unit of the value.</param>
public sealed record MetricDto(string Key, string Label, double? Value, string Unit);

/// <summary>Where a single frame took far longer than its neighbours.</summary>
/// <param name="TimeSeconds">When it happened, measured from the start of the capture.</param>
/// <param name="Milliseconds">How long that frame took.</param>
public sealed record SpikeDto(double TimeSeconds, double Milliseconds);

/// <summary>
/// How evenly the frames arrived, as a split of the captured time.
/// </summary>
/// <param name="SmoothPercent">Time that was neither stuttering nor below the low-FPS threshold.</param>
/// <param name="StutterPercent">Time in frames far above their local average.</param>
/// <param name="LowFpsPercent">Time in frames that were slow without being stutter.</param>
/// <param name="SpikeCount">How many frames count as stutter.</param>
/// <param name="WorstSpike">The longest of them, or <c>null</c> when there is none.</param>
public sealed record FramePacingDto(
    double SmoothPercent,
    double StutterPercent,
    double LowFpsPercent,
    int SpikeCount,
    SpikeDto? WorstSpike);

/// <summary>Input-to-display latency, where the capture recorded it.</summary>
/// <param name="AverageMs">Mean latency.</param>
/// <param name="P99Ms">99th percentile.</param>
public sealed record PcLatencyDto(double AverageMs, double P99Ms);

/// <summary>The stretch of the capture an analysis covers.</summary>
/// <param name="StartSeconds">Where it begins.</param>
/// <param name="EndSeconds">Where it ends.</param>
/// <param name="FrameCount">Frames inside it, after outlier removal.</param>
/// <param name="Run">Index of the single run analysed, or <c>null</c> for all of them.</param>
public sealed record AnalysisWindowDto(double StartSeconds, double EndSeconds, int FrameCount, int? Run);

/// <summary>What the analysis was told to assume.</summary>
/// <param name="StutteringFactor">How far above its local average a frame counts as stutter.</param>
/// <param name="StutteringThreshold">Frame rate below which a frame counts as low FPS.</param>
/// <param name="OutlierMethod">How outliers were removed, if at all.</param>
public sealed record AnalysisThresholdsDto(
    double StutteringFactor,
    double StutteringThreshold,
    string OutlierMethod);

/// <summary>One capture analysed.</summary>
/// <param name="Metrics">The numbers the tiles show, in the order they were asked for.</param>
/// <param name="FramePacing">How evenly the frames arrived.</param>
/// <param name="PcLatency">Latency, or <c>null</c> where the capture does not carry it.</param>
/// <param name="LShape">Frame rate against percentile, the curve CapFrameX calls the L-shape.</param>
/// <param name="Distribution">How the frame times are distributed.</param>
/// <param name="Window">What stretch of the capture this covers.</param>
/// <param name="Thresholds">What it was told to assume.</param>
public sealed record AnalysisDto(
    IReadOnlyList<MetricDto> Metrics,
    FramePacingDto FramePacing,
    PcLatencyDto? PcLatency,
    IReadOnlyList<PointDto> LShape,
    IReadOnlyList<PointDto> Distribution,
    AnalysisWindowDto Window,
    AnalysisThresholdsDto Thresholds);
