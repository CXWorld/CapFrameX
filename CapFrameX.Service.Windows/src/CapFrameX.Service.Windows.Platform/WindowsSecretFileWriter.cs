using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;
using CapFrameX.Service.Core.Security;

namespace CapFrameX.Service.Windows.Platform;

/// <summary>
/// Writes a file whose access control list names the current user and nobody else.
/// </summary>
/// <remarks>
/// Inheritance is switched off rather than merely added to: a runtime directory that inherits
/// "Users: read" from its parent would hand the session token to every account on the machine, and
/// the inherited entry survives any rule this class adds.
/// </remarks>
[SupportedOSPlatform("windows")]
public sealed class WindowsSecretFileWriter : ISecretFileWriter
{
    /// <inheritdoc />
    public void Write(string path, string content)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(content);

        var user = WindowsIdentity.GetCurrent().User
            ?? throw new InvalidOperationException("The current identity has no user SID.");

        // Create the file empty, harden it, then write - so the token never exists on disk under
        // the permissions inherited from the directory.
        var file = new FileInfo(path);

        using (file.Create())
        {
        }

        var security = new FileSecurity();

        // Changing the DACL needs WRITE_DAC, which a plain write handle does not carry; going
        // through FileInfo opens the file with the right access.
        security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
        security.SetOwner(user);
        security.AddAccessRule(new FileSystemAccessRule(
            user,
            FileSystemRights.Read | FileSystemRights.Write | FileSystemRights.Delete,
            AccessControlType.Allow));

        file.SetAccessControl(security);

        File.WriteAllText(path, content);
    }
}
