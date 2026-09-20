namespace CapFrameX.Service.Contracts.Analysis;

/// <summary>
/// The curves behind the chart, column by column.
/// </summary>
/// <remarks>
/// Columnar rather than an array of points: a capture is tens of thousands of frames, and one
/// object per frame costs several times the bytes and the parsing of one number per array slot.
/// A column the capture does not carry is absent rather than full of nulls.
/// </remarks>
/// <param name="Time">Seconds from the start of the capture, one per frame.</param>
/// <param name="Frametimes">Milliseconds between presents.</param>
/// <param name="Fps">Frames per second, the reciprocal of <paramref name="Frametimes"/>.</param>
/// <param name="DisplayChange">Milliseconds between display changes; null where a frame has none.</param>
/// <param name="PcLatency">Input-to-display latency in milliseconds; null where a frame has none.</param>
/// <param name="Window">What stretch of the capture this covers.</param>
public sealed record SeriesResponse(
    double[] Time,
    double[]? Frametimes,
    double[]? Fps,
    double?[]? DisplayChange,
    double?[]? PcLatency,
    AnalysisWindowDto Window);

/// <summary>The curves a caller can ask for.</summary>
public static class SeriesKinds
{
    /// <summary>Milliseconds between presents.</summary>
    public const string Frametimes = "frametimes";

    /// <summary>Frames per second.</summary>
    public const string Fps = "fps";

    /// <summary>Milliseconds between display changes.</summary>
    public const string DisplayChange = "displaychange";

    /// <summary>Input-to-display latency.</summary>
    public const string PcLatency = "pclatency";

    /// <summary>What a caller gets without naming any.</summary>
    public static IReadOnlyList<string> Default { get; } = [Frametimes];

    /// <summary>Every kind this service can produce.</summary>
    public static IReadOnlyList<string> All { get; } = [Frametimes, Fps, DisplayChange, PcLatency];
}
