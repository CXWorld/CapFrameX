﻿using CapFrameX.Data.Session.Contracts;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace CapFrameX.Contracts.Data
{
	public interface IRecordManager
	{
		Task<IFileRecordInfo> GetFileRecordInfo(FileInfo fileInfo);

		Task<bool> SaveSessionRunsToFile(IEnumerable<ISessionRun> runs, string processName, string comment, string recordDirectory, List<ISessionInfo> HWInfo);

		void UpdateCustomData(IFileRecordInfo recordInfo, string customCpuInfo,
			string customGpuInfo, string customRamInfo, string customGameName, string customComment);

		List<ISystemInfoEntry> GetSystemInfos(IFileRecordInfo recordInfo);

		ISession LoadData(string file);

		ISessionRun ConvertPresentDataLinesToSessionRun(IEnumerable<string> presentLines);

		Task SavePresentmonRawToFile(IEnumerable<string> lines, string process, string recordDirectory);
		void NormalizeStartTimesOfSessionRuns(IEnumerable<ISessionRun> sessionRuns);
	}
}
