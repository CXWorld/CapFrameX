namespace CapFrameX.Contracts.Overlay
{
    [System.Flags]
    public enum EOverlayEntryType
    {
        CX = 1,
        OnlineMetric = 2,
        Mainboard = 4,
        FanController = 8,
        CPU = 16,
        RAM = 32,
        GPU = 64,
        HDD = 128,
        Undefined = 256
    }
}
