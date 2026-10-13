using CapFrameX.Data.Session.Classes;
using CapFrameX.Webservice.Data;
using CapFrameX.Webservice.Data.DTO;
using CapFrameX.Webservice.Data.Mappings;
using CapFrameX.Webservice.Data.Exceptions;
using CapFrameX.Webservice.Data.Interfaces;
using CapFrameX.Webservice.Data.Queries;
using FluentValidation;
using MediatR;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CapFrameX.Webservice.Data.Extensions;

namespace CapFrameX.Webservice.Implementation.Handlers
{
	public class GetSessionCollectionByIdHandler : IRequestHandler<GetSessionCollectionByIdQuery, SessionCollectionDTO>
	{
		private readonly IValidator<GetSessionCollectionByIdQuery> _validator;
		private readonly ISessionService _capturesService;

		public GetSessionCollectionByIdHandler(IValidator<GetSessionCollectionByIdQuery> validator, ISessionService capturesService)
		{
			_validator = validator;
			_capturesService = capturesService;
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
				var (fileName, fileBytes) = await _capturesService.DownloadAsset(Guid.Parse(assetId));
				if (fileName.EndsWith(".gz"))
				{
						fileBytes = fileBytes.Decompress();
				}
				sessions.Add(JsonConvert.DeserializeObject<Session>(Encoding.UTF8.GetString(fileBytes)));
			}
			var sessionCollectionDTO = collection.ToSessionCollectionDto();
			sessionCollectionDTO.Sessions = sessions;
			return sessionCollectionDTO;
		}
	}
}
