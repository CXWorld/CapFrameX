using Newtonsoft.Json;
using Squidex.ClientLibrary;
using System;
using System.Collections.Generic;
using System.Text;

namespace CapFrameX.Webservice.Data.DTO
{
	public class ProcessListDataDTO
	{
		[JsonProperty("Name")]
		public string Name { get; set; }
		[JsonProperty("DisplayName")]
		public string DisplayName { get; set; }
		[JsonProperty("IsBlacklisted")]
		public bool IsBlacklisted { get; set; }
		[JsonProperty("IsWhitelisted")]
		public bool IsWhitelisted { get; set; }
	}

	public sealed class ProcessListData
	{
		[JsonConverter(typeof(InvariantConverter))]
		public string Name { get; set; }

		[JsonConverter(typeof(InvariantConverter))]
		public string DisplayName { get; set; }
		[JsonConverter(typeof(InvariantConverter))]
		public bool IsBlacklisted { get; set; }
		[JsonConverter(typeof(InvariantConverter))]
		public bool IsWhitelisted { get; set; }
	}

	public class ProcessList : Content<ProcessListData>
	{
	}
}
