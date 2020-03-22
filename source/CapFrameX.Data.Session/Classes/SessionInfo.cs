using CapFrameX.Data.Session.Contracts;
using CapFrameX.Data.Session.Converters;
using Newtonsoft.Json;
using System;

namespace CapFrameX.Data.Session.Classes
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
		public string GPUDriverVersion { get; set; }
		public string DriverPackage { get; set; }
		public string GPU { get; set; }
		public string GPUCount { get; set; }
		public string GpuCoreClock { get; set; }
		public string GpuMemoryClock { get; set; }
		public string Comment { get; set; }
	}
}
