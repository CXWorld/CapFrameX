using System.Collections.Generic;
using System.IO;

namespace CapFrameX.Contracts.Data
{
    public interface IRecordDataProvider
    {
        IFileRecordInfo GetFileRecordInfo(FileInfo fileInfo);

        IList<IFileRecordInfo> GetFileRecordInfoList();

        bool SavePresentData(IList<string> recordLines, string filePath, string processName, int captureTime);

        void AddGameNameToMatchingList(string processName, string gameName);

        string GetGameFromMatchingList(string processName);
    }
}
