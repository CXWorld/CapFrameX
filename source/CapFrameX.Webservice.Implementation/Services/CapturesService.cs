using CapFrameX.Webservice.Data.DTO;
using CapFrameX.Webservice.Data.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CapFrameX.Webservice.Implementation.Services
{
	public class CapturesService: ICapturesService
	{
		private readonly ICaptureStorage _storage;

		public CapturesService(ICaptureStorage storage) {
			_storage = storage;
		}

		public async Task<CaptureCollectionMetadata> SaveCaptures(string appVersion, IEnumerable<Capture> captures)
		{
			var metadata = await _storage.SaveCaptureCollection(new CaptureCollection()
			{
				Captures = captures
			});
			return new CaptureCollectionMetadata() { 
				AppVersion = appVersion,
				UploadTimestamp = DateTime.UtcNow,
				Id = metadata.Id,
				CaptureCount = captures.Count()
			};
		}

		public async Task<CaptureCollection> GetCaptureCollectionById(Guid id)
		{
			return await _storage.GetCaptureCollection(id);
		}
	}
}
