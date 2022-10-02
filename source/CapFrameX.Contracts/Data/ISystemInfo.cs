namespace CapFrameX.Contracts.Data
{
    public interface ISystemInfo
    {
        ESystemInfoTertiaryStatus ResizableBarD3DStatus { get; }

        ESystemInfoTertiaryStatus ResizableBarVulkanStatus { get; }

        ESystemInfoTertiaryStatus ResizableBarHardwareStatus { get; }

        ulong PciBarSizeD3D { get; }

        ulong PciBarSizeVulkan { get; }

        ESystemInfoTertiaryStatus GameModeStatus { get; }

        ESystemInfoTertiaryStatus HardwareAcceleratedGPUSchedulingStatus { get; }

        string GetProcessorName();

        string GetGraphicCardName();

        string GetOSVersion();

        string GetMotherboardName();

        string GetSystemRAMInfoName();

        void SetSystemInfosStatus();

        double GetCapFrameXAppCpuUsage();
    }
}
