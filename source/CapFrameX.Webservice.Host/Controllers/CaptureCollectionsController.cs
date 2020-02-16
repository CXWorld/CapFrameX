using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CapFrameX.Webservice.Data.Commands;
using CapFrameX.Webservice.Data.DTO;
using CapFrameX.Webservice.Data.Interfaces;
using CapFrameX.Webservice.Data.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace CapFrameX.Webservice.Host.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CaptureCollectionsController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly IUserClaimsProvider _userClaimsProvider;

        public CaptureCollectionsController(IMediator mediator, IUserClaimsProvider userClaimsProvider)
        {
            _mediator = mediator;
            _userClaimsProvider = userClaimsProvider;
        }

        [HttpGet]
        public async Task<IActionResult> GetMyCollections ([FromQuery] Guid? sub)
        {
                var userId = sub ?? _userClaimsProvider.GetUserClaims().Sub;
                return Ok(new List<dynamic>() { 
                    new {  
                        DateUploaded = DateTime.UtcNow,
                        Size = "4522Kb",
                        Name = "Test dummy"
                    }
                });
        }

        // GET: api/CaptureCollections/Collection/5
        [HttpGet("{id}", Name = "Get")]
        public async Task<IActionResult> Get(Guid id)
        {
            var query = new GetCaptureCollectionByIdQuery()
            {
                Id = id
            };
            var result = await _mediator.Send(query);

            return Ok(result);
        }

        // POST: api/CaptureCollections
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
                    memoryStream.SetLength(0);
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
