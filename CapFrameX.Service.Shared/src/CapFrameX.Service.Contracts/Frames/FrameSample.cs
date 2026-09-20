namespace CapFrameX.Service.Contracts.Frames;

/// <summary>
/// One present, normalised across frame sources. PresentMon on Windows and the Vulkan layer on
/// Linux fill different subsets; everything a source cannot measure stays <c>null</c> and is
/// absent from <see cref="FrameSourceInfo.AvailableMetrics"/>.
/// </summary>
/// <remarks>
/// A value type on purpose: at a few thousand presents per second this sits on the hot path
/// between the source and the capture buffer.
/// </remarks>
public readonly record struct FrameSample
{
    /// <summary>Process that issued the present.</summary>
    public int ProcessId { get; init; }

    /// <summary>Monotonic time stamp in seconds, relative to the start of the source.</summary>
    public double TimeInSeconds { get; init; }

    /// <summary>Interval between this present and the previous one, in milliseconds.</summary>
    public double MsBetweenPresents { get; init; }

    /// <summary>Interval between display changes, in milliseconds.</summary>
    public double? MsBetweenDisplayChange { get; init; }

    /// <summary>Time from the present call until rendering completed, in milliseconds.</summary>
    public double? MsUntilRenderComplete { get; init; }

    /// <summary>Time from the present call until the frame was displayed, in milliseconds.</summary>
    public double? MsUntilDisplayed { get; init; }

    /// <summary>GPU busy time for this frame, in milliseconds.</summary>
    public double? MsGpuBusy { get; init; }

    /// <summary>CPU busy time for this frame, in milliseconds.</summary>
    public double? MsCpuBusy { get; init; }

    /// <summary>Input-to-display latency for this frame, in milliseconds.</summary>
    public double? MsPcLatency { get; init; }

    /// <summary>Animation error for this frame, in milliseconds.</summary>
    public double? MsAnimationError { get; init; }

    /// <summary>Whether the present was dropped.</summary>
    public bool? Dropped { get; init; }

    /// <summary>Presentation mode as reported by the source.</summary>
    public string? PresentMode { get; init; }

    /// <summary>Frame type as reported by the source; identifies generated frames.</summary>
    public string? FrameType { get; init; }

    /// <summary>Swap chain this present belongs to.</summary>
    public ulong? SwapChainId { get; init; }
}
