using CapFrameX.Data.Session.Contracts;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace CapFrameX.Contracts.Data
{
	public interface IRecordManager
	{
		/// <param name="fileMayStillBeWritten">True for a file reported by a Created or Changed
		/// event, whose writer may not be done yet: its first read waits a moment. Files from a
		/// directory scan or a drop are read right away.</param>
		Task<IFileRecordInfo> GetFileRecordInfo(FileInfo fileInfo, bool fileMayStillBeWritten = false);

		Task<bool> SaveSessionRunsToFile(IEnumerable<ISessionRun> runs, string processName, string comment, string recordDirectory, List<ISessionInfo> HWInfo);

		void UpdateCustomData(IFileRecordInfo recordInfo, string customCpuInfo,
			string customGpuInfo, string customRamInfo, string customMainboardInfo,
			string customGameName, string customComment, string customResolution = null);

		List<ISystemInfoEntry> GetSystemInfos(IFileRecordInfo recordInfo);

		ISession LoadData(string file);

		ISessionRun ConvertPresentDataLinesToSessionRun(IEnumerable<string> presentLines);

		Task SavePresentmonRawToFile(IEnumerable<string> lines, string process, string recordDirectory);

		void NormalizeStartTimesOfSessionRuns(IEnumerable<ISessionRun> sessionRuns);
	}
}
