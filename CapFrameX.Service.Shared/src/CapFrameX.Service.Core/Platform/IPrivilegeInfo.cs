namespace CapFrameX.Service.Core.Platform;

/// <summary>
/// What the service process is allowed to do. The Windows service always runs elevated and fails
/// fast when it does not; the Linux service runs unprivileged and reports which privileged helpers
/// it found.
/// </summary>
public interface IPrivilegeInfo
{
    /// <summary>Whether the process has the elevated rights its platform expects.</summary>
    bool IsElevated { get; }

    /// <summary>
    /// Whether privileged hardware access is usable - the PawnIO driver on Windows, the telemetry
    /// helper on Linux - with a reason when it is not.
    /// </summary>
    PlatformAvailability LowLevelHardwareAccess { get; }
}
