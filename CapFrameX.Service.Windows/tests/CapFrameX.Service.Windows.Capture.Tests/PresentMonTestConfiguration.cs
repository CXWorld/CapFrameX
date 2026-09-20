using CapFrameX.Service.Capture.Contracts;
using System.Text;

namespace CapFrameX.Service.Capture.Tests;

/// <summary>
/// Configuration helper for PresentMon tests, matching legacy behavior.
/// </summary>
internal static class PresentMonTestConfiguration
{
    private const string PresentMonAppName = "PresentMon-2.4.0-x64";

    /// <summary>
    /// Gets common blacklisted process names that should be excluded from capture.
    /// Based on legacy ProcessList default ignore list.
    /// </summary>
    public static HashSet<string> GetDefaultIgnoreList()
    {
        return new HashSet<string>
        {
            // System processes
            "dwm",
            "explorer",
            "taskmgr",
            "svchost",
            "System",
            "Registry",
            "smss",
            "csrss",
            "wininit",
            "services",
            "lsass",
            "winlogon",

            // Common applications
            "Discord",
            "Spotify",
            "Chrome",
            "Firefox",
            "msedge",
            "Code",
            "devenv",
            "steam",
            "EpicGamesLauncher",
            "Battle.net",
            "Origin",

            // Monitoring/utilities
            "HWiNFO64",
            "MSIAfterburner",
            "RTSS",
            "RivaTuner",
            "OBS64",
            "obs-browser-page",
            "steamwebhelper",

            // CapFrameX itself
            "CapFrameX"
        };
    }

    /// <summary>
    /// Creates PresentMon start info for redirected output (streaming mode).
    /// Matches legacy GetRedirectedServiceConfig configuration.
    /// </summary>
    public static IServiceStartInfo CreateRedirectedStartInfo(List<string>? excludeProcesses = null)
    {
        var arguments = BuildRedirectedArguments(excludeProcesses);

        return new ServiceStartInfo
        {
            FileName = $"PresentMon\\{PresentMonAppName}.exe",
            Arguments = arguments,
            CreateNoWindow = true,
            RunWithAdminRights = true,
            RedirectStandardOutput = false,
            UseShellExecute = false
        };
    }

    /// <summary>
    /// Builds PresentMon arguments for redirected output mode.
    /// Matches legacy PresentMonServiceConfiguration.ConfigParameterToArguments().
    /// </summary>
    private static string BuildRedirectedArguments(List<string>? excludeProcesses)
    {
        var sb = new StringBuilder();

        // Core parameters
        sb.Append("--restart_as_admin");
        sb.Append(" --stop_existing_session");
        sb.Append(" --output_stdout");
        sb.Append(" --no_track_input");
        sb.Append(" --qpc_time_ms");
        sb.Append(" --track_pc_latency");

        // Exclude processes
        if (excludeProcesses != null && excludeProcesses.Any())
        {
            foreach (var process in excludeProcesses.Where(p => !p.Contains(" ")))
            {
                sb.Append(" --exclude ");
                sb.Append(process.EndsWith(".exe") ? process : process + ".exe");
            }
        }

        return sb.ToString();
    }

    private class ServiceStartInfo : IServiceStartInfo
    {
        public string FileName { get; set; } = string.Empty;
        public string Arguments { get; set; } = string.Empty;
        public bool CreateNoWindow { get; set; }
        public bool RunWithAdminRights { get; set; }
        public bool RedirectStandardOutput { get; set; }
        public bool UseShellExecute { get; set; }
    }
}
