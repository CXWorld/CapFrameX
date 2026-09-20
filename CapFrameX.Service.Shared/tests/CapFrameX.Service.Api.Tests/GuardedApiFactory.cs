using CapFrameX.Service.Application.Records;
using CapFrameX.Service.Core.Platform;
using CapFrameX.Service.Core.Security;
using CapFrameX.Service.Data;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
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

    /// <summary>
    /// Host header a legitimate caller sends.
    /// </summary>
    /// <remarks>
    /// The guard compares this against the port the service bound, so the two have to agree. It
    /// has to be a compile-time constant to serve as a default argument, so it cannot read
    /// <see cref="CapFrameXApiOptions.DefaultPort"/> - <c>The_test_host_uses_the_service_port</c>
    /// pins them together instead, and fails on its own rather than as ninety-nine tests that say
    /// nothing about what moved.
    /// </remarks>
    public const string OwnHost = "127.0.0.1:" + PortText;

    private const string PortText = "17337";

    private readonly string _root = Path.Combine(Path.GetTempPath(), "cfx-api-" + Guid.NewGuid().ToString("N"));

    private WebApplication? _app;

    /// <summary>The hosted services, so a test can seed what an endpoint reads.</summary>
    public IServiceProvider Services => _app!.Services;

    /// <summary>The trash the delete endpoint reaches, so a test can see what went into it.</summary>
    public RecordingTrash Trash { get; } = new();

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

        // The endpoints that read records need the database the hosts register alongside the API.
        Directory.CreateDirectory(_root);
        var databasePath = Path.Combine(_root, CapFrameXDatabaseExtensions.FileName);
        builder.Services.AddDbContext<CapFrameXDbContext>(
            options => options.UseSqlite($"Data Source={databasePath}"));

        // The analysis endpoints read the capture behind a record, which the hosts wire up the
        // same way.
        builder.Services.AddCapFrameXAnalysis();
        builder.Services.AddSingleton<IFileTrash>(Trash);

        // The index, but not the folder watcher: editing a record has to re-read it at once, while
        // a background scan of a temp folder would only add timing to every test here.
        var testPaths = new TestPaths(_root);
        builder.Services.AddSingleton<IAppPaths>(testPaths);
        builder.Services.AddCapFrameXRecordIndex(testPaths);

        _app = builder.Build();
        _app.MapCapFrameXApi();

        using (var scope = _app.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<CapFrameXDbContext>().Database.MigrateAsync();
        }

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

        foreach (var folder in new[] { _root, Trash.Directory })
        {
            TryDelete(folder);
        }
    }

    /// <summary>A layout under one temp folder, which is all the index needs to be constructed.</summary>
    /// <param name="root">The temp folder.</param>
    private sealed class TestPaths(string root) : IAppPaths
    {
        public string ConfigurationDirectory { get; } = Path.Combine(root, "Configuration");

        public string DataDirectory { get; } = root;

        public string CaptureDirectory { get; } = Path.Combine(root, "Captures");

        public string LogDirectory { get; } = Path.Combine(root, "Logs");

        public string RuntimeDirectory { get; } = Path.Combine(root, "run");

        public bool IsPortable => true;
    }

    private static void TryDelete(string folder)
    {
        try
        {
            Directory.Delete(folder, recursive: true);
        }
        catch (Exception exception) when (exception is IOException or DirectoryNotFoundException)
        {
            // A temp folder that outlives the run is not a test failure.
        }
    }
}
