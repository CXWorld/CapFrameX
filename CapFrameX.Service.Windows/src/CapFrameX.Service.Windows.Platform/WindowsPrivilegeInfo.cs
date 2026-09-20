using System.Runtime.Versioning;
using System.Security.Principal;
using CapFrameX.Service.Core.Platform;

namespace CapFrameX.Service.Windows.Platform;

/// <summary>
/// What the Windows service process is allowed to do.
/// </summary>
/// <remarks>
/// The Windows service always runs elevated: PresentMon needs a real-time ETW session and PawnIO
/// needs its device. There is no degraded mode, so a process that finds itself unelevated stops
/// rather than serving half an API.
/// </remarks>
[SupportedOSPlatform("windows")]
public sealed class WindowsPrivilegeInfo : IPrivilegeInfo
{
    /// <inheritdoc />
    public bool IsElevated { get; } = DetermineElevation();

    /// <inheritdoc />
    public PlatformAvailability LowLevelHardwareAccess =>
        IsElevated
            ? PlatformAvailability.Unavailable(
                "The PawnIO driver has not been probed yet; sensors that need it are unavailable until then.")
            : PlatformAvailability.Unavailable("The service is not running with administrator rights.");

    private static bool DetermineElevation()
    {
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch (Exception)
        {
            // A token that cannot be read is not a token that can be trusted.
            return false;
        }
    }
}
