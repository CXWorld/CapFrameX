namespace CapFrameX.Contracts.Data
{
    public interface ISystemInfo
    {
        ESystemInfoTertiaryStatus ResizableBarD3DStatus { get; }

        ESystemInfoTertiaryStatus ResizableBarVulkanStatus { get; }

        ESystemInfoTertiaryStatus ResizableBarHardwareStatus { get; }

        ulong PciBarSizeD3D { get; }

        ulong PciBarSizeHardware { get; }

        ulong PciBarSizeVulkan { get; }

        ESystemInfoTertiaryStatus GameModeStatus { get; }

        ESystemInfoTertiaryStatus HardwareAcceleratedGPUSchedulingStatus { get; }

        /// <summary>
        /// UEFI Secure Boot. While it is on, Windows ignores the test signing boot option.
        /// </summary>
        ESystemInfoTertiaryStatus SecureBootStatus { get; }

        /// <summary>
        /// Test signing as the running kernel enforces it, i.e. whether test-signed drivers can load.
        /// </summary>
        ESystemInfoTertiaryStatus TestSigningStatus { get; }

        /// <summary>
        /// Virtualization-based security; enabled means running, not merely configured.
        /// </summary>
        ESystemInfoTertiaryStatus VirtualizationBasedSecurityStatus { get; }

        /// <summary>
        /// Memory integrity (hypervisor-enforced code integrity) as the running kernel enforces it.
        /// </summary>
        ESystemInfoTertiaryStatus MemoryIntegrityStatus { get; }

        string GetDeviceName();

        string GetProcessorName();

        string GetGraphicCardName();

        string GetOSVersion();

        string GetMotherboardName();

        string GetMotherboardManufacturerBrand();

        string GetBiosVersion();

        string GetSystemRAMInfoName();

        string GetSystemRAMManufacturer();

        string GetProcessorCoreCountInfo();

        void SetSystemInfosStatus();

        double GetCapFrameXAppCpuUsage();
    }
}
