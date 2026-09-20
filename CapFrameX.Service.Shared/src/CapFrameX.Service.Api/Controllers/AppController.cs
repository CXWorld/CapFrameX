using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using CapFrameX.Service.Contracts.App;
using Microsoft.AspNetCore.Mvc;

namespace CapFrameX.Service.Api.Controllers;

[ApiController]
[Route("api/app")]
public sealed class AppController : ControllerBase
{
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
