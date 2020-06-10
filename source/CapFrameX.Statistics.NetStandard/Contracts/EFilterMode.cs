using System.ComponentModel;

namespace CapFrameX.Statistics.NetStandard.Contracts
{
    public enum EFilterMode
    {
        [Description("Raw data")]
        None,
        [Description("Moving average")]
        MovingAverage,
        [Description("Median")]
        Median
    }
}
