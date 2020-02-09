using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace CapFrameX.Webservice.Data.Interfaces
{
	public interface ICapturesService
	{
		Task<Capture> GetCaptureById(Guid id);
	}
}
