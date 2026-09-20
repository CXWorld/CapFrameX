using CapFrameX.Service.Core.Platform;

namespace CapFrameX.Service.Linux.Platform;

/// <summary>
/// Directory layout of the Linux service, following the XDG base directory specification.
/// </summary>
/// <remarks>
/// The lookup is written against an environment reader rather than against
/// <see cref="Environment"/> directly, so the rules - which variable wins, what the fallback is,
/// when a relative value has to be ignored - are verifiable on any machine instead of only on a
/// Linux box with the right variables set.
/// </remarks>
public sealed class XdgAppPaths : IAppPaths
{
    private const string ApplicationDirectoryName = "capframex";

    /// <summary>Creates the path set.</summary>
    /// <param name="environment">
    /// Reads environment variables; defaults to the process environment.
    /// </param>
    /// <param name="homeDirectory">
    /// Home directory to fall back to; defaults to the current user's home.
    /// </param>
    /// <param name="portableRoot">
    /// When set, everything lives below this directory and <see cref="IsPortable"/> is true.
    /// </param>
    public XdgAppPaths(
        Func<string, string?>? environment = null,
        string? homeDirectory = null,
        string? portableRoot = null)
    {
        var read = environment ?? System.Environment.GetEnvironmentVariable;
        var home = Absolute(homeDirectory) ??
                   Absolute(read("HOME")) ??
                   System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile);

        IsPortable = portableRoot is not null;

        if (portableRoot is not null)
        {
            ConfigurationDirectory = Path.Combine(portableRoot, "Configuration");
            DataDirectory = Path.Combine(portableRoot, "Data");
            CaptureDirectory = Path.Combine(portableRoot, "Captures");
            LogDirectory = Path.Combine(portableRoot, "Logs");
            RuntimeDirectory = Path.Combine(portableRoot, "Run");
            return;
        }

        ConfigurationDirectory = Application(read, "XDG_CONFIG_HOME", home, ".config");
        DataDirectory = Application(read, "XDG_DATA_HOME", home, Path.Combine(".local", "share"));
        LogDirectory = Application(read, "XDG_STATE_HOME", home, Path.Combine(".local", "state"));
        CaptureDirectory = Path.Combine(DataDirectory, "Captures");

        // XDG_RUNTIME_DIR is the only one with no defined fallback; the state directory is the
        // closest thing that is guaranteed to be writable.
        RuntimeDirectory = Absolute(read("XDG_RUNTIME_DIR")) is { } runtime
            ? Path.Combine(runtime, ApplicationDirectoryName)
            : LogDirectory;
    }

    /// <inheritdoc />
    public string ConfigurationDirectory { get; }

    /// <inheritdoc />
    public string DataDirectory { get; }

    /// <inheritdoc />
    public string CaptureDirectory { get; }

    /// <inheritdoc />
    public string LogDirectory { get; }

    /// <inheritdoc />
    public string RuntimeDirectory { get; }

    /// <inheritdoc />
    public bool IsPortable { get; }

    private static string Application(Func<string, string?> read, string variable, string home, string fallback) =>
        Path.Combine(Absolute(read(variable)) ?? Path.Combine(home, fallback), ApplicationDirectoryName);

    /// <summary>
    /// The specification says a relative value has to be treated as unset, which matters because a
    /// relative path would otherwise resolve against the service's working directory.
    /// </summary>
    private static string? Absolute(string? value) =>
        !string.IsNullOrWhiteSpace(value) && Path.IsPathRooted(value) ? value : null;
}
