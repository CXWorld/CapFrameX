using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using CapFrameX.Webservice.Data.DTO;
using CapFrameX.Webservice.Data.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace CapFrameX.Webservice.Host.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ProcessListController : ControllerBase
    {
        private readonly ISquidexService _squidexService;
        private readonly IMapper _mapper;

        public ProcessListController(ISquidexService squidexService, IMapper mapper)
        {
            _squidexService = squidexService;
            _mapper = mapper;
        }
        [HttpGet]
        public async Task<IActionResult> Get()
        {
            return Ok(_mapper.Map<ProcessListDataDTO[]>(await _squidexService.GetProcessList()));
        }
    }
}
