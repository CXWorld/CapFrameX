using CapFrameX.Webservice.Data.DTO;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace CapFrameX.Webservice.Data.Interfaces
{
	public interface ICaptureStorage
	{
		Task<CaptureCollection> GetCaptureCollection(Guid collectionId);
		Task<CaptureCollection> SaveCaptureCollection(CaptureCollection captureCollection);
	}
}
