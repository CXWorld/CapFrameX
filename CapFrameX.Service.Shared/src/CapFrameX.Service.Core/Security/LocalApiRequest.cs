namespace CapFrameX.Service.Core.Security;

/// <summary>
/// The parts of an incoming request the guard judges, lifted out of any web framework so the rules
/// can be tested without one.
/// </summary>
/// <param name="HeaderToken">Token from the CapFrameX token header, if present.</param>
/// <param name="QueryToken">Token from the query string, if present.</param>
/// <param name="Host">Value of the <c>Host</c> header.</param>
/// <param name="Origin">Value of the <c>Origin</c> header, if the caller sent one.</param>
/// <param name="AllowQueryToken">
/// Whether this endpoint may take its token from the query string. True only for the event stream
/// and the live socket, which cannot set request headers.
/// </param>
public readonly record struct LocalApiRequest(
    string? HeaderToken,
    string? QueryToken,
    string Host,
    string? Origin,
    bool AllowQueryToken);
