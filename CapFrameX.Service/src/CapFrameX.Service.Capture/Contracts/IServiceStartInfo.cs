namespace CapFrameX.Service.Capture.Contracts;

/// <summary>
/// Configuration for starting a capture service process.
/// </summary>
public interface IServiceStartInfo
{
    /// <summary>
    /// The executable file name or path to launch.
    /// </summary>
    string FileName { get; }

    /// <summary>
    /// Command-line arguments to pass to the process.
    /// </summary>
    string Arguments { get; }

    /// <summary>
    /// Whether to create the process without a visible window.
    /// </summary>
    bool CreateNoWindow { get; }

    /// <summary>
    /// Whether to run the process with administrator privileges.
    /// </summary>
    bool RunWithAdminRights { get; }

    /// <summary>
    /// Whether to redirect standard output for stream processing.
    /// </summary>
    bool RedirectStandardOutput { get; }

    /// <summary>
    /// Whether to use the operating system shell to start the process.
    /// </summary>
    bool UseShellExecute { get; }
}
