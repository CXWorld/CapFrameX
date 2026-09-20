using CapFrameX.Service.Windows.Platform;

namespace CapFrameX.Service.Windows.Platform.Tests;

/// <summary>
/// These locations are shared with CapFrameX 1.x, so a change here silently hides a user's
/// settings or captures from one of the two generations.
/// </summary>
public sealed class WindowsAppPathsTests
{
    private static string Folder(Environment.SpecialFolder folder) => folder switch
    {
        Environment.SpecialFolder.ApplicationData => @"C:\Users\Tester\AppData\Roaming",
        Environment.SpecialFolder.LocalApplicationData => @"C:\Users\Tester\AppData\Local",
        Environment.SpecialFolder.MyDocuments => @"C:\Users\Tester\Documents",
        _ => @"C:\Users\Tester",
    };

    private static WindowsAppPaths Paths(string? portableRoot = null) => new(Folder, portableRoot);

    [Fact]
    public void Settings_stay_where_1_x_put_them()
    {
        Assert.Equal(@"C:\Users\Tester\AppData\Roaming\CapFrameX\Configuration", Paths().ConfigurationDirectory);
    }

    [Fact]
    public void Captures_default_to_the_documents_folder()
    {
        Assert.Equal(@"C:\Users\Tester\Documents\CapFrameX\Captures", Paths().CaptureDirectory);
    }

    [Fact]
    public void Machine_specific_data_is_local_not_roaming()
    {
        var paths = Paths();

        Assert.Equal(@"C:\Users\Tester\AppData\Local\CapFrameX", paths.DataDirectory);
        Assert.Equal(@"C:\Users\Tester\AppData\Local\CapFrameX\Logs", paths.LogDirectory);
        Assert.Equal(@"C:\Users\Tester\AppData\Local\CapFrameX\run", paths.RuntimeDirectory);
    }

    [Fact]
    public void Nothing_is_portable_by_default()
    {
        Assert.False(Paths().IsPortable);
    }

    [Fact]
    public void Portable_mode_keeps_everything_below_one_root()
    {
        var paths = Paths(@"D:\CapFrameX");

        Assert.True(paths.IsPortable);
        Assert.Equal(@"D:\CapFrameX\Configuration", paths.ConfigurationDirectory);
        Assert.Equal(@"D:\CapFrameX\Captures", paths.CaptureDirectory);
        Assert.Equal(@"D:\CapFrameX\run", paths.RuntimeDirectory);
        Assert.Equal(@"D:\CapFrameX\Logs", paths.LogDirectory);
    }

    [Fact]
    public void Runtime_directory_is_not_shared_with_settings()
    {
        // The session token lives there; it must not end up in a roaming profile that follows the
        // user to another machine.
        var paths = Paths();

        Assert.DoesNotContain("Roaming", paths.RuntimeDirectory);
    }
}
