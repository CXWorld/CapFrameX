namespace CapFrameX.Contracts.Configuration
{
    public interface IPmdServiceConfiguration
    {
        bool UseVirtualMode { get; set; }

        int DownSamplingSize { get; set; }

        EDownSamplingMode DownSamplingMode { get; set; }
    }
}
