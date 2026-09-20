using CapFrameX.Service.Capture.Frames;
using CapFrameX.Service.Contracts.Frames;

namespace CapFrameX.Service.Capture.Tests.Frames;

/// <summary>
/// Pins the mapping against output actually produced by the shipped PresentMon build, captured on
/// 2026-09-20 from <c>PresentMon-2.5.1-x64.exe --output_stdout --no_track_input --qpc_time_ms
/// --track_pc_latency</c>.
/// </summary>
/// <remarks>
/// This layout is deliberately not the one <see cref="PresentMonKnownLayouts"/> carries: without
/// <c>--track_frame_type</c> PresentMon emits 27 columns instead of 29, with no <c>FrameType</c>
/// and no <c>MsInstrumentedLatency</c>. Reading the indices from the header is what makes that a
/// non-event; with the fixed indices the service used before, every metric from
/// <c>TimeInSeconds</c> onwards would have been read one column off.
/// </remarks>
public sealed class PresentMonRealOutputTests
{
    private const string ObservedHeader =
        "Application,ProcessID,SwapChainAddress,PresentRuntime,SyncInterval,PresentFlags,AllowsTearing,PresentMode," +
        "TimeInSeconds,MsBetweenSimulationStart,MsBetweenPresents,MsBetweenDisplayChange,MsInPresentAPI," +
        "MsRenderPresentLatency,MsUntilDisplayed,MsPCLatency,CPUStartQPCTimeInMs,MsBetweenAppStart,MsCPUBusy," +
        "MsCPUWait,MsGPULatency,MsGPUTime,MsGPUBusy,MsGPUWait,MsAnimationError,AnimationTime,MsFlipDelay";

    private const string ObservedRow =
        "CapFrameX.DevBoard.exe,28204,0x1D1027B9118,D3D9,-1,0,0,Other,435.6989,NA,107.91250000000001,NA," +
        "0.15520000000000,0.00000000000000,NA,NA,85754722.1368,107.9475,107.7923,0.1552,0.0000,0.0000,0.0000," +
        "0.0000,NA,NA,NA";

    [Fact]
    public void Observed_header_has_the_columns_it_actually_shipped_with()
    {
        var layout = PresentMonColumnLayout.FromHeader(ObservedHeader);

        Assert.Equal(27, layout.ColumnCount);
        Assert.Equal(8, layout.IndexOf(PresentMonColumns.TimeInSeconds));
        Assert.Equal(10, layout.IndexOf(PresentMonColumns.MsBetweenPresents));
        Assert.Equal(22, layout.IndexOf(PresentMonColumns.MsGpuBusy));
        Assert.Equal(-1, layout.IndexOf(PresentMonColumns.FrameType));
    }

    [Fact]
    public void Frame_type_is_reported_absent_without_the_tracking_switch()
    {
        var layout = PresentMonColumnLayout.FromHeader(ObservedHeader);

        Assert.False(layout.AvailableMetrics.HasFlag(FrameMetrics.FrameType));
        Assert.True(layout.AvailableMetrics.HasFlag(FrameMetrics.PcLatency));
        Assert.True(layout.AvailableMetrics.HasFlag(FrameMetrics.GpuBusy));
    }

    [Fact]
    public void Observed_row_maps_to_the_values_PresentMon_printed()
    {
        var mapper = new PresentMonFrameMapper(PresentMonColumnLayout.FromHeader(ObservedHeader));

        Assert.True(mapper.TryMap(ObservedRow.Split(','), out var frame));

        Assert.Equal(28204, frame.ProcessId);
        Assert.Equal(435.6989, frame.TimeInSeconds);
        Assert.Equal(107.91250000000001, frame.MsBetweenPresents);
        Assert.Equal(107.7923, frame.MsCpuBusy);
        Assert.Equal(0d, frame.MsGpuBusy);
        Assert.Equal("Other", frame.PresentMode);
        Assert.Equal(0x1D1027B9118UL, frame.SwapChainId);
        Assert.Null(frame.FrameType);
    }

    [Fact]
    public void Column_present_but_value_NA_is_not_a_measurement()
    {
        var mapper = new PresentMonFrameMapper(PresentMonColumnLayout.FromHeader(ObservedHeader));

        Assert.True(mapper.TryMap(ObservedRow.Split(','), out var frame));

        // The D3D9 window reports no display timing and no PC latency for this frame, even though
        // both columns exist - the flag says "the source can deliver it", the value says "not here".
        Assert.Null(frame.MsBetweenDisplayChange);
        Assert.Null(frame.MsPcLatency);
        Assert.Null(frame.MsUntilDisplayed);
    }
}
