using CapFrameX.Service.Capture.Frames;
using CapFrameX.Service.Contracts.Frames;

namespace CapFrameX.Service.Capture.Tests.Frames;

/// <summary>
/// Pins the header-driven column mapping down against the two layouts PresentMon 2.x produces for
/// CapFrameX, plus the shapes that a future PresentMon may hand us.
/// </summary>
public sealed class PresentMonColumnLayoutTests
{
    internal const string HeaderWithPcLatency =
        "Application,ProcessID,SwapChainAddress,PresentRuntime,SyncInterval,PresentFlags,AllowsTearing,PresentMode," +
        "FrameType,TimeInSeconds,MsBetweenSimulationStart,MsBetweenPresents,MsBetweenDisplayChange,MsInPresentAPI," +
        "MsRenderPresentLatency,MsUntilDisplayed,MsPCLatency,CPUStartQPCTimeInMs,MsBetweenAppStart,MsCPUBusy," +
        "MsCPUWait,MsGPULatency,MsGPUTime,MsGPUBusy,MsGPUWait,MsAnimationError,AnimationTime,MsFlipDelay," +
        "MsInstrumentedLatency";

    internal const string HeaderWithoutPcLatency =
        "Application,ProcessID,SwapChainAddress,PresentRuntime,SyncInterval,PresentFlags,AllowsTearing,PresentMode," +
        "FrameType,TimeInSeconds,MsBetweenSimulationStart,MsBetweenPresents,MsBetweenDisplayChange,MsInPresentAPI," +
        "MsRenderPresentLatency,MsUntilDisplayed,CPUStartQPCTimeInMs,MsBetweenAppStart,MsCPUBusy,MsCPUWait," +
        "MsGPULatency,MsGPUTime,MsGPUBusy,MsGPUWait,MsAnimationError,AnimationTime,MsFlipDelay,MsInstrumentedLatency";

    [Fact]
    public void Reads_indices_from_the_header()
    {
        var layout = PresentMonColumnLayout.FromHeader(HeaderWithPcLatency);

        Assert.Equal(29, layout.ColumnCount);
        Assert.Equal(1, layout.IndexOf(PresentMonColumns.ProcessId));
        Assert.Equal(9, layout.IndexOf(PresentMonColumns.TimeInSeconds));
        Assert.Equal(11, layout.IndexOf(PresentMonColumns.MsBetweenPresents));
        Assert.Equal(16, layout.IndexOf(PresentMonColumns.MsPcLatency));
        Assert.Equal(23, layout.IndexOf(PresentMonColumns.MsGpuBusy));
    }

    [Fact]
    public void Absent_column_has_no_index()
    {
        var layout = PresentMonColumnLayout.FromHeader(HeaderWithoutPcLatency);

        Assert.Equal(-1, layout.IndexOf(PresentMonColumns.MsPcLatency));
    }

    [Fact]
    public void Column_order_does_not_matter()
    {
        var reordered = string.Join(',', HeaderWithPcLatency.Split(',').Reverse());

        var layout = PresentMonColumnLayout.FromHeader(reordered);

        Assert.Equal(27, layout.IndexOf(PresentMonColumns.ProcessId));
        Assert.Equal(19, layout.IndexOf(PresentMonColumns.TimeInSeconds));
        Assert.Equal(PresentMonColumnLayout.FromHeader(HeaderWithPcLatency).AvailableMetrics, layout.AvailableMetrics);
    }

    [Fact]
    public void Unknown_columns_are_ignored()
    {
        var layout = PresentMonColumnLayout.FromHeader(HeaderWithPcLatency + ",SomeFutureColumn");

        Assert.Equal(30, layout.ColumnCount);
        Assert.Equal(1, layout.IndexOf(PresentMonColumns.ProcessId));
    }

    [Fact]
    public void Surrounding_whitespace_in_the_header_is_tolerated()
    {
        var layout = PresentMonColumnLayout.FromHeader(" Application , ProcessID , TimeInSeconds , MsBetweenPresents ");

        Assert.Equal(1, layout.IndexOf(PresentMonColumns.ProcessId));
        Assert.Equal(3, layout.IndexOf(PresentMonColumns.MsBetweenPresents));
    }

    [Fact]
    public void Full_layout_advertises_every_metric_it_carries()
    {
        var layout = PresentMonColumnLayout.FromHeader(HeaderWithPcLatency);

        Assert.Equal(
            FrameMetrics.DisplayChange | FrameMetrics.RenderComplete | FrameMetrics.UntilDisplayed |
            FrameMetrics.GpuBusy | FrameMetrics.CpuBusy | FrameMetrics.PcLatency |
            FrameMetrics.AnimationError | FrameMetrics.PresentMode | FrameMetrics.FrameType |
            FrameMetrics.SwapChain,
            layout.AvailableMetrics);
    }

    [Fact]
    public void Layout_without_pc_latency_does_not_advertise_it()
    {
        var layout = PresentMonColumnLayout.FromHeader(HeaderWithoutPcLatency);

        Assert.False(layout.AvailableMetrics.HasFlag(FrameMetrics.PcLatency));
        Assert.True(layout.AvailableMetrics.HasFlag(FrameMetrics.GpuBusy));
    }

    [Fact]
    public void Minimal_layout_advertises_nothing_optional()
    {
        var layout = PresentMonColumnLayout.FromHeader("Application,ProcessID,TimeInSeconds,MsBetweenPresents");

        Assert.Equal(FrameMetrics.None, layout.AvailableMetrics);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Application,SwapChainAddress")]
    [InlineData("ProcessID,TimeInSeconds")]
    [InlineData("ProcessID,MsBetweenPresents")]
    public void Header_without_the_mandatory_columns_is_rejected(string header)
    {
        Assert.Throws<ArgumentException>(() => PresentMonColumnLayout.FromHeader(header));
    }
}
