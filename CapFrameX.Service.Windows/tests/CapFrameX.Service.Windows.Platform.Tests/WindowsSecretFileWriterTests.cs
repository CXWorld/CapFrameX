using System.Security.AccessControl;
using System.Security.Principal;
using CapFrameX.Service.Windows.Platform;

namespace CapFrameX.Service.Windows.Platform.Tests;

/// <summary>
/// The session token file is what keeps another account on this machine out of an elevated
/// service, so its access control list is asserted rather than assumed.
/// </summary>
public sealed class WindowsSecretFileWriterTests : IDisposable
{
    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), "cfx-secret-" + Guid.NewGuid().ToString("N"));

    private readonly WindowsSecretFileWriter _writer = new();

    private string Path_ => Path.Combine(_directory, "service.token");

    public WindowsSecretFileWriterTests() => Directory.CreateDirectory(_directory);

    [Fact]
    public void Writes_the_content()
    {
        _writer.Write(Path_, "the-token");

        Assert.Equal("the-token", File.ReadAllText(Path_));
    }

    [Fact]
    public void Replaces_an_existing_file()
    {
        File.WriteAllText(Path_, "old and longer than the new one");

        _writer.Write(Path_, "new");

        Assert.Equal("new", File.ReadAllText(Path_));
    }

    [Fact]
    public void Access_is_granted_to_the_current_user_only()
    {
        _writer.Write(Path_, "the-token");

        var rules = new FileInfo(Path_)
            .GetAccessControl()
            .GetAccessRules(includeExplicit: true, includeInherited: true, typeof(SecurityIdentifier))
            .Cast<FileSystemAccessRule>()
            .ToArray();

        var user = WindowsIdentity.GetCurrent().User!;

        Assert.NotEmpty(rules);
        Assert.All(rules, rule => Assert.Equal(user, rule.IdentityReference));
    }

    [Fact]
    public void Inherited_permissions_are_switched_off()
    {
        // Without this an inherited "Users: read" on the parent directory would still apply, and
        // adding an explicit rule would not remove it.
        _writer.Write(Path_, "the-token");

        var inherited = new FileInfo(Path_)
            .GetAccessControl()
            .GetAccessRules(includeExplicit: false, includeInherited: true, typeof(SecurityIdentifier));

        Assert.Empty(inherited);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }
}
