using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CapFrameX.Data.Session.Classes;
using CapFrameX.Webservice.Data.Commands;
using CapFrameX.Webservice.Data.DTO;
using CapFrameX.Webservice.Data.Exceptions;
using CapFrameX.Webservice.Data.Extensions;
using CapFrameX.Webservice.Data.Interfaces;
using CapFrameX.Webservice.Data.Queries;
using CapFrameX.Webservice.Host.Attributes;
using MediatR;
using Microsoft.AspNetCore.Authorization;
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
		public async Task<IActionResult> GetUserCollections([FromQuery] Guid? sub)
		{
			try
			{
				var result = await _mediator.Send(new GetSessionCollectionsReducedForUserByIdQuery()
				{
					UserId = sub ?? _userClaimsProvider.GetUserClaims().Sub
				});
				return Ok(result);
			} catch(SessionCollectionNotFoundException)
			{
				return NotFound();
			}
		}

		[HttpGet("{id}", Name = "Get")]
		public async Task<IActionResult> Get(Guid id)
		{
			if(!Request.HasCXClientHeader())
			{
				return Redirect($"https://capframex.com/sessioncollections/{id}");
			}
			var query = new GetSessionCollectionByIdQuery()
			{
				Id = id
			};
			try
			{
				var result = await _mediator.Send(query);
				return Ok(result);
			}
			catch (SessionCollectionNotFoundException)
			{
				return NotFound();
			}
		}

		[HttpGet("{id}/reduced", Name = "GetReduced")]
		public async Task<IActionResult> GetReduced(Guid id)
		{
			var query = new GetSessionCollectionReducedByIdQuery()
			{
				Id = id
			};
			return Ok(await _mediator.Send(query));
		}

		[HttpPost]
		[ServiceFilter(typeof(UserAgentFilter))]
		public async Task<IActionResult> Post(IEnumerable<Session> sessions, [FromQuery] string description)
		{
			var result = await _mediator.Send(new UploadSessionsCommand()
			{
				UserId = _userClaimsProvider.GetUserClaims()?.Sub,
				Sessions = sessions,
				Description = description
			});
			return CreatedAtAction(nameof(Get), new { Id = result }, result);
		}

		[Authorize]
		[HttpDelete("{id}", Name = "Delete")]
		public async Task<IActionResult> Delete(Guid id)
		{
			try
			{
				await _mediator.Send(new DeleteSessionCollectionByIdCommand()
				{
					Id = id,
					UserId = _userClaimsProvider.GetUserClaims()?.Sub
				});

				return NoContent();
			}
			catch (UnauthorizedAccessException)
			{
				return Forbid();
			}

		}
	}
}
