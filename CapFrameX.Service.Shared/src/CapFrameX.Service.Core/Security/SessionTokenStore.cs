using CapFrameX.Service.Core.Platform;

namespace CapFrameX.Service.Core.Security;

/// <summary>
/// Publishes the running service's session token for a frontend that did not start it.
/// </summary>
/// <remarks>
/// The default start order is frontend first, which hands the token to the service through its
/// environment and needs no file at all. This exists for the other order: a service started on its
/// own, or a second frontend attaching to one that is already running. The file is rewritten on
/// every start and removed on shutdown, so a leftover token cannot authenticate anything.
/// </remarks>
/// <param name="paths">Supplies the runtime directory.</param>
/// <param name="writer">Writes the file with restricted access.</param>
public sealed class SessionTokenStore(IAppPaths paths, ISecretFileWriter writer)
{
    /// <summary>Name of the file inside the runtime directory.</summary>
    public const string FileName = "service.token";

    private readonly IAppPaths _paths = paths ?? throw new ArgumentNullException(nameof(paths));
    private readonly ISecretFileWriter _writer = writer ?? throw new ArgumentNullException(nameof(writer));

    /// <summary>Full path of the token file.</summary>
    public string Path => System.IO.Path.Combine(_paths.RuntimeDirectory, FileName);

    /// <summary>Writes the token, creating the runtime directory if needed.</summary>
    /// <param name="token">The token this service instance issued.</param>
    public void Publish(SessionToken token)
    {
        ArgumentNullException.ThrowIfNull(token);

        Directory.CreateDirectory(_paths.RuntimeDirectory);

        // Never File.WriteAllText: the token has to land with an ACL, or with mode 0600, that
        // keeps other users out.
        _writer.Write(Path, token.Value);
    }

    /// <summary>
    /// Reads a previously published token, or <c>null</c> when there is none to read.
    /// </summary>
    public SessionToken? Read()
    {
        var path = Path;

        if (!File.Exists(path))
        {
            return null;
        }

        string content;

        try
        {
            content = File.ReadAllText(path);
        }
        catch (IOException)
        {
            // A service writing the file at this moment is not an error for the reader; it will
            // try again or start its own service.
            return null;
        }

        var value = content.Trim();

        return value.Length == 0 ? null : new SessionToken(value);
    }

    /// <summary>Removes the token file; doing so twice is harmless.</summary>
    public void Revoke()
    {
        try
        {
            File.Delete(Path);
        }
        catch (DirectoryNotFoundException)
        {
            // Nothing was ever published.
        }
    }
}
