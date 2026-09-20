namespace CapFrameX.Service.Core.Security;

/// <summary>Why the guard refused a request.</summary>
public enum LocalApiDenialReason
{
    /// <summary>The request was allowed.</summary>
    None = 0,

    /// <summary>No token was presented where one is required.</summary>
    MissingToken,

    /// <summary>A token was presented but is not this session's.</summary>
    InvalidToken,

    /// <summary>The <c>Host</c> header does not name this service's loopback endpoint.</summary>
    ForeignHost,

    /// <summary>The <c>Origin</c> header names something other than the CapFrameX frontend.</summary>
    ForeignOrigin,
}

/// <summary>The guard's verdict on one request.</summary>
/// <param name="IsAllowed">Whether the request may proceed.</param>
/// <param name="Reason">Why it was refused; <see cref="LocalApiDenialReason.None"/> when allowed.</param>
public readonly record struct LocalApiDecision(bool IsAllowed, LocalApiDenialReason Reason)
{
    /// <summary>The request may proceed.</summary>
    public static LocalApiDecision Allow { get; } = new(true, LocalApiDenialReason.None);

    /// <summary>The request is refused.</summary>
    /// <param name="reason">Why.</param>
    public static LocalApiDecision Deny(LocalApiDenialReason reason) => new(false, reason);
}
