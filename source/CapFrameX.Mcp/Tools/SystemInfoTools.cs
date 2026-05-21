using CapFrameX.Contracts.Data;
using CapFrameX.Mcp.Attributes;

namespace CapFrameX.Mcp.Tools
{
    [McpServerToolType]
    public class SystemInfoTools
    {
        private readonly ISystemInfo _systemInfo;

        public SystemInfoTools(ISystemInfo systemInfo)
        {
            _systemInfo = systemInfo;
        }

        [McpServerTool(Name = "cfx_get_current_system",
            Description = "Returns the live system info as CapFrameX sees it: CPU/GPU/RAM/OS/motherboard names, " +
                "Resizable BAR status (hardware + D3D + Vulkan), HAGS status, Windows Game Mode status, and PCI BAR sizes. " +
                "Useful for diagnosing 'why is performance different from last time' or comparing the current system to a record's snapshot.")]
        public CurrentSystemInfo GetCurrentSystem()
        {
            // The implementation caches some values internally but Resizable Bar / HAGS / GameMode
            // status is set during construction. Re-collect to make sure we return the latest snapshot.
            try { _systemInfo.SetSystemInfosStatus(); } catch { /* keep cached values on failure */ }

            return new CurrentSystemInfo
            {
                Cpu = SafeGet(() => _systemInfo.GetProcessorName()),
                Gpu = SafeGet(() => _systemInfo.GetGraphicCardName()),
                Os = SafeGet(() => _systemInfo.GetOSVersion()),
                Motherboard = SafeGet(() => _systemInfo.GetMotherboardName()),
                Ram = SafeGet(() => _systemInfo.GetSystemRAMInfoName()),
                ResizableBarHardware = StatusToString(_systemInfo.ResizableBarHardwareStatus),
                ResizableBarD3D = StatusToString(_systemInfo.ResizableBarD3DStatus),
                ResizableBarVulkan = StatusToString(_systemInfo.ResizableBarVulkanStatus),
                HardwareAcceleratedGpuScheduling = StatusToString(_systemInfo.HardwareAcceleratedGPUSchedulingStatus),
                WindowsGameMode = StatusToString(_systemInfo.GameModeStatus),
                PciBarSizeHardware = NullIfZero(_systemInfo.PciBarSizeHardware),
                PciBarSizeD3D = NullIfZero(_systemInfo.PciBarSizeD3D),
                PciBarSizeVulkan = NullIfZero(_systemInfo.PciBarSizeVulkan),
            };
        }

        private static string SafeGet(System.Func<string> fn)
        {
            try { return fn(); } catch { return null; }
        }

        private static string StatusToString(ESystemInfoTertiaryStatus s)
        {
            switch (s)
            {
                case ESystemInfoTertiaryStatus.Enabled: return "enabled";
                case ESystemInfoTertiaryStatus.Disabled: return "disabled";
                default: return "unknown";
            }
        }

        private static ulong? NullIfZero(ulong v) => v == 0UL ? (ulong?)null : v;
    }
}
