namespace CapFrameX.Service.Core.Platform;

/// <summary>
/// Whether a platform port can do its job on this machine, and if not, why. A port that reports
/// unavailable must not throw on use and must never prevent service start-up; the reason is what
/// the frontend shows in place of the feature.
/// </summary>
/// <param name="IsAvailable">True when the port is usable.</param>
/// <param name="Reason">Human-readable reason when unavailable; <c>null</c> when available.</param>
public sealed record PlatformAvailability(bool IsAvailable, string? Reason = null)
{
    /// <summary>The port is usable.</summary>
    public static PlatformAvailability Available { get; } = new(true);

    /// <summary>The port cannot be used on this machine.</summary>
    /// <param name="reason">Why it cannot be used, phrased for a user.</param>
    public static PlatformAvailability Unavailable(string reason) => new(false, reason);
}
