using System.IO;

namespace CapFrameX.Contracts.Data
{
	public interface IFileRecordInfo
	{
		string GameName { get; set; }
		string ProcessName { get; }
		string CreationDate { get; }
		string CreationTime { get; }
		double RecordTime { get; }
		string FullPath { get; }
		FileInfo FileInfo { get; }
		string CombinedInfo { get; }
		string MotherboardName { get; }
		string OsVersion { get; }
		string ProcessorName { get; }
		string SystemRamInfo { get; }
		string BaseDriverVersion { get; }
		string DriverPackage { get; }
		string NumberGPUs { get; }
		string GraphicCardName { get; }
		string GPUCoreClock { get; }
		string GPUMemoryClock { get; }
		string GPUMemory { get; }
		string IsAggregated { get; }
		string Comment { get; }
		bool IsValid { get; }
		bool HasInfoHeader { get; }
		string Id { get; }
		string Hash { get; }
	}
}
