using System.ComponentModel;

namespace CapFrameX.Statistics.NetStandard.Contracts
{
    public enum EFilterMode
    {
        [Description("Raw data")]
        None,
        [Description("Average")]
        MovingAverage,
        [Description("Median")]
        Median
    }
}
