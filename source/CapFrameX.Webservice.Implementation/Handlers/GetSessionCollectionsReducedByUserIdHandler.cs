using AutoMapper;
using CapFrameX.Webservice.Data.DTO;
using CapFrameX.Webservice.Data.Interfaces;
using CapFrameX.Webservice.Data.Queries;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CapFrameX.Webservice.Implementation.Handlers
{
	public class GetSessionCollectionsReducedByUserIdHandler : IRequestHandler<GetSessionCollectionsReducedForUserByIdQuery, IEnumerable<SessionCollectionReducedDTO>>
	{
		private readonly IMapper _mapper;
		private readonly ISessionService _sessionService;

		public GetSessionCollectionsReducedByUserIdHandler(IMapper mapper, ISessionService sessionService)
		{
			_mapper = mapper;
			_sessionService = sessionService;
		}
		public async Task<IEnumerable<SessionCollectionReducedDTO>> Handle(GetSessionCollectionsReducedForUserByIdQuery request, CancellationToken cancellationToken)
		{
			return (await _sessionService.GetSessionCollectionsForUser(request.UserId)).Select(sc => _mapper.Map<SessionCollectionReducedDTO>(sc));
		}
	}
}
