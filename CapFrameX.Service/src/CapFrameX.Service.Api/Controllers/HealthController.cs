using Microsoft.AspNetCore.Mvc;
using CapFrameX.Service.Contracts.App;

namespace CapFrameX.Service.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class HealthController : ControllerBase
{
    [HttpGet]
    public ActionResult<ServiceHealthDto> Get()
    {
        return Ok(new ServiceHealthDto(
            "Healthy",
            "CapFrameX.Service",
            DateTimeOffset.UtcNow));
    }
}
