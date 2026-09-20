using System.Runtime.InteropServices;
using CapFrameX.Service.Contracts.Capabilities;
using Microsoft.AspNetCore.Mvc;

namespace CapFrameX.Service.Api.Controllers;

[ApiController]
[Route("api/capabilities")]
public sealed class CapabilitiesController : ControllerBase
{
    [HttpGet]
    public ActionResult<CapabilitiesResponse> Get()
    {
        var isWindows = OperatingSystem.IsWindows();
        var isLinux = OperatingSystem.IsLinux();

        var capabilities = new List<CapabilityDto>
        {
            new("bridge.http", "HTTP API", CapabilityStates.Available, "frontend"),
            new("bridge.events.sse", "Server-sent bridge events", CapabilityStates.Available, "frontend"),
            new(
                "capture.presentmon",
                "PresentMon capture provider",
                isWindows ? CapabilityStates.Planned : CapabilityStates.Unavailable,
                "capture",
                isWindows
                    ? "Provider assembly is intentionally not registered by the platform-neutral API yet."
                    : "PresentMon is a Windows provider."),
            new(
                "monitoring.pawnio",
                "PawnIO hardware access",
                isWindows ? CapabilityStates.Planned : CapabilityStates.Unavailable,
                "monitoring",
                isWindows
                    ? "PawnIO remains a Windows provider and is not loaded by the platform-neutral API."
                    : "PawnIO is not available on Linux."),
            new(
                "overlay.rtss",
                "RTSS overlay output",
                isWindows ? CapabilityStates.Planned : CapabilityStates.Unavailable,
                "overlay",
                isWindows
                    ? "Overlay integration remains a Windows provider and is not part of the frontend bridge."
                    : "RTSS overlay output is Windows-only."),
            new(
                "capture.vulkanLayer.linux",
                "Linux Vulkan layer capture provider",
                isLinux ? CapabilityStates.Planned : CapabilityStates.Unavailable,
                "capture",
                isLinux
                    ? "The capframex-linux Vulkan provider still needs to be wired into the service."
                    : "Linux Vulkan layer capture is only relevant on Linux.")
        };

        return Ok(new CapabilitiesResponse(
            GetPlatform(),
            RuntimeInformation.OSDescription,
            RuntimeInformation.ProcessArchitecture.ToString(),
            capabilities));
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
