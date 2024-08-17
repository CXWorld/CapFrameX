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
	public class SearchSessionsHandler : IRequestHandler<SearchSessionsQuery, SqSessionData[]>
	{
		private readonly ISessionService _squidexService;
		private readonly IMapper _mapper;

		public SearchSessionsHandler(ISessionService squidexService, IMapper mapper)
		{
			_squidexService = squidexService;
			_mapper = mapper;
		}
		public async Task<SqSessionData[]> Handle(SearchSessionsQuery request, CancellationToken cancellationToken)
		{
			var result = await _squidexService.SearchSessions(request.Cpu, request.Gpu, request.Mainbaord, request.Ram, request.GameName, request.Comment);
			return result.ToArray();
		}
	}
}
