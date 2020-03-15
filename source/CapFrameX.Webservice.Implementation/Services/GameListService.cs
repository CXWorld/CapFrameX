using CapFrameX.Webservice.Data.Entities;
using CapFrameX.Webservice.Data.Interfaces;
using CapFrameX.Webservice.Persistance;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace CapFrameX.Webservice.Implementation.Services
{
	public class GameListService: IGameListService
	{
		private readonly CXContext _context;

		public GameListService(CXContext context) {
			_context = context;
		}

		public Task<Game[]> GetGameListAsync()
		{
			return _context.GameList.ToArrayAsync();
		}
	}
}
