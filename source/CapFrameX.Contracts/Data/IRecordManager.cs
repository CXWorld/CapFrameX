using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace CapFrameX.Contracts.Data
{
	public interface IRecordManager
	{
		Task<IFileRecordInfo> GetFileRecordInfo(FileInfo fileInfo);

		Task<bool> SaveSessionRunsToFile(IEnumerable<ISessionRun> runs, string processName);

		void AddGameNameToMatchingList(string processName, string gameName);

		string GetGameFromMatchingList(string processName);

		void UpdateCustomData(IFileRecordInfo recordInfo, string customCpuInfo,
			string customGpuInfo, string customRamInfo, string customGameName, string customComment);

		List<ISystemInfoEntry> GetSystemInfos(IFileRecordInfo recordInfo);

		ISession LoadData(string file);

		ISessionRun ConvertPresentDataLinesToSessionRun(IEnumerable<string> presentLines);
	}
}
