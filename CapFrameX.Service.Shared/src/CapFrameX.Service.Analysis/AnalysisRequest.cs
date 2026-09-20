using CapFrameX.Statistics.NetStandard.Contracts;

namespace CapFrameX.Service.Analysis;

/// <summary>
/// What one analysis call asks for, as opposed to what the user configured once.
/// </summary>
public sealed record AnalysisRequest
{
    /// <summary>Which run to analyse, or <c>null</c> for the whole capture.</summary>
    public int? Run { get; init; }

    /// <summary>Where the window begins, in seconds; <c>null</c> means the start of the capture.</summary>
    public double? StartSeconds { get; init; }

    /// <summary>Where the window ends, in seconds; <c>null</c> means the end of the capture.</summary>
    public double? EndSeconds { get; init; }

    /// <summary>How outliers are removed before anything is computed.</summary>
    public ERemoveOutlierMethod OutlierMethod { get; init; } = ERemoveOutlierMethod.None;

    /// <summary>
    /// Which metrics to return, in the order the tiles show them; <c>null</c> takes the default set.
    /// </summary>
    public IReadOnlyList<EMetric>? Metrics { get; init; }

    /// <summary>
    /// Whether the L-shape curve is drawn over frame times or over frame rate.
    /// </summary>
    /// <remarks>
    /// The two are not the same curve read differently: each has its own set of quantiles, high
    /// ones for frame times and low ones for frame rate, because in both cases the interesting end
    /// is the slow one.
    /// </remarks>
    public ELShapeMetrics LShapeMetric { get; init; } = ELShapeMetrics.Frametimes;
}

/// <summary>What one series call asks for.</summary>
public sealed record SeriesRequest
{
    /// <summary>Which run to return, or <c>null</c> for the whole capture.</summary>
    public int? Run { get; init; }

    /// <summary>Where the window begins, in seconds; <c>null</c> means the start of the capture.</summary>
    public double? StartSeconds { get; init; }

    /// <summary>Where the window ends, in seconds; <c>null</c> means the end of the capture.</summary>
    public double? EndSeconds { get; init; }

    /// <summary>
    /// Which curves to return; <c>null</c> takes <see cref="Contracts.Analysis.SeriesKinds.Default"/>.
    /// </summary>
    /// <remarks>
    /// Named rather than always sent: the frame times alone are what the chart opens with, and
    /// every further column is another array the size of the capture.
    /// </remarks>
    public IReadOnlyList<string>? Kinds { get; init; }
}
