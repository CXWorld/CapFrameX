using CapFrameX.Service.Contracts.Frames;

namespace CapFrameX.Service.Linux.Capture.Protocol;

/// <summary>
/// Maps what the Vulkan layer reports onto the platform-neutral <see cref="FrameSample"/>.
/// </summary>
/// <remarks>
/// The layer's time stamps are absolute nanoseconds; a capture needs seconds relative to its own
/// start, so the first frame of a subscription sets the origin. Fields the layer leaves at zero
/// are not measurements and become <c>null</c>, which is how the frontend learns to say
/// "this system has no display timing" instead of drawing a flat line at zero.
/// </remarks>
public sealed class LayerFrameMapper
{
    private ulong? _originNs;

    /// <summary>
    /// Optional metrics this source can deliver. Display-side timing depends on the driver, so it
    /// is decided per subscription from the layer's hello message rather than assumed.
    /// </summary>
    /// <param name="presentTimingSupported">Whether the layer reported VK_EXT_present_timing.</param>
    public static FrameMetrics MetricsFor(bool presentTimingSupported) =>
        presentTimingSupported
            ? FrameMetrics.DisplayChange | FrameMetrics.RenderComplete | FrameMetrics.UntilDisplayed
            : FrameMetrics.None;

    /// <summary>Resets the time origin; call when a subscription starts.</summary>
    public void Reset() => _originNs = null;

    /// <summary>Maps one frame, establishing the time origin from the first frame seen.</summary>
    /// <param name="frame">Frame as the layer reported it.</param>
    public FrameSample Map(in LayerFrameData frame)
    {
        _originNs ??= frame.TimestampNs;

        return new FrameSample
        {
            ProcessId = frame.ProcessId,
            TimeInSeconds = (frame.TimestampNs - _originNs.Value) / 1_000_000_000d,
            MsBetweenPresents = frame.FrametimeMs,
            MsBetweenDisplayChange = Measured(frame.ActualFrametimeMs),
            MsUntilRenderComplete = Measured(frame.MsUntilRenderComplete),
            MsUntilDisplayed = Measured(frame.MsUntilDisplayed),
        };
    }

    /// <summary>Zero means "not measured" in the layer's frame data, not "measured as zero".</summary>
    private static double? Measured(float value) => value > 0f ? value : null;
}
