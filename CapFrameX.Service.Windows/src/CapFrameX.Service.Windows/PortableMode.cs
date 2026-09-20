namespace CapFrameX.Service.Windows.Host;

/// <summary>
/// Portable mode: everything the service writes stays next to its binaries instead of in the
/// user's profile, so the installation can live on a stick.
/// </summary>
public static class PortableMode
{
    /// <summary>File whose presence next to the executable switches portable mode on.</summary>
    public const string MarkerFileName = "portable.json";

    /// <summary>
    /// The directory everything lives under in portable mode, or <c>null</c> for a normal install.
    /// </summary>
    public static string? RootOrNull()
    {
        var directory = AppContext.BaseDirectory;

        return File.Exists(Path.Combine(directory, MarkerFileName)) ? directory : null;
    }
}
