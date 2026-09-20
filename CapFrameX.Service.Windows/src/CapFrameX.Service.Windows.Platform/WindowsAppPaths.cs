using System.Runtime.Versioning;
using CapFrameX.Service.Core.Platform;

namespace CapFrameX.Service.Windows.Platform;

/// <summary>
/// Where the Windows service keeps its files, matching the locations CapFrameX 1.x has always used
/// so both generations see the same settings and captures.
/// </summary>
/// <remarks>
/// The known folders are read through a delegate rather than called directly, so the layout rules
/// - which folder wins, what portable mode changes - are verifiable without a particular machine's
/// profile.
/// </remarks>
[SupportedOSPlatform("windows")]
public sealed class WindowsAppPaths : IAppPaths
{
    private const string ApplicationDirectoryName = "CapFrameX";

    /// <summary>Creates the path set.</summary>
    /// <param name="folder">Resolves a known folder; defaults to the current user's.</param>
    /// <param name="portableRoot">
    /// When set, everything lives below this directory and <see cref="IsPortable"/> is true.
    /// </param>
    public WindowsAppPaths(Func<Environment.SpecialFolder, string>? folder = null, string? portableRoot = null)
    {
        var resolve = folder ?? Environment.GetFolderPath;

        IsPortable = portableRoot is not null;

        if (portableRoot is not null)
        {
            ConfigurationDirectory = Path.Combine(portableRoot, "Configuration");
            DataDirectory = portableRoot;
            CaptureDirectory = Path.Combine(portableRoot, "Captures");
            LogDirectory = Path.Combine(portableRoot, "Logs");
            RuntimeDirectory = Path.Combine(portableRoot, "run");
            return;
        }

        var roaming = Path.Combine(resolve(Environment.SpecialFolder.ApplicationData), ApplicationDirectoryName);
        var local = Path.Combine(resolve(Environment.SpecialFolder.LocalApplicationData), ApplicationDirectoryName);

        // Settings are roaming because 1.x put them there; everything machine-specific is local.
        ConfigurationDirectory = Path.Combine(roaming, "Configuration");
        DataDirectory = local;
        LogDirectory = Path.Combine(local, "Logs");
        RuntimeDirectory = Path.Combine(local, "run");
        CaptureDirectory = Path.Combine(
            resolve(Environment.SpecialFolder.MyDocuments),
            ApplicationDirectoryName,
            "Captures");
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
}
