namespace CapFrameX.Service.Data.Models;

/// <summary>
/// Represents a collection of related benchmark sessions.
/// A suite groups sessions by purpose (hardware review, game benchmark, or comparison).
/// </summary>
public class Suite
{
    /// <summary>
    /// Unique identifier for the suite
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Name of the suite
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Optional description of the suite
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Type of suite (HardwareReview, GameBenchmark, ComparisonSet)
    /// </summary>
    public SuiteType Type { get; set; }

    /// <summary>
    /// Creation timestamp
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Last modified timestamp
    /// </summary>
    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// Sessions belonging to this suite
    /// </summary>
    public ICollection<Session> Sessions { get; set; } = new List<Session>();
}
