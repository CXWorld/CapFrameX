namespace CapFrameX.Service.Core.Platform;

/// <summary>
/// Every directory the service writes to. Windows uses the known folders it always has, Linux the
/// XDG base directories; shared code never builds a path itself and never compares paths for case.
/// </summary>
public interface IAppPaths
{
    /// <summary>Settings and profiles.</summary>
    string ConfigurationDirectory { get; }

    /// <summary>Database and other service-owned data.</summary>
    string DataDirectory { get; }

    /// <summary>Capture records. The user can point this somewhere else.</summary>
    string CaptureDirectory { get; }

    /// <summary>Log files.</summary>
    string LogDirectory { get; }

    /// <summary>Short-lived run-time state such as the session token; cleared on reboot.</summary>
    string RuntimeDirectory { get; }

    /// <summary>Whether the service runs in portable mode, keeping everything next to the binaries.</summary>
    bool IsPortable { get; }
}
