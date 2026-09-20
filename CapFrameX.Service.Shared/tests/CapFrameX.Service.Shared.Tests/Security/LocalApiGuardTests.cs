using CapFrameX.Service.Core.Security;

namespace CapFrameX.Service.Shared.Tests.Security;

/// <summary>
/// The Windows service runs elevated and listens on loopback, so its API is a privilege boundary:
/// anything the API grants, any medium-integrity program of this user could otherwise do as
/// administrator. These are the rules that boundary rests on.
/// </summary>
public sealed class LocalApiGuardTests
{
    private const string Secret = "n2Tq8kVw0Zx7cYbM3rLp9sJdF1hGtEaQ5uWnRiOkXyZ";
    private const string Wrong = "n2Tq8kVw0Zx7cYbM3rLp9sJdF1hGtEaQ5uWnRiOkXyA";

    private static LocalApiGuard Guard() => new(new SessionToken(Secret), 1337);

    private static LocalApiRequest Request(
        string? token = Secret,
        string? queryToken = null,
        string host = "127.0.0.1:1337",
        string? origin = null,
        bool allowQueryToken = false) =>
        new(token, queryToken, host, origin, allowQueryToken);

    [Fact]
    public void Correct_token_in_the_header_is_allowed()
    {
        Assert.True(Guard().Evaluate(Request()).IsAllowed);
    }

    [Fact]
    public void Request_without_a_token_is_refused()
    {
        var decision = Guard().Evaluate(Request(token: null));

        Assert.False(decision.IsAllowed);
        Assert.Equal(LocalApiDenialReason.MissingToken, decision.Reason);
    }

    [Theory]
    [InlineData(Wrong)]
    [InlineData("short")]
    public void Wrong_token_is_refused(string token)
    {
        var decision = Guard().Evaluate(Request(token: token));

        Assert.False(decision.IsAllowed);
        Assert.Equal(LocalApiDenialReason.InvalidToken, decision.Reason);
    }

    [Fact]
    public void Empty_token_header_counts_as_none_presented()
    {
        var decision = Guard().Evaluate(Request(token: string.Empty));

        Assert.False(decision.IsAllowed);
        Assert.Equal(LocalApiDenialReason.MissingToken, decision.Reason);
    }

    [Fact]
    public void Stream_endpoint_may_carry_the_token_in_the_query()
    {
        // EventSource and WebSocket cannot set request headers.
        var decision = Guard().Evaluate(Request(token: null, queryToken: Secret, allowQueryToken: true));

        Assert.True(decision.IsAllowed);
    }

    [Fact]
    public void Ordinary_endpoint_ignores_a_token_in_the_query()
    {
        // A query token ends up in logs and referrers; it is tolerated only where a header is
        // impossible.
        var decision = Guard().Evaluate(Request(token: null, queryToken: Secret, allowQueryToken: false));

        Assert.False(decision.IsAllowed);
        Assert.Equal(LocalApiDenialReason.MissingToken, decision.Reason);
    }

    [Theory]
    [InlineData("127.0.0.1:1337")]
    [InlineData("localhost:1337")]
    [InlineData("[::1]:1337")]
    public void Loopback_host_headers_are_accepted(string host)
    {
        Assert.True(Guard().Evaluate(Request(host: host)).IsAllowed);
    }

    [Theory]
    [InlineData("evil.example.com:1337")]
    [InlineData("127.0.0.1:1338")]
    [InlineData("127.0.0.1")]
    [InlineData("")]
    public void Foreign_or_mismatched_host_header_is_refused(string host)
    {
        // A page on any website can resolve its own name to 127.0.0.1 and talk to this port; the
        // Host header is what tells the two apart.
        var decision = Guard().Evaluate(Request(host: host));

        Assert.False(decision.IsAllowed);
        Assert.Equal(LocalApiDenialReason.ForeignHost, decision.Reason);
    }

    [Theory]
    [InlineData("app://capframex")]
    [InlineData("http://localhost:4200")]
    [InlineData("http://127.0.0.1:4200")]
    public void Origins_the_frontend_uses_are_accepted(string origin)
    {
        Assert.True(Guard().Evaluate(Request(origin: origin)).IsAllowed);
    }

    [Fact]
    public void Request_without_an_origin_is_accepted()
    {
        // Non-browser clients send none; the token is what authenticates them.
        Assert.True(Guard().Evaluate(Request(origin: null)).IsAllowed);
    }

    [Theory]
    [InlineData("https://evil.example.com")]
    [InlineData("http://localhost:4201")]
    [InlineData("null")]
    public void Foreign_origin_is_refused_even_with_a_valid_token(string origin)
    {
        var decision = Guard().Evaluate(Request(origin: origin));

        Assert.False(decision.IsAllowed);
        Assert.Equal(LocalApiDenialReason.ForeignOrigin, decision.Reason);
    }

    [Fact]
    public void Host_is_checked_before_the_token_is_compared()
    {
        // Never reveal anything about the token to a caller that is already out of bounds.
        var decision = Guard().Evaluate(Request(token: Wrong, host: "evil.example.com:1337"));

        Assert.Equal(LocalApiDenialReason.ForeignHost, decision.Reason);
    }
}
