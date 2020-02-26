using CapFrameX.Data.Session.Contracts;
using System.Collections.Generic;
using System.IO;

namespace CapFrameX.Contracts.Data
{
	public interface IRecordManager
	{
		IFileRecordInfo GetFileRecordInfo(FileInfo fileInfo);

		IList<IFileRecordInfo> GetFileRecordInfoList();

		bool SaveSessionRunsToFile(IEnumerable<ISessionRun> runs, string filePath, string processName);

		void AddGameNameToMatchingList(string processName, string gameName);

		string GetGameFromMatchingList(string processName);

		string GetOutputFilename(string processName);

		void UpdateCustomData(IFileRecordInfo recordInfo, string customCpuInfo,
			string customGpuInfo, string customRamInfo, string customGameName, string customComment);
		List<ISystemInfoEntry> GetSystemInfos(IFileRecordInfo recordInfo);
		ISession LoadData(string file);
		ISessionRun ConvertPresentDataLinesToSessionRun(IEnumerable<string> presentLines);

	}
}
