using System.ComponentModel;

namespace CapFrameX.Contracts.PMD
{
    public enum EPmdDriverStatus
    {
        [Description("Ready")]
        Ready,
        [Description("Connected")]
        Connected,
        [Description("Error")]
        Error
    }
}
