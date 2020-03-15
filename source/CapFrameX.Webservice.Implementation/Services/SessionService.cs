﻿using CapFrameX.Webservice.Data.DTO;
using CapFrameX.Webservice.Data.Entities;
using CapFrameX.Webservice.Data.Interfaces;
using CapFrameX.Webservice.Persistance;
using Microsoft.EntityFrameworkCore;
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

		public async Task<IEnumerable<SessionCollection>> GetSessionCollectionsForUser(Guid userId)
		{
			return await _context.SessionCollections.Where(sc => sc.UserId == userId).ToArrayAsync();
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

		public async Task DeleteCollection(Guid id, Guid userId)
		{
			var sessionToDelete = await GetSessionCollection(id);
			if(sessionToDelete.UserId != userId)
			{
				throw new UnauthorizedAccessException("You cannot delete this session.");
			}
			_context.SessionCollections.Remove(sessionToDelete);
			await _context.SaveChangesAsync();
		}
	}
}
