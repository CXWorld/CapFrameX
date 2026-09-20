using System.Runtime.Versioning;
using System.Security.Principal;

namespace CapFrameX.Service.Capture.Tests;

/// <summary>
/// A test that can only run with administrator rights, because PresentMon opens a real-time ETW
/// session and the kernel refuses that to a normal user.
/// </summary>
/// <remarks>
/// Without this the whole capture suite reports red on any developer machine that is not elevated,
/// which trains everyone to ignore its result. Reporting "skipped" keeps the distinction between
/// "this is broken" and "this was not tried" - but it also means a green run is no evidence that
/// capture works; for that the suite has to be started from an elevated shell.
/// </remarks>
[SupportedOSPlatform("windows")]
public sealed class RequiresElevationFactAttribute : FactAttribute
{
    private static readonly bool IsElevated = DetermineElevation();

    /// <summary>Creates the attribute and skips the test when the process is not elevated.</summary>
    public RequiresElevationFactAttribute()
    {
        if (!IsElevated)
        {
            Skip = "Needs administrator rights: PresentMon opens a real-time ETW session.";
        }
    }

    private static bool DetermineElevation()
    {
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch (Exception)
        {
            // If the token cannot be read the test cannot be trusted to run either.
            return false;
        }
    }
}
