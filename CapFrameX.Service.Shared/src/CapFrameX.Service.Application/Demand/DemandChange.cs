namespace CapFrameX.Service.Application.Demand;

/// <summary>What happened to one demand key.</summary>
public enum DemandChangeKind
{
    /// <summary>The first lease was taken; whatever serves this key must start.</summary>
    Activated,

    /// <summary>The last lease was released and the grace period elapsed; the key can stand down.</summary>
    Deactivated,

    /// <summary>The key stays active but the interval its holders accept has changed.</summary>
    IntervalChanged,
}

/// <summary>A change to one demand key.</summary>
/// <param name="Key">The demand key.</param>
/// <param name="Kind">What happened.</param>
/// <param name="Interval">
/// Shortest interval any current holder insists on, or <c>null</c> when no holder stated one.
/// </param>
public sealed record DemandChange(string Key, DemandChangeKind Kind, TimeSpan? Interval);
