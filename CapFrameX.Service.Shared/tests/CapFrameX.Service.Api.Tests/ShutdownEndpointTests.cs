using System.Net;

namespace CapFrameX.Service.Api.Tests;

/// <summary>
/// Stopping the service is what releases PresentMon, the overlay and the session token in order;
/// a killed process leaves all three behind.
/// </summary>
public sealed class ShutdownEndpointTests(GuardedApiFactory factory) : IClassFixture<GuardedApiFactory>
{
    [Fact]
    public async Task Shutdown_is_refused_without_a_token()
    {
        // Stopping an elevated service is exactly the kind of thing the guard exists for.
        using var client = factory.CreateCaller(token: null);

        var response = await client.PostAsync("/api/app/shutdown", content: null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Shutdown_is_not_reachable_by_a_get()
    {
        using var client = factory.CreateCaller();

        var response = await client.GetAsync("/api/app/shutdown");

        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
    }
}
