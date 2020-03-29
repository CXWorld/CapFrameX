using Newtonsoft.Json;
using Squidex.ClientLibrary;
using System;
using System.Collections.Generic;
using System.Text;

namespace CapFrameX.Webservice.Data.DTO
{
	public class SqSessionData
	{
		public string[] File { get; set; }
		public string Hash { get; set; }
		public string AppVersion { get; set; }
		public string ProcessName { get; set; }
		public string GameName { get; set; }
		public string Comment { get; set; }
		public DateTime CreationDate { get; set; }
	}
	public class SqSessionCollectionData
	{
		[JsonConverter(typeof(InvariantConverter))]
		public Guid? Sub { get; set; }
		[JsonConverter(typeof(InvariantConverter))]
		public string Description { get; set; }
		[JsonConverter(typeof(InvariantConverter))]
		public SqSessionData[] Sessions { get; set; }
	}

	public class SqSessionCollection : Content<SqSessionCollectionData>
	{
	}
}