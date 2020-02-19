using System.Collections.Generic;
using System.IO;

namespace CapFrameX.Contracts.Data
{
	public interface IRecordManager
	{
		IFileRecordInfo GetFileRecordInfo(FileInfo fileInfo);

		IList<IFileRecordInfo> GetFileRecordInfoList();

		bool SavePresentData(IList<string> recordLines, string filePath, string processNamem,
			bool IsAggregated = false, IList<string> externalHeaderLines = null);

		bool SaveAggregatedPresentData(IList<IList<string>> aggregatedCaptureData, IList<string> externalHeaderLines = null);

		void AddGameNameToMatchingList(string processName, string gameName);

		string GetGameFromMatchingList(string processName);

		string GetOutputFilename(string processName);

		IList<string> CreateHeaderLinesFromRecordInfo(IFileRecordInfo recordInfo);

		void UpdateCustomData(IFileRecordInfo recordInfo, string customCpuInfo,
			string customGpuInfo, string customRamInfo, string customGameName, string customComment);
		List<ISystemInfoEntry> GetSystemInfos(IFileRecordInfo recordInfo);
		ISession LoadData(string csvFile);
		IList<string> LoadPresentData(string csvFile);

		string GetProcessNameFromDataLine(string dataLine);
		long GetQpcTimeFromDataLine(string dataLine);
		string GetProcessIdFromDataLine(string dataLine);

		double GetStartTimeFromDataLine(string dataLine);

		double GetFrameTimeFromDataLine(string dataLine);

	}
}
