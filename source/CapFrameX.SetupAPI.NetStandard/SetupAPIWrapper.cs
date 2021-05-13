using System.Linq;

using Mixaill.HwInfo.SetupApi;
using Mixaill.HwInfo.SetupApi.Defines;

namespace CapFrameX.SetupAPI.NetStandard
{
    public class SetupAPIWrapper : ISetupAPI
    {
        public bool PciAbove4GDecodingStatus { get; } = false;
        public bool PciLargeMemoryStatus { get; } = false;

        public SetupAPIWrapper()
        {
            var displayDevices = new DeviceInfoSet(DeviceClassGuid.Display);
            PciAbove4GDecodingStatus = displayDevices.Devices.Any(x => (x as DeviceInfoPci)?.Pci_Above4GDecoding == true);
            PciLargeMemoryStatus = displayDevices.Devices.Any(x => (x as DeviceInfoPci)?.Pci_LargeMemory == true);

            displayDevices.Dispose();
        }
    }
}
