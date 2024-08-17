using AutoMapper;
using CapFrameX.Webservice.Data.DTO;
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
	public class GetProcessListHandler : IRequestHandler<GetProcessListQuery, ProcessListDataDTO[]>
	{
		private readonly IProcessListService _squidexService;
		private readonly IMapper _mapper;

		public GetProcessListHandler(IProcessListService squidexService, IMapper mapper)
		{
			_squidexService = squidexService;
			_mapper = mapper;
		}
		public async Task<ProcessListDataDTO[]> Handle(GetProcessListQuery request, CancellationToken cancellationToken)
		{
			var result = await _squidexService.GetProcessList();
			return _mapper.Map<ProcessListDataDTO[]>(result);
		}
	}
}
