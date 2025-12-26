namespace CapFrameX.Service.Data.Models;

/// <summary>
/// Defines the type of benchmark suite.
/// </summary>
public enum SuiteType
{
    /// <summary>
    /// Hardware review - comparing different hardware components
    /// </summary>
    HardwareReview,

    /// <summary>
    /// Game review - comparing different game settings
    /// </summary>
    GameReview,

    /// <summary>
    /// Comparison set - custom comparisons
    /// </summary>
    ComparisonSet,

    /// <summary>
    /// Aggregation of sessions without specific type
    /// </summary>
    Miscellaneous
}
