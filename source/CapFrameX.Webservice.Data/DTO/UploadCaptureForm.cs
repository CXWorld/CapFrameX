using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Text;

namespace CapFrameX.Webservice.Data.DTO
{
	public class UploadCapturesForm
	{
		public string AppVersion { get; set; }
		public List<IFormFile> Capture { get; set; } = new List<IFormFile>();
	}
}
