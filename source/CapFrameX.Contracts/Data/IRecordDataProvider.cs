using System.Collections.Generic;
using System.IO;

namespace CapFrameX.Contracts.Data
{
    public interface IRecordDataProvider
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
    }
}
