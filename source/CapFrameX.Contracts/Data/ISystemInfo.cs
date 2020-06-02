namespace CapFrameX.Contracts.Data
{
    public interface ISystemInfo
    {
        string GetProcessorName();

        string GetGraphicCardName();

        string GetOSVersion();

        string GetMotherboardName();

        string GetSystemRAMInfoName();
    }
}
