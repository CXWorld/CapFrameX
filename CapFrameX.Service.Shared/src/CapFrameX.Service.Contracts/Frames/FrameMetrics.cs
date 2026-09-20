namespace CapFrameX.Service.Contracts.Frames;

/// <summary>
/// Optional per-frame metrics beyond the always-present time stamp and present interval.
/// A frame source declares what it can deliver; the frontend turns missing flags into an
/// explanation instead of drawing zeros.
/// </summary>
[Flags]
public enum FrameMetrics
{
    /// <summary>Only <see cref="FrameSample.TimeInSeconds"/> and <see cref="FrameSample.MsBetweenPresents"/>.</summary>
    None = 0,

    /// <summary>Display-side interval (PresentMon MsBetweenDisplayChange, Vulkan VK_EXT_present_timing).</summary>
    DisplayChange = 1 << 0,

    /// <summary>Time from present call until rendering finished.</summary>
    RenderComplete = 1 << 1,

    /// <summary>Time from present call until the frame was displayed.</summary>
    UntilDisplayed = 1 << 2,

    /// <summary>GPU busy time attributable to the frame.</summary>
    GpuBusy = 1 << 3,

    /// <summary>CPU busy time attributable to the frame.</summary>
    CpuBusy = 1 << 4,

    /// <summary>Input-to-display latency as reported by the source.</summary>
    PcLatency = 1 << 5,

    /// <summary>Animation error (deviation between simulation and display pacing).</summary>
    AnimationError = 1 << 6,

    /// <summary>Whether the present was dropped.</summary>
    Dropped = 1 << 7,

    /// <summary>Presentation mode of the swap chain.</summary>
    PresentMode = 1 << 8,

    /// <summary>Frame type, which distinguishes generated frames from application frames.</summary>
    FrameType = 1 << 9,

    /// <summary>Swap chain identity, which allows filtering a process with several swap chains.</summary>
    SwapChain = 1 << 10,
}
