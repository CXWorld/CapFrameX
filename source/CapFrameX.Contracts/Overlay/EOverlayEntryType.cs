using CapFrameX.Extensions.NetStandard.Attributes;
using System.ComponentModel;

namespace CapFrameX.Contracts.Overlay
{
    [System.Flags]
    public enum EOverlayEntryType
    {
        [Description("Application info")]
        [ShortDescription("App")]
        CX = 1,
        [Description("Performance metric")]
        [ShortDescription("Metric")]
        OnlineMetric = 2,
        [Description("Mainboard sensor")]
        [ShortDescription("Mainboard")]
        Mainboard = 4,
        [Description("Fan controller sensor")]
        [ShortDescription("Fan")]
        FanController = 8,
        [Description("CPU sensor")]
        [ShortDescription("CPU")]
        CPU = 16,
        [Description("Memory sensor")]
        [ShortDescription("RAM")]
        RAM = 32,
        [Description("GPU sensor")]
        [ShortDescription("GPU")]
        GPU = 64,
        [Description("Storage sensor")]
        [ShortDescription("Storage")]
        HDD = 128,
        [Description("Miscellaneous")]
        [ShortDescription("Misc")]
        Undefined = 256
    }
}
