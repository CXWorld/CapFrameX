using CapFrameX.Statistics.NetStandard.Contracts;

namespace CapFrameX.Service.Analysis;

/// <summary>
/// The analysis options the user sets once, and the statistics provider reads on every call.
/// </summary>
/// <remarks>
/// The defaults are the ones CapFrameX 1.x ships, so a record analysed here and in the desktop app
/// produces the same numbers without anyone configuring anything. It implements
/// <see cref="IFrametimeStatisticProviderOptions"/> directly rather than mapping onto it: a second
/// object in between is one more place for a default to drift.
/// </remarks>
public sealed class AnalysisSettings : IFrametimeStatisticProviderOptions
{
    /// <inheritdoc />
    public int MovingAverageWindowSize { get; set; } = 100;

    /// <inheritdoc />
    public int IntervalAverageWindowTime { get; set; } = 500;

    /// <inheritdoc />
    public int FpsValuesRoundingDigits { get; set; } = 1;

    /// <summary>How far above its local average a frame counts as stutter.</summary>
    public double StutteringFactor { get; set; } = 2.5;

    /// <summary>Frame rate below which a frame counts as low FPS rather than stutter.</summary>
    public double StutteringThreshold { get; set; } = 25d;
}
