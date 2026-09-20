namespace CapFrameX.Service.Windows.Host;

/// <summary>
/// Exit codes the frontend interprets when the service fails to come up.
/// </summary>
/// <remarks>
/// The frontend cannot ask a process that already exited what went wrong, so the code is the whole
/// message: each one maps to a repair action it can offer instead of "the service did not start".
/// </remarks>
public static class ServiceExitCode
{
    /// <summary>The service stopped normally.</summary>
    public const int Ok = 0;

    /// <summary>
    /// The process is not elevated. PresentMon and PawnIO cannot work, so it stops instead of
    /// serving half an API; the frontend offers to repair the scheduled task.
    /// </summary>
    public const int NotElevated = 2;

    /// <summary>The loopback port is taken, most likely by another CapFrameX service.</summary>
    public const int PortInUse = 3;
}
