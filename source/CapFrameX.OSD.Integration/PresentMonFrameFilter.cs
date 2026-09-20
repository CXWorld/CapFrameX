using System.Globalization;

namespace CapFrameX.OSD.Integration
{
    internal static class PresentMonFrameFilter
    {
        internal static bool IsForTargetProcess(string[] row, int processIdIndex,
            int targetPid)
        {
            return targetPid > 0 && row != null && processIdIndex >= 0 &&
                row.Length > processIdIndex &&
                int.TryParse(row[processIdIndex], NumberStyles.Integer,
                    CultureInfo.InvariantCulture, out int rowPid) &&
                rowPid == targetPid;
        }
    }
}
