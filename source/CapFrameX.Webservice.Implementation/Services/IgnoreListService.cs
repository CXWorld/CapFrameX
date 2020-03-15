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
	public class IgnoreListService: IIgnoreListService
	{
		private readonly CXContext _context;

		public IgnoreListService(CXContext context) {
			_context = context;
		}

		public Task<IgnoreEntry[]> GetIgnoreListAsync()
		{
			return _context.IgnoreList.ToArrayAsync();
		}
	}
}
