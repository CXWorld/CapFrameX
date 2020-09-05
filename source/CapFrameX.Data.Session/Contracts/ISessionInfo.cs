using System;

namespace CapFrameX.Data.Session.Contracts
{
	public interface ISessionInfo
	{
		Version AppVersion { get; set; }
		Guid Id { get; set; }
		string Processor { get; set; }
		string GameName { get; set; }
		string ProcessName { get; set; }
		DateTime CreationDate { get; set; }
		string Motherboard { get; set; }
		string OS { get; set; }
		string SystemRam { get; set; }
		string BaseDriverVersion { get; set; }
		string DriverPackage { get; set; }
		string GPUDriverVersion { get; set; }
		string GPU { get; set; }
		string GPUCount {get; set; }
		string GpuCoreClock { get; set; }
		string GpuMemoryClock { get; set; }
		string Comment { get; set; }
		string ApiInfo { get; set; }
		string PresentationMode { get; set; }
		string ResolutionInfo { get; set; }
	}
}