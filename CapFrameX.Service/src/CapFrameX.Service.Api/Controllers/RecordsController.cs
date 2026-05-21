using CapFrameX.Service.Contracts.Records;
using Microsoft.AspNetCore.Mvc;

namespace CapFrameX.Service.Api.Controllers;

[ApiController]
[Route("api/records")]
public sealed class RecordsController : ControllerBase
{
    [HttpGet]
    public ActionResult<RecordsListResponse> List()
    {
        return Ok(new RecordsListResponse(Array.Empty<RecordSummaryDto>()));
    }
}
