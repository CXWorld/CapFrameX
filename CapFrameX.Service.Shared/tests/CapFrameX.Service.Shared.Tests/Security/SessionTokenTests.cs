using CapFrameX.Service.Core.Security;

namespace CapFrameX.Service.Shared.Tests.Security;

/// <summary>
/// The session token is the only thing standing between an arbitrary local program and an elevated
/// service, so how it is generated and compared is pinned down here.
/// </summary>
public sealed class SessionTokenTests
{
    [Fact]
    public void Generated_token_has_at_least_256_bits_of_entropy()
    {
        var token = SessionToken.Generate();

        // base64url of 32 bytes, without padding.
        Assert.Equal(43, token.Value.Length);
    }

    [Fact]
    public void Generated_tokens_differ()
    {
        var tokens = Enumerable.Range(0, 64).Select(_ => SessionToken.Generate().Value).ToHashSet();

        Assert.Equal(64, tokens.Count);
    }

    [Fact]
    public void Generated_token_is_url_safe()
    {
        // It has to survive a query string unescaped, because EventSource cannot set headers.
        var value = SessionToken.Generate().Value;

        Assert.DoesNotContain('+', value);
        Assert.DoesNotContain('/', value);
        Assert.DoesNotContain('=', value);
    }

    [Fact]
    public void Token_matches_itself()
    {
        var token = SessionToken.Generate();

        Assert.True(token.Matches(token.Value));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("wrong")]
    public void Token_does_not_match_anything_else(string? candidate)
    {
        Assert.False(SessionToken.Generate().Matches(candidate));
    }

    [Fact]
    public void Comparison_does_not_depend_on_where_the_candidate_differs()
    {
        // A length-independent, early-exit comparison leaks the token one character at a time.
        var token = new SessionToken(new string('a', 43));

        Assert.False(token.Matches('b' + new string('a', 42)));
        Assert.False(token.Matches(new string('a', 42) + 'b'));
    }

    [Fact]
    public void Empty_token_cannot_be_constructed()
    {
        Assert.Throws<ArgumentException>(() => new SessionToken(string.Empty));
    }
}
