using AutoMapper;
using CapFrameX.Webservice.Data.DTO;
using CapFrameX.Webservice.Data.Exceptions;
using CapFrameX.Webservice.Data.Interfaces;
using CapFrameX.Webservice.Data.Queries;
using MediatR;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CapFrameX.Webservice.Implementation.Handlers
{
	public class GetSessionCollectionReducedByIdHandler : IRequestHandler<GetSessionCollectionReducedByIdQuery, SessionCollectionReducedDTO>
	{
		private readonly IMapper _mapper;
		private readonly ISessionService _sessionService;

		public GetSessionCollectionReducedByIdHandler(IMapper mapper, ISessionService sessionService)
		{
			_mapper = mapper;
			_sessionService = sessionService;
		}
		public async Task<SessionCollectionReducedDTO> Handle(GetSessionCollectionReducedByIdQuery request, CancellationToken cancellationToken)
		{
			var sessionCollection = await _sessionService.GetSessionCollection(request.Id);
			if(sessionCollection is null)
			{
				throw new SessionCollectionNotFoundException($"No Sessioncollection found with id {request.Id}");
			}
			return _mapper.Map<SessionCollectionReducedDTO>(sessionCollection);
		}
	}
}
