using AutoMapper;
using CapFrameX.Data.Session.Classes;
using CapFrameX.Webservice.Data;
using CapFrameX.Webservice.Data.DTO;
using CapFrameX.Webservice.Data.Exceptions;
using CapFrameX.Webservice.Data.Interfaces;
using CapFrameX.Webservice.Data.Queries;
using FluentValidation;
using MediatR;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CapFrameX.Webservice.Implementation.Handlers
{
	public class GetSessionCollectionByIdHandler : IRequestHandler<GetSessionCollectionByIdQuery, SessionCollectionDTO>
	{
		private readonly IValidator<GetSessionCollectionByIdQuery> _validator;
		private readonly ISessionService _capturesService;
		private readonly IMapper _mapper;

		public GetSessionCollectionByIdHandler(IValidator<GetSessionCollectionByIdQuery> validator, ISessionService capturesService, IMapper mapper)
		{
			_validator = validator;
			_capturesService = capturesService;
			_mapper = mapper;
		}
		public async Task<SessionCollectionDTO> Handle(GetSessionCollectionByIdQuery request, CancellationToken cancellationToken)
		{
			_validator.ValidateAndThrow(request);
			var collection = await _capturesService.GetSessionCollection(request.Id);
			if(collection is null)
			{
				throw new SessionCollectionNotFoundException($"No Sessioncollection found with id {request.Id}");
			}

			var sessions = new List<Session>();
			foreach(var sessionData in collection.Data.Sessions)
			{
				var assetId = sessionData.File[0];
				var fileBytes = await _capturesService.DownloadAsset(Guid.Parse(assetId));
				sessions.Add(JsonConvert.DeserializeObject<Session>(Encoding.UTF8.GetString(fileBytes)));
			}
			var sessionCollectionDTO = _mapper.Map<SessionCollectionDTO>(collection);
			sessionCollectionDTO.Sessions = sessions;
			return sessionCollectionDTO;
		}
	}
}
