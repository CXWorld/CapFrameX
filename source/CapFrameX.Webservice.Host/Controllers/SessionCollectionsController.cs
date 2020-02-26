using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CapFrameX.Data.Session.Classes;
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
    public class SessionCollectionsController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly IUserClaimsProvider _userClaimsProvider;

        public SessionCollectionsController(IMediator mediator, IUserClaimsProvider userClaimsProvider)
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
            var query = new GetSessionCollectionByIdQuery()
            {
                Id = id
            };
            var result = await _mediator.Send(query);

            return Ok(result);
        }

        // POST: api/CaptureCollections
        [HttpPost]
        public async Task<IActionResult> Post(IEnumerable<Session> sessions)
        {
            var result = await _mediator.Send(new UploadSessionsCommand() {
                UserId = _userClaimsProvider.GetUserClaims()?.Sub,
                Sessions = sessions
            });
            return CreatedAtAction("Get", new { Id = result } ,result);
        }
    }
}
