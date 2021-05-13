using System;
using System.Linq;
using Microsoft.Extensions.Logging;
using Mixaill.HwInfo.SetupApi;
using Mixaill.HwInfo.SetupApi.Defines;

namespace CapFrameX.SetupAPI.NetStandard
{
    /// <summary>
    /// Nuget https://github.com/Mixaill/Mixaill.HwInfo
    /// </summary>
    public class SetupAPIWrapper : ISetupAPI
    {
        public EPciDeviceInfoStatus PciAbove4GDecodingStatus { get; } = EPciDeviceInfoStatus.Invalid;
        public EPciDeviceInfoStatus PciLargeMemoryStatus { get; } = EPciDeviceInfoStatus.Invalid;

        public SetupAPIWrapper(ILogger<SetupAPIWrapper> logger)
        {
            try
            {
                using (var displayDevices = new DeviceInfoSet(DeviceClassGuid.Display))
                {
                    var decodingStatus = displayDevices.Devices.Any(x => (x as DeviceInfoPci)?.Pci_Above4GDecoding == true);
                    var largeMemoryStatus = displayDevices.Devices.Any(x => (x as DeviceInfoPci)?.Pci_LargeMemory == true);

                    PciAbove4GDecodingStatus = decodingStatus ? EPciDeviceInfoStatus.Enabled : EPciDeviceInfoStatus.Disabled;
                    PciLargeMemoryStatus = largeMemoryStatus ? EPciDeviceInfoStatus.Enabled : EPciDeviceInfoStatus.Disabled;                   
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error while getting PCI device infos.");
            }
        }
    }
}
