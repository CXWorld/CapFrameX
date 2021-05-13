namespace CapFrameX.SetupAPI.NetStandard
{
    public interface ISetupAPI
    {
        EPciDeviceInfoStatus PciAbove4GDecodingStatus { get; }

        EPciDeviceInfoStatus PciLargeMemoryStatus { get; }
    }
}
