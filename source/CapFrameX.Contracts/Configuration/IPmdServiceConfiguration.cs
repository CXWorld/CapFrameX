namespace CapFrameX.Contracts.Configuration
{
    public interface IPmdServiceConfiguration
    {
        bool UseVirtualMode { get; set; }

        int DownSamplingSize { get; set; }

        string DownSamplingMode { get; set; }

        int PmdChartRefreshPeriod { get; set; }
    }
}
