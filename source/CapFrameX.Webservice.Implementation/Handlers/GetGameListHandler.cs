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
	public class GetGameListHandler : IRequestHandler<GetGameListQuery, Game[]>
	{
		private readonly IGameListService _gameListService;

		public GetGameListHandler(IGameListService gameListService)
		{
			_gameListService = gameListService;
		}
		public Task<Game[]> Handle(GetGameListQuery request, CancellationToken cancellationToken)
		{
			return _gameListService.GetGameListAsync();
		}
	}
}
