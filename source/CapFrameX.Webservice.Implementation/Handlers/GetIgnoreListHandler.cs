using CapFrameX.Webservice.Data.Entities;
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
	public class GetIgnoreListHandler : IRequestHandler<GetIgnoreListQuery, IgnoreEntry[]>
	{
		private readonly IIgnoreListService _ignoreListService;

		public GetIgnoreListHandler(IIgnoreListService ignoreListService)
		{
			_ignoreListService = ignoreListService;
		}
		public Task<IgnoreEntry[]> Handle(GetIgnoreListQuery request, CancellationToken cancellationToken)
		{
			return _ignoreListService.GetIgnoreListAsync();
		}
	}
}
