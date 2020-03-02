using CapFrameX.Webservice.Data.DTO;
using CapFrameX.Webservice.Data.Entities;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace CapFrameX.Webservice.Data.Interfaces
{
	public interface ISessionService
	{
		Task<Guid> SaveSessionCollection(SessionCollection sessionCollection);
		Task<SessionCollection> GetSessionCollection(Guid id);
	}
}
