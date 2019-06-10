using System.Collections.Generic;
using System.IO;

namespace CapFrameX.Contracts.Data
{
    public interface IRecordDataProvider
    {
        IFileRecordInfo GetIFileRecordInfo(FileInfo fileInfo);

        IList<IFileRecordInfo> GetFileRecordInfoList();

        void SavePresentData(IList<string> recordLines, string filePath, string processName, int captureTime);
    }
}
