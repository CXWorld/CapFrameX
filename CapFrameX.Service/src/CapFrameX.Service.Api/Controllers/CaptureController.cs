using CapFrameX.Service.Contracts.Capture;
using Microsoft.AspNetCore.Mvc;

namespace CapFrameX.Service.Api.Controllers;

[ApiController]
[Route("api/capture")]
public sealed class CaptureController : ControllerBase
{
    [HttpGet("status")]
    public ActionResult<CaptureStatusDto> GetStatus()
    {
        var isWindows = OperatingSystem.IsWindows();

        return Ok(new CaptureStatusDto(
            "idle",
            isWindows ? "presentmon" : null,
            false,
            null,
            null,
            isWindows
                ? "PresentMon is not registered in the platform-neutral API host yet."
                : "No Linux capture provider is registered yet."));
    }
}
