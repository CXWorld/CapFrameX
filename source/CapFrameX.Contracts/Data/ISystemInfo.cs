using System.Threading.Tasks;

namespace CapFrameX.Contracts.Data
{
    public interface ISystemInfo
    {
        ESystemInfoTertiaryStatus ResizableBarSoftwareStatus { get; }

        ESystemInfoTertiaryStatus ResizableBarHardwareStatus { get; }

        ESystemInfoTertiaryStatus GameModeStatus { get; }

        ESystemInfoTertiaryStatus HardwareAcceleratedGPUSchedulingStatus { get; }

        string GetProcessorName();

        string GetGraphicCardName();

        string GetOSVersion();

        string GetMotherboardName();

        string GetSystemRAMInfoName();

        Task SetSystemInfosStatus();
    }
}
