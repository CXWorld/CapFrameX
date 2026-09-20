using CapFrameX.Service.Core.Security;

namespace CapFrameX.Service.Api;

/// <summary>
/// What a composition root has to tell the API about the process it is hosted in.
/// </summary>
public sealed class CapFrameXApiOptions
{
    /// <summary>
    /// Loopback port the host listens on. The guard rejects any request whose <c>Host</c> header
    /// names something else, so this must be the port Kestrel was actually bound to.
    /// </summary>
    public int Port { get; init; } = DefaultPort;

    /// <summary>Port the Angular dev server runs on, accepted as an origin during development.</summary>
    public int FrontendDevPort { get; init; } = 4200;

    /// <summary>The token this service instance accepts.</summary>
    public required SessionToken Token { get; init; }

    /// <summary>The port CapFrameX uses unless a host overrides it.</summary>
    public const int DefaultPort = 1337;
}
