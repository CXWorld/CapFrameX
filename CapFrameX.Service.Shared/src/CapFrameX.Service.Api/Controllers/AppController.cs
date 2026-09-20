using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using CapFrameX.Service.Contracts.App;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;

namespace CapFrameX.Service.Api.Controllers;

[ApiController]
[Route("api/app")]
public sealed class AppController : ControllerBase
{
    /// <summary>
    /// Asks the service to stop.
    /// </summary>
    /// <remarks>
    /// The service outlives the frontend on purpose - capture and overlay keep running with the
    /// window closed - so closing the window must not stop it. This is the deliberate "Exit", and
    /// it goes through the generic host's graceful stop so PresentMon, the overlay and the session
    /// token are released in order rather than left behind by a killed process.
    /// </remarks>
    /// <param name="lifetime">The host's lifetime.</param>
    [HttpPost("shutdown")]
    public IActionResult Shutdown([FromServices] IHostApplicationLifetime lifetime)
    {
        // Answer first, stop afterwards: a caller that gets no response cannot tell a shutdown
        // from a crash.
        lifetime.StopApplication();

        return Accepted();
    }

    [HttpGet("version")]
    public ActionResult<AppVersionDto> GetVersion()
    {
        var assembly = typeof(AppController).Assembly;
        var assemblyName = assembly.GetName();
        var informationalVersion = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion ?? assemblyName.Version?.ToString() ?? "0.0.0";
        var targetFramework = assembly
            .GetCustomAttribute<TargetFrameworkAttribute>()
            ?.FrameworkName ?? "unknown";

        return Ok(new AppVersionDto(
            assemblyName.Name ?? "CapFrameX.Service.Api",
            assemblyName.Version?.ToString() ?? "0.0.0",
            informationalVersion,
            targetFramework,
            RuntimeInformation.ProcessArchitecture.ToString(),
            GetPlatform()));
    }

    private static string GetPlatform()
    {
        if (OperatingSystem.IsWindows())
        {
            return "windows";
        }

        if (OperatingSystem.IsLinux())
        {
            return "linux";
        }

        if (OperatingSystem.IsMacOS())
        {
            return "macos";
        }

        return "unknown";
    }
}
