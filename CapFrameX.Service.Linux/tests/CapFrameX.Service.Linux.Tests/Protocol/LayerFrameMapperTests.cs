using CapFrameX.Service.Contracts.Frames;
using CapFrameX.Service.Linux.Capture.Protocol;

namespace CapFrameX.Service.Linux.Tests.Protocol;

/// <summary>
/// Pins down how the layer's absolute nanosecond stamps and its "zero means unmeasured"
/// convention become a platform-neutral frame.
/// </summary>
public sealed class LayerFrameMapperTests
{
    private static LayerFrameData Frame(ulong timestampNs, float frametimeMs = 16.6f) =>
        new(1, timestampNs, frametimeMs, 60f, 4711, 0, 0, 0, 0);

    [Fact]
    public void First_frame_defines_the_time_origin()
    {
        var mapper = new LayerFrameMapper();

        var first = mapper.Map(Frame(5_000_000_000));
        var second = mapper.Map(Frame(5_500_000_000));

        Assert.Equal(0d, first.TimeInSeconds);
        Assert.Equal(0.5d, second.TimeInSeconds, 9);
    }

    [Fact]
    public void Reset_starts_a_new_capture_at_zero()
    {
        var mapper = new LayerFrameMapper();
        mapper.Map(Frame(5_000_000_000));

        mapper.Reset();
        var afterReset = mapper.Map(Frame(9_000_000_000));

        Assert.Equal(0d, afterReset.TimeInSeconds);
    }

    [Fact]
    public void Cpu_side_frametime_and_process_are_carried_over()
    {
        var mapper = new LayerFrameMapper();

        var sample = mapper.Map(Frame(1_000_000_000, frametimeMs: 8.25f));

        Assert.Equal(4711, sample.ProcessId);
        Assert.Equal(8.25d, sample.MsBetweenPresents, 5);
    }

    [Fact]
    public void Without_present_timing_the_display_side_metrics_are_absent()
    {
        var mapper = new LayerFrameMapper();

        var sample = mapper.Map(Frame(1_000_000_000));

        Assert.Null(sample.MsBetweenDisplayChange);
        Assert.Null(sample.MsUntilRenderComplete);
        Assert.Null(sample.MsUntilDisplayed);
        Assert.Equal(FrameMetrics.None, LayerFrameMapper.MetricsFor(presentTimingSupported: false));
    }

    [Fact]
    public void With_present_timing_the_display_side_metrics_are_mapped()
    {
        var mapper = new LayerFrameMapper();
        var frame = new LayerFrameData(1, 1_000_000_000, 16.6f, 60f, 4711, 1_000_100_000, 3.5f, 4.25f, 16.7f);

        var sample = mapper.Map(frame);

        Assert.Equal(16.7d, sample.MsBetweenDisplayChange!.Value, 5);
        Assert.Equal(3.5d, sample.MsUntilRenderComplete!.Value, 5);
        Assert.Equal(4.25d, sample.MsUntilDisplayed!.Value, 5);
        Assert.Equal(
            FrameMetrics.DisplayChange | FrameMetrics.RenderComplete | FrameMetrics.UntilDisplayed,
            LayerFrameMapper.MetricsFor(presentTimingSupported: true));
    }

    [Fact]
    public void Metrics_the_layer_never_measures_stay_absent()
    {
        var mapper = new LayerFrameMapper();

        var sample = mapper.Map(Frame(1_000_000_000));

        Assert.Null(sample.MsPcLatency);
        Assert.Null(sample.MsGpuBusy);
        Assert.Null(sample.MsCpuBusy);
        Assert.Null(sample.FrameType);
    }
}
