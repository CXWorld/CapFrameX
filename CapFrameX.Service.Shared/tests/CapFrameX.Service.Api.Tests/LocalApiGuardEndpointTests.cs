using System.Net;
using CapFrameX.Service.Core.Security;

namespace CapFrameX.Service.Api.Tests;

/// <summary>
/// The guard is unit-tested as a policy; this proves it is actually in the request pipeline, which
/// is the part a refactor can silently undo.
/// </summary>
public sealed class LocalApiGuardEndpointTests(GuardedApiFactory factory) : IClassFixture<GuardedApiFactory>
{
    [Theory]
    [InlineData("/api/health")]
    [InlineData("/api/app/version")]
    [InlineData("/api/capabilities")]
    [InlineData("/api/capture/status")]
    [InlineData("/api/records")]
    public async Task Endpoint_answers_a_caller_with_the_token(string path)
    {
        using var client = factory.CreateCaller();

        var response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData("/api/health")]
    [InlineData("/api/app/version")]
    [InlineData("/api/capabilities")]
    [InlineData("/api/capture/status")]
    [InlineData("/api/records")]
    public async Task Endpoint_refuses_a_caller_without_a_token(string path)
    {
        // Every route, not just the mutating ones: on Windows this process is elevated.
        using var client = factory.CreateCaller(token: null);

        var response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Wrong_token_is_refused()
    {
        using var client = factory.CreateCaller(token: "not-the-token");

        var response = await client.GetAsync("/api/health");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Request_naming_a_foreign_host_is_refused()
    {
        // A web page can resolve its own name to 127.0.0.1; the Host header is what betrays it.
        using var client = factory.CreateCaller(host: "evil.example.com:1337");

        var response = await client.GetAsync("/api/health");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Request_from_a_foreign_origin_is_refused()
    {
        using var client = factory.CreateCaller(origin: "https://evil.example.com");

        var response = await client.GetAsync("/api/health");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Frontend_origin_is_accepted()
    {
        using var client = factory.CreateCaller(origin: "app://capframex");

        var response = await client.GetAsync("/api/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Refusal_is_problem_details_and_names_no_check()
    {
        using var client = factory.CreateCaller(token: null);

        var response = await client.GetAsync("/api/health");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.DoesNotContain("token", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("origin", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Event_stream_accepts_the_token_from_the_query()
    {
        // EventSource cannot set request headers.
        using var client = factory.CreateCaller(token: null);
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"/api/events?{LocalApiGuard.TokenQueryName}={GuardedApiFactory.Token}");

        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellation.Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Ordinary_endpoint_does_not_accept_the_token_from_the_query()
    {
        using var client = factory.CreateCaller(token: null);

        var response = await client.GetAsync(
            $"/api/health?{LocalApiGuard.TokenQueryName}={GuardedApiFactory.Token}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
