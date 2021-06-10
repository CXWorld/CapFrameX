using System.ComponentModel;

namespace CapFrameX.Contracts.Capture
{
    public enum ELogMessageType
    {
        [Description("Error")]
        Error,
        [Description("Basic Info")]
        BasicInfo,
        [Description("Advanced Info")]
        AdvancedInfo
    }
}
