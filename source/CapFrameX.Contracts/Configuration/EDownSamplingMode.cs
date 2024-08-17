using System.ComponentModel;

namespace CapFrameX.Contracts.Configuration
{
    public enum PmdSampleFilterMode
    {
        [Description("Last of N")]
        Single,
        [Description("Average of N")]
        Average
    }
}
