using CapFrameX.Webservice.Data;
using CapFrameX.Webservice.Data.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace CapFrameX.Webservice.Implementation.Services
{
	public class CapturesService: ICapturesService
	{
		public CapturesService() { }

		public async Task<Capture> GetCaptureById(Guid id)
		{
			return new Capture();
		}
	}
}
