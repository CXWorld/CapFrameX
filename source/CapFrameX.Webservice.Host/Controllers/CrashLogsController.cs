using CapFrameX.Webservice.Data.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CapFrameX.Webservice.Data.Extensions;
using Squidex.ClientLibrary;
using Serilog.Formatting.Json;

namespace CapFrameX.Webservice.Host.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CrashLogsController : ControllerBase
    {
        private readonly ICrashlogReportingService _crashlogReportingService;

        public CrashLogsController(ICrashlogReportingService crashlogReportingService)
        {
            _crashlogReportingService = crashlogReportingService;
        }

        [HttpPost]
        public async Task<IActionResult> Post(JToken json)
        {
            var fileBytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(json, Formatting.Indented));

            var assetId = await _crashlogReportingService.UploadCrashlog(fileBytes.Compress(), $"crashlog_{DateTime.UtcNow}.json.gz");
            return Ok(assetId);
        }
    }
}
