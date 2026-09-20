using CapFrameX.Service.Core.Security;
using Microsoft.AspNetCore.Mvc.Testing;

namespace CapFrameX.Service.Api.Tests;

/// <summary>
/// Hosts the real API in-process with a known session token, so the guard can be exercised exactly
/// as a caller would meet it.
/// </summary>
public sealed class GuardedApiFactory : WebApplicationFactory<Program>
{
    /// <summary>The token the hosted service was started with.</summary>
    public const string Token = "TestTokenTestTokenTestTokenTestTokenTestTok";

    /// <summary>Host header a legitimate caller sends.</summary>
    public const string OwnHost = "127.0.0.1:1337";

    /// <summary>Creates the factory and pins the token the service will accept.</summary>
    public GuardedApiFactory() =>
        Environment.SetEnvironmentVariable("CAPFRAMEX_SERVICE_TOKEN", Token);

    /// <summary>A client whose requests look like they come from the frontend.</summary>
    /// <param name="token">Token to present, or <c>null</c> to present none.</param>
    /// <param name="host">Host header to send.</param>
    /// <param name="origin">Origin header to send, if any.</param>
    public HttpClient CreateCaller(string? token = Token, string host = OwnHost, string? origin = null)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Host = host;

        if (token is not null)
        {
            client.DefaultRequestHeaders.Add(LocalApiGuard.TokenHeaderName, token);
        }

        if (origin is not null)
        {
            client.DefaultRequestHeaders.Add("Origin", origin);
        }

        return client;
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Environment.SetEnvironmentVariable("CAPFRAMEX_SERVICE_TOKEN", null);
        }

        base.Dispose(disposing);
    }
}
