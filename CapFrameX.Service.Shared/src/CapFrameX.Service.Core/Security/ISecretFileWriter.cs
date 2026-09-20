namespace CapFrameX.Service.Core.Security;

/// <summary>
/// Writes a file only the current user can read.
/// </summary>
/// <remarks>
/// How that is enforced is the one platform-specific part of the token hand-over: Windows replaces
/// the file's ACL and switches inheritance off, Linux sets mode 0600. Everything else about the
/// hand-over is the same on both and lives in <see cref="SessionTokenStore"/>.
/// </remarks>
public interface ISecretFileWriter
{
    /// <summary>Writes the text, replacing any existing file, readable by the current user only.</summary>
    /// <param name="path">Absolute path of the file.</param>
    /// <param name="content">Text to write.</param>
    void Write(string path, string content);
}
