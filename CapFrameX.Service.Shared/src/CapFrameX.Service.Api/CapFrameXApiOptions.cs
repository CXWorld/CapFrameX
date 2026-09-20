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

    /// <summary>
    /// The port CapFrameX uses unless a host overrides it.
    /// </summary>
    /// <remarks>
    /// Deliberately not 1337: that is CapFrameX 1.x's own web service port
    /// (<c>WebservicePort</c>, which defaults to it), and the two applications are meant to run
    /// side by side while records are being moved over. Sharing the number means whichever starts
    /// second does not start at all.
    /// </remarks>
    public const int DefaultPort = 17337;

    /// <summary>
    /// Environment variable a host sets to put the service on another port.
    /// </summary>
    /// <remarks>
    /// The same way the token is handed over, and for the same reason: the frontend starts the
    /// service, so the frontend is what knows which port it managed to take.
    /// </remarks>
    public const string PortVariable = "CAPFRAMEX_SERVICE_PORT";

    /// <summary>Reads the port a host asked for, or the default.</summary>
    /// <param name="read">Reads an environment variable; defaults to the process environment.</param>
    public static int ResolvePort(Func<string, string?>? read = null)
    {
        var value = (read ?? Environment.GetEnvironmentVariable)(PortVariable);

        return int.TryParse(value, out var port) && port is > 0 and < 65536 ? port : DefaultPort;
    }
}
