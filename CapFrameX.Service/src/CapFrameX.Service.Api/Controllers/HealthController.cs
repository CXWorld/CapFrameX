using Microsoft.AspNetCore.Mvc;

namespace CapFrameX.Service.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class HealthController : ControllerBase
{
    [HttpGet]
    public IActionResult Get()
    {
        return Ok(new
        {
            Status = "Healthy",
            Service = "CapFrameX.Service",
            Timestamp = DateTime.UtcNow
        });
    }
}
