using CapFrameX.Service.Linux.Platform;

namespace CapFrameX.Service.Linux.Tests.Platform;

/// <summary>
/// The XDG rules decide where a Linux install keeps settings, captures and the session token, so
/// they are verified here rather than discovered on a user's machine.
/// </summary>
public sealed class XdgAppPathsTests
{
    private static Func<string, string?> Environment(params (string Key, string Value)[] values)
    {
        var map = values.ToDictionary(v => v.Key, v => v.Value, StringComparer.Ordinal);
        return key => map.TryGetValue(key, out var value) ? value : null;
    }

    [Fact]
    public void Falls_back_to_the_home_directory_when_nothing_is_set()
    {
        var paths = new XdgAppPaths(Environment(), homeDirectory: "/home/tester");

        Assert.Equal(Path.Combine("/home/tester", ".config", "capframex"), paths.ConfigurationDirectory);
        Assert.Equal(Path.Combine("/home/tester", ".local", "share", "capframex"), paths.DataDirectory);
        Assert.Equal(Path.Combine("/home/tester", ".local", "state", "capframex"), paths.LogDirectory);
        Assert.False(paths.IsPortable);
    }

    [Fact]
    public void Xdg_variables_win_over_the_fallback()
    {
        var paths = new XdgAppPaths(
            Environment(("XDG_CONFIG_HOME", "/cfg"), ("XDG_DATA_HOME", "/data"), ("XDG_STATE_HOME", "/state")),
            homeDirectory: "/home/tester");

        Assert.Equal(Path.Combine("/cfg", "capframex"), paths.ConfigurationDirectory);
        Assert.Equal(Path.Combine("/data", "capframex"), paths.DataDirectory);
        Assert.Equal(Path.Combine("/state", "capframex"), paths.LogDirectory);
    }

    [Fact]
    public void Captures_live_below_the_data_directory()
    {
        var paths = new XdgAppPaths(Environment(("XDG_DATA_HOME", "/data")), homeDirectory: "/home/tester");

        Assert.Equal(Path.Combine("/data", "capframex", "Captures"), paths.CaptureDirectory);
    }

    [Fact]
    public void Runtime_directory_is_used_for_short_lived_state()
    {
        var paths = new XdgAppPaths(
            Environment(("XDG_RUNTIME_DIR", "/run/user/1000")),
            homeDirectory: "/home/tester");

        Assert.Equal(Path.Combine("/run/user/1000", "capframex"), paths.RuntimeDirectory);
    }

    [Fact]
    public void Without_a_runtime_directory_the_state_directory_stands_in()
    {
        var paths = new XdgAppPaths(Environment(), homeDirectory: "/home/tester");

        Assert.Equal(paths.LogDirectory, paths.RuntimeDirectory);
    }

    [Theory]
    [InlineData("relative/path")]
    [InlineData("")]
    [InlineData("   ")]
    public void Value_that_is_not_an_absolute_path_counts_as_unset(string value)
    {
        var paths = new XdgAppPaths(Environment(("XDG_CONFIG_HOME", value)), homeDirectory: "/home/tester");

        Assert.Equal(Path.Combine("/home/tester", ".config", "capframex"), paths.ConfigurationDirectory);
    }

    [Fact]
    public void Home_is_taken_from_the_environment_when_not_supplied()
    {
        var paths = new XdgAppPaths(Environment(("HOME", "/home/from-env")));

        Assert.Equal(Path.Combine("/home/from-env", ".config", "capframex"), paths.ConfigurationDirectory);
    }

    [Fact]
    public void Portable_mode_keeps_everything_below_one_root()
    {
        var paths = new XdgAppPaths(
            Environment(("XDG_CONFIG_HOME", "/cfg")),
            homeDirectory: "/home/tester",
            portableRoot: "/opt/capframex");

        Assert.True(paths.IsPortable);
        Assert.Equal(Path.Combine("/opt/capframex", "Configuration"), paths.ConfigurationDirectory);
        Assert.Equal(Path.Combine("/opt/capframex", "Captures"), paths.CaptureDirectory);
        Assert.Equal(Path.Combine("/opt/capframex", "Run"), paths.RuntimeDirectory);
    }
}
