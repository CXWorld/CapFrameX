using CapFrameX.Data.Session.Contracts;
using CapFrameX.Data.Session.Converters;
using Newtonsoft.Json;
using System;
using System.ComponentModel;

namespace CapFrameX.Data.Session.Classes
{
	public class SessionInfo : ISessionInfo
	{
		[JsonConverter(typeof(VersionConverter))]
		public Version AppVersion { get; set; }
		public Guid Id { get; set; }
		[Description("Cpu")]
		public string Processor { get; set; }
		public string GameName { get; set; }
		public string ProcessName { get; set; }
		public DateTime CreationDate { get; set; }
		[Description("Mainboard")]
		public string Motherboard { get; set; }
		[Description("OS")]
		public string OS { get; set; }
		[Description("Ram")]
		public string SystemRam { get; set; }
		public string BaseDriverVersion { get; set; }
		[Description("Gpu Driver")]
		public string GPUDriverVersion { get; set; }
		public string DriverPackage { get; set; }
		[Description("Gpu")]
		public string GPU { get; set; }
		public string GPUCount { get; set; }
		public string GpuCoreClock { get; set; }
		public string GpuMemoryClock { get; set; }
		public string Comment { get; set; }
		public string ApiInfo { get; set; }
		public string PresentationMode { get; set; }
		public string ResolutionInfo { get; set; }
	}
}
