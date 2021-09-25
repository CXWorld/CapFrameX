using CapFrameX.Webservice.Data.Queries;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace CapFrameX.Webservice.Host.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AppNotificationController: ControllerBase
    {
        private readonly IMediator _mediator;

        public AppNotificationController(IMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpGet]
        public async Task<IActionResult> Get()
        {
            var result = await _mediator.Send(new GetAppNotificationQuery());
            return Ok(result);
        }
    }
}
