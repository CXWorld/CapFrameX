using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CapFrameX.Webservice.Data.Queries;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace CapFrameX.Webservice.Host.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CapturesController : ControllerBase
    {
        private readonly IMediator _mediator;

        public CapturesController(IMediator mediator)
        {
            _mediator = mediator;
        }

        // GET: api/Captures/5
        [HttpGet("{id}", Name = "Get")]
        public async Task<IActionResult> Get(Guid id)
        {
            var query = new GetCaptureByIdQuery()
            {
                Id = id
            };
            var result = await _mediator.Send(query);

            return Ok(result);
        }

        // POST: api/Captures
        [HttpPost]
        public void Post([FromBody] string value)
        {
        }
    }
}
