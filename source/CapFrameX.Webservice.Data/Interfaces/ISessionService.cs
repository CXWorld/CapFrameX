using CapFrameX.Webservice.Data.DTO;
using System;
using System.Collections.Generic;
using System.IO;
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
		Task<Guid> UploadAsset(byte[] data, string fileName);
		Task<(string, byte[])> DownloadAsset(Guid id);
		Task<IEnumerable<SqSessionData>> SearchSessions(string cpu, string gpu, string mainboard, string ram, string gameName, string comment);
	}
}
