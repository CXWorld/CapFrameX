using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;

namespace CapFrameX.Service.Core.Security;

/// <summary>
/// The secret a service instance issues at start and hands to its own frontend.
/// </summary>
/// <remarks>
/// It authenticates "a program this user started with the service", nothing stronger - any process
/// of the same user can read it. That is enough for its job: it stops a web page, or a program of
/// another user, from reaching an API that on Windows runs elevated. It is generated per start and
/// never persisted beyond the run.
/// </remarks>
public sealed class SessionToken
{
    private const int SizeInBytes = 32;

    private readonly byte[] _value;

    /// <summary>Wraps an existing token value, for a host that received one.</summary>
    /// <param name="value">The token as it travels on the wire.</param>
    /// <exception cref="ArgumentException">The value is empty.</exception>
    public SessionToken(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        Value = value;
        _value = Encoding.UTF8.GetBytes(value);
    }

    /// <summary>The token as it travels on the wire: base64url, no padding.</summary>
    public string Value { get; }

    /// <summary>Creates a fresh token from a cryptographic random source.</summary>
    public static SessionToken Generate() =>
        new(Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(SizeInBytes)));

    /// <summary>
    /// Whether a presented value is this token, compared in time independent of where it differs.
    /// </summary>
    /// <param name="candidate">The value a caller presented.</param>
    public bool Matches(string? candidate)
    {
        if (string.IsNullOrEmpty(candidate))
        {
            return false;
        }

        // FixedTimeEquals returns false for different lengths without comparing, so the length
        // itself is not secret - the token's length is fixed and public anyway.
        return CryptographicOperations.FixedTimeEquals(_value, Encoding.UTF8.GetBytes(candidate));
    }
}
