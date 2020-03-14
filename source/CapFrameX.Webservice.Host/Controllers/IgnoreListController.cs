using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CapFrameX.Webservice.Data.Queries;
using CapFrameX.Webservice.Host.Attributes;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace CapFrameX.Webservice.Host.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class IgnoreListController : ControllerBase
    {
        private readonly IMediator _mediator;

        public IgnoreListController(IMediator mediator) {
            _mediator = mediator;
        }
        [HttpGet]
        [ServiceFilter(typeof(UserAgentFilter))]
        public async Task<IActionResult> Get()
        {
            return Ok(await _mediator.Send(new GetIgnoreListQuery()));
        }
    }
}
