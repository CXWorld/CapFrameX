using CapFrameX.Webservice.Data.Queries;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CapFrameX.Webservice.Host.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class SessionsController: ControllerBase
    {
        private readonly IMediator _mediator;

        public SessionsController(IMediator mediator)
        {
            _mediator = mediator;
        }

		[HttpGet]
		public async Task<IActionResult> GetUserCollections([FromQuery] string cpu, [FromQuery] string gpu, [FromQuery] string mainboard, [FromQuery] string ram, [FromQuery] string gameName, [FromQuery] string comment)
		{
			try
			{
				var result = await _mediator.Send(new SearchSessionsQuery()
				{
					Cpu = cpu,
					Gpu = gpu,
					Mainbaord = mainboard,
					GameName = gameName,
					Ram = ram,
					Comment = comment
				});
				return Ok(result);
			}
			catch (Exception e)
			{
				return BadRequest();
			}
		}
	}
}
