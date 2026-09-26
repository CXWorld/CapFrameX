namespace LibreHardwareMonitor.PawnIo;

/// <summary>
/// What the service control manager reports for the PawnIO driver.
/// </summary>
public enum PawnIoDriverState
{
    /// <summary>
    /// The service could not be queried, or it is in a transitional state.
    /// </summary>
    Unknown,

    /// <summary>
    /// No PawnIO service is registered.
    /// </summary>
    NotInstalled,

    /// <summary>
    /// The driver is loaded.
    /// </summary>
    Running,

    /// <summary>
    /// The driver is registered but not loaded.
    /// </summary>
    Stopped,

    /// <summary>
    /// The last start failed because Windows code integrity refused the driver image (Win32 error 577),
    /// e.g. a test-signed build while Secure Boot is enabled.
    /// </summary>
    Blocked
}

/// <summary>
/// State of the PawnIO service and the version of the driver image it points at.
/// </summary>
public sealed class PawnIoDriverStatus
{
    /// <summary>
    /// Creates a status snapshot.
    /// </summary>
    /// <param name="state">What the service control manager reports.</param>
    /// <param name="version">File version of the registered driver image; null when unknown.</param>
    public PawnIoDriverStatus(PawnIoDriverState state, string version)
    {
        State = state;
        Version = version;
    }

    /// <summary>
    /// Gets what the service control manager reports for the driver.
    /// </summary>
    public PawnIoDriverState State { get; }

    /// <summary>
    /// Gets the file version of the driver image the service points at - the one Windows actually
    /// loads, which need not be the version the PawnIO installer registered. Null when unknown.
    /// </summary>
    public string Version { get; }
}
