namespace CapFrameX.SetupAPI.NetStandard
{
    public interface ISetupAPI
    {
        bool PciAbove4GDecodingStatus { get; }

        bool PciLargeMemoryStatus { get; }
    }
}
