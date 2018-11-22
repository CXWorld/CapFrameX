using CapFrameX.Extensions;

namespace CapFrameX.OcatInterface
{
    /// <summary>
    /// UI wrapper for record file info
    /// </summary>
    public class OcatRecordInfo
    {
        public string GameName { get; private set; }
        public string CreationDate { get; private set; }
        public string RecordTime { get; private set; }

        public OcatRecordInfo(string fileName)
        {
            GameName = fileName.Substring("OCAT-", ".exe");
            CreationDate = fileName.Substring("exe-", "T");
            RecordTime = fileName.Substring(CreationDate + "T", ".csv");
        }
    }
}
