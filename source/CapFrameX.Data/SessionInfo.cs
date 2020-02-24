using CapFrameX.Contracts.Data;
using Newtonsoft.Json;
using System;

namespace CapFrameX.Data
{
	public class SessionInfo : ISessionInfo
	{
		[JsonConverter(typeof(VersionConverter))]
		public Version AppVersion { get; set; }
		public Guid Id { get; set; }
		public string Processor { get; set; }
		public string GameName { get; set; }
		public string ProcessName { get; set; }
		public DateTime CreationDate { get; set; }
		public string Motherboard { get; set; }
		public string OS { get; set; }
		public string SystemRam { get; set; }
		public string BaseDriverVersion { get; set; }
		public string DriverPackage { get; set; }
		public string GPU { get; set; }
		public string GPUCount { get; set; }
		public string GpuCoreClock { get; set; }
		public string GpuMemoryClock { get; set; }
		public string Comment { get; set; }
	}

	public class VersionConverter : JsonConverter<Version>
	{
		public override void WriteJson(JsonWriter writer, Version value, JsonSerializer serializer)
		{
			writer.WriteValue(value.ToString());
		}

		public override Version ReadJson(JsonReader reader, Type objectType, Version existingValue, bool hasExistingValue, JsonSerializer serializer)
		{
			string s = (string)reader.Value;

			return new Version(s);
		}
	}
}
