using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CapFrameX.Webservice.Data.Commands;
using CapFrameX.Webservice.Data.DTO;
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

        // GET: api/Captures/Collection/5
        [HttpGet("Collection/{id}", Name = "Get")]
        public async Task<IActionResult> Get(Guid id)
        {
            var query = new GetCaptureCollectionByIdQuery()
            {
                Id = id
            };
            var result = await _mediator.Send(query);

            return Ok(result);
        }

        // POST: api/Captures
        [HttpPost]
        public async Task<IActionResult> Post([FromForm] UploadCapturesForm form)
        {
            var blobBytes = new List<Capture>();
            using(var memoryStream = new MemoryStream())
            {
                foreach(var capture in form.Capture)
                {
                    await capture.CopyToAsync(memoryStream);
                    blobBytes.Add(new Capture() { 
                        Name = capture.FileName,
                        BlobBytes = memoryStream.ToArray()
                    });
                    await memoryStream.FlushAsync();
                }
            }
            var query = new UploadCapturesCommand()
            {
                AppVersion = form.AppVersion,
                CaptureFiles = blobBytes
            };

            var result = await _mediator.Send(query);
            return CreatedAtAction("Get", new { Id = result.Id } ,result);
        }
    }
}
