using System.Runtime.Versioning;
using CapFrameX.Service.Core.Security;

namespace CapFrameX.Service.Linux.Platform;

/// <summary>
/// Writes a file with mode <c>0600</c>: readable and writable by its owner, invisible to everyone
/// else.
/// </summary>
/// <remarks>
/// The mode is applied to the empty file before the content is written, so the token never exists
/// on disk under the process umask.
/// </remarks>
[UnsupportedOSPlatform("windows")]
public sealed class PosixSecretFileWriter : ISecretFileWriter
{
    private const UnixFileMode OwnerOnly = UnixFileMode.UserRead | UnixFileMode.UserWrite;

    /// <inheritdoc />
    public void Write(string path, string content)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(content);

        using (new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None))
        {
        }

        File.SetUnixFileMode(path, OwnerOnly);
        File.WriteAllText(path, content);
    }
}
