using System.ComponentModel;
using System.IO;

namespace CapFrameX.Contracts.Data
{
	public interface IFileRecordInfo: INotifyPropertyChanged
	{
		string GameName { get; set; }
		string ProcessName { get; set; }
		string CreationDate { get; }
		string CreationTime { get; }
		double RecordTime { get; }
		string FullPath { get; }
		FileInfo FileInfo { get; }
		string CombinedInfo { get; }
		string MotherboardName { get; }
		string OsVersion { get; }
		string ProcessorName { get; set; }
		string SystemRamInfo { get; set; }
		string BaseDriverVersion { get; }
		string DriverPackage { get; }
		string NumberGPUs { get; }
		string GraphicCardName { get; set; }
		string GPUCoreClock { get; }
		string GPUMemoryClock { get; }
		string GPUMemory { get; }
		string GPUDriverVersion { get; }
		string IsAggregated { get; }
		string Comment { get; set; }
		bool IsValid { get; }
		bool HasInfoHeader { get; }
		string Id { get; }
		string Hash { get; }
		string ApiInfo { get; }
		string ResizableBar { get; }
		string WinGameMode { get; }
		string HAGS { get; }
		string PresentationMode { get; }
		string Resolution { get; }
	}
}
