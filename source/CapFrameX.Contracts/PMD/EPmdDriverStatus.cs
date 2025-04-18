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

    public enum EPmdServiceStatus
    {
        [Description("Waiting")]
        Waiting,
        [Description("Running")]
        Running,
        [Description("Stopped")]
        Stopped,
        [Description("Error")]
        Error
    }
}
