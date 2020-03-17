using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using CapFrameX.Webservice.Data.Commands;
using CapFrameX.Webservice.Data.DTO;
using CapFrameX.Webservice.Data.Interfaces;
using CapFrameX.Webservice.Data.Queries;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;

namespace CapFrameX.Webservice.Host.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ProcessListController : ControllerBase
    {
        private readonly IMediator _mediator;

        public ProcessListController(IMediator mediator)
        {
            _mediator = mediator;
        }
        [HttpGet]
        public async Task<IActionResult> Get()
        {
            var list = await _mediator.Send(new GetProcessListQuery());

            return new JsonResult(list, new JsonSerializerSettings
            {
                Formatting = Formatting.Indented,
            });
        }

        public async Task<IActionResult> Post(ProcessListDataDTO process)
        {
            await _mediator.Send(new AddProcessCommand() { 
                Process = process
            });
            return NoContent();
        }
    }
}
