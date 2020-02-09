using System;
using System.Collections.Generic;
using System.Text;

namespace CapFrameX.Webservice.Data.DTO
{
	public class CaptureCollection
	{
		public Guid Id { get; set; }
		public DateTime UploadTimestamp { get; set; }
		public IEnumerable<Capture> Captures { get; set; }
	}
}
