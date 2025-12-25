using CapFrameX.Service.Capture.Contracts;
using System.Text;

namespace CapFrameX.Service.Capture;

/// <summary>
/// Configuration for PresentMon capture service.
/// </summary>
public sealed class PresentMonServiceConfiguration : IServiceStartInfo
{
    private const string PresentMonExecutable = "PresentMon-2.4.0-x64.exe";
    private static readonly string PresentMonPath = Path.Combine("PresentMon", PresentMonExecutable);

    public string FileName => PresentMonPath;
    public string Arguments { get; private set; } = string.Empty;
    public bool CreateNoWindow { get; init; } = true;
    public bool RunWithAdminRights { get; init; } = true;
    public bool RedirectStandardOutput { get; init; } = true;
    public bool UseShellExecute { get; init; } = false;

    /// <summary>
    /// Whether to redirect output to stdout for streaming.
    /// </summary>
    public bool EnableOutputStream { get; init; } = true;

    /// <summary>
    /// Processes to exclude from capture (e.g., system processes).
    /// </summary>
    public List<string>? ExcludeProcesses { get; init; }

    /// <summary>
    /// Builds command-line arguments based on configuration.
    /// OPTIMIZED: Uses StringBuilder to minimize allocations.
    /// </summary>
    public void BuildArguments()
    {
        if (!EnableOutputStream)
        {
            Arguments = string.Empty;
            return;
        }

        var sb = new StringBuilder(256);

        // Core PresentMon flags
        sb.Append("--restart_as_admin ");           // Ensure admin privileges
        sb.Append("--stop_existing_session ");      // Clean state
        sb.Append("--output_stdout ");              // Redirect to console
        sb.Append("--no_track_input ");             // Reduce overhead
        sb.Append("--qpc_time_ms ");                // Millisecond QPC timestamps
        sb.Append("--track_pc_latency");            // PC latency metrics

        // Exclude system processes
        if (ExcludeProcesses != null && ExcludeProcesses.Count > 0)
        {
            foreach (var process in ExcludeProcesses.Where(p => !p.Contains(' ')))
            {
                sb.Append(" --exclude ");
                sb.Append(process);
                if (!process.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                {
                    sb.Append(".exe");
                }
            }
        }

        Arguments = sb.ToString();
    }

    /// <summary>
    /// Creates a default configuration with common exclusions.
    /// </summary>
    public static PresentMonServiceConfiguration CreateDefault()
    {
        var config = new PresentMonServiceConfiguration
        {
            ExcludeProcesses = new List<string>
            {
                "explorer",
                "dwm",
                "ShellExperienceHost",
                "ApplicationFrameHost",
                "SystemSettings",
                "TextInputHost"
            }
        };

        config.BuildArguments();
        return config;
    }
}
