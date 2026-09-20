namespace CapFrameX.Service.Core.Security;

/// <summary>
/// Decides whether a request reaching the localhost API may proceed.
/// </summary>
/// <remarks>
/// Three checks, in this order:
/// <list type="number">
/// <item>the <c>Host</c> header names this service's loopback endpoint - any website can point its
/// own name at 127.0.0.1, and the Host header is what distinguishes that from a local caller;</item>
/// <item>the <c>Origin</c>, when the caller sent one, is the CapFrameX frontend - a browser always
/// sends it on cross-origin requests, so this is what stops a page from driving the service;</item>
/// <item>the session token matches.</item>
/// </list>
/// Host comes first deliberately: a caller that is already out of bounds learns nothing about the
/// token, not even whether one was close.
/// </remarks>
public sealed class LocalApiGuard
{
    /// <summary>Header the frontend presents its token in.</summary>
    public const string TokenHeaderName = "X-CapFrameX-Token";

    /// <summary>Query parameter the event stream and the live socket use instead.</summary>
    public const string TokenQueryName = "access_token";

    private static readonly string[] AllowedOriginHosts = ["localhost", "127.0.0.1"];
    private static readonly string[] AllowedAppOrigins = ["app://capframex", "capframex://app"];

    private readonly SessionToken _token;
    private readonly int _port;
    private readonly int _frontendDevPort;

    /// <summary>Creates a guard for one service instance.</summary>
    /// <param name="token">The token this instance issued.</param>
    /// <param name="port">The loopback port the API listens on.</param>
    /// <param name="frontendDevPort">Port the Angular dev server runs on during development.</param>
    public LocalApiGuard(SessionToken token, int port, int frontendDevPort = 4200)
    {
        ArgumentNullException.ThrowIfNull(token);

        _token = token;
        _port = port;
        _frontendDevPort = frontendDevPort;
    }

    /// <summary>Judges one request.</summary>
    /// <param name="request">The parts of the request that matter.</param>
    public LocalApiDecision Evaluate(LocalApiRequest request)
    {
        if (!IsOwnEndpoint(request.Host))
        {
            return LocalApiDecision.Deny(LocalApiDenialReason.ForeignHost);
        }

        if (request.Origin is { Length: > 0 } origin && !IsFrontendOrigin(origin))
        {
            return LocalApiDecision.Deny(LocalApiDenialReason.ForeignOrigin);
        }

        var presented = request.HeaderToken;

        if (string.IsNullOrEmpty(presented) && request.AllowQueryToken)
        {
            presented = request.QueryToken;
        }

        if (string.IsNullOrEmpty(presented))
        {
            return LocalApiDecision.Deny(LocalApiDenialReason.MissingToken);
        }

        return _token.Matches(presented)
            ? LocalApiDecision.Allow
            : LocalApiDecision.Deny(LocalApiDenialReason.InvalidToken);
    }

    private bool IsOwnEndpoint(string host)
    {
        if (string.IsNullOrEmpty(host))
        {
            return false;
        }

        // IPv6 literals arrive bracketed: [::1]:1337.
        var separator = host.LastIndexOf(':');

        if (separator < 0 || separator < host.LastIndexOf(']'))
        {
            return false;
        }

        var name = host[..separator];
        var port = host[(separator + 1)..];

        return int.TryParse(port, out var parsed)
            && parsed == _port
            && (AllowedOriginHosts.Contains(name, StringComparer.OrdinalIgnoreCase) || name == "[::1]");
    }

    private bool IsFrontendOrigin(string origin)
    {
        if (AllowedAppOrigins.Contains(origin, StringComparer.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri))
        {
            return false;
        }

        return uri.Scheme is "http" or "https"
            && AllowedOriginHosts.Contains(uri.Host, StringComparer.OrdinalIgnoreCase)
            && (uri.Port == _port || uri.Port == _frontendDevPort);
    }
}
