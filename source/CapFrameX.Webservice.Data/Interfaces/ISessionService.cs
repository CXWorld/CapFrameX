using CapFrameX.Webservice.Data.DTO;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace CapFrameX.Webservice.Data.Interfaces
{
	public interface ISessionService
	{
		Task<Guid> SaveSessionCollection(SqSessionCollectionData sessionCollection);
		Task<SqSessionCollection> GetSessionCollection(Guid id);
		Task<IEnumerable<SqSessionCollection>> GetSessionCollectionsForUser(Guid userId);
		Task DeleteCollection(Guid id, Guid userId);
	}
}
