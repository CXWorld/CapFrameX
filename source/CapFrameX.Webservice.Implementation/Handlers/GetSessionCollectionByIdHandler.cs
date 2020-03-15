using AutoMapper;
using CapFrameX.Webservice.Data;
using CapFrameX.Webservice.Data.DTO;
using CapFrameX.Webservice.Data.Exceptions;
using CapFrameX.Webservice.Data.Interfaces;
using CapFrameX.Webservice.Data.Queries;
using FluentValidation;
using MediatR;
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
			return _mapper.Map<SessionCollectionDTO>(collection);
		}
	}
}
