using CapFrameX.Webservice.Data.DTO;
using CapFrameX.Webservice.Data.Entities;
using CapFrameX.Webservice.Data.Interfaces;
using CapFrameX.Webservice.Persistance;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CapFrameX.Webservice.Implementation.Services
{
	public class SessionService: ISessionService
	{
		private readonly CXContext _context;

		public SessionService(CXContext context) {
			_context = context;
		}

		public async Task<SessionCollection> GetSessionCollection(Guid id)
		{
			return await _context.SessionCollections.FindAsync(id);
		}

		public async Task<Guid> SaveSessionCollection(SessionCollection sessionCollection)
		{
			_context.SessionCollections.Add(sessionCollection);
			await _context.SaveChangesAsync();
			return sessionCollection.Id;
		}
	}
}
