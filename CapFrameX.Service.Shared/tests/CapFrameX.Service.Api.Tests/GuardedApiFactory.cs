using CapFrameX.Service.Core.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CapFrameX.Service.Api.Tests;

/// <summary>
/// Hosts the API exactly as a composition root does - <c>AddCapFrameXApi</c> plus
/// <c>MapCapFrameXApi</c> - with a known session token.
/// </summary>
/// <remarks>
/// Deliberately not a copy of the pipeline: if a host ever adds the guard itself instead of
/// getting it from the library, or the library stops adding it, these tests notice.
/// </remarks>
public sealed class GuardedApiFactory : IAsyncLifetime
{
    /// <summary>The token the hosted service accepts.</summary>
    public const string Token = "TestTokenTestTokenTestTokenTestTokenTestTok";

    /// <summary>Host header a legitimate caller sends.</summary>
    public const string OwnHost = "127.0.0.1:1337";

    private WebApplication? _app;

    /// <inheritdoc />
    public async Task InitializeAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();

        builder.Services.AddCapFrameXApi(new CapFrameXApiOptions
        {
            Token = new SessionToken(Token),
            Port = CapFrameXApiOptions.DefaultPort,
        });

        _app = builder.Build();
        _app.MapCapFrameXApi();

        await _app.StartAsync();
    }

    /// <summary>A client whose requests look like they come from the frontend.</summary>
    /// <param name="token">Token to present, or <c>null</c> to present none.</param>
    /// <param name="host">Host header to send.</param>
    /// <param name="origin">Origin header to send, if any.</param>
    public HttpClient CreateCaller(string? token = Token, string host = OwnHost, string? origin = null)
    {
        var client = _app!.GetTestClient();
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
    public async Task DisposeAsync()
    {
        if (_app is not null)
        {
            await _app.DisposeAsync();
        }
    }
}
