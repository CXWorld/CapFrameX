using System;
using System.Collections.Generic;
using System.Text;

namespace CapFrameX.Webservice.Data.DTO
{
	public class CaptureCollectionMetadata
	{
		public Guid Id { get; set; }
		public DateTime UploadTimestamp { get; set; }
		public string AppVersion { get; set; }
		public int CaptureCount { get; set; }
	}
}
