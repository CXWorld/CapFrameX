namespace CapFrameX.Service.Capture.Frames;

/// <summary>
/// Column names of the PresentMon CSV output that CapFrameX reads. Only these are interpreted;
/// every other column is carried past untouched, so a PresentMon update that adds columns does not
/// need a code change here.
/// </summary>
public static class PresentMonColumns
{
    /// <summary>Executable name of the presenting process.</summary>
    public const string Application = "Application";

    /// <summary>Process id of the presenting process.</summary>
    public const string ProcessId = "ProcessID";

    /// <summary>Swap chain address, which identifies the swap chain within the process.</summary>
    public const string SwapChainAddress = "SwapChainAddress";

    /// <summary>Presentation mode of the swap chain.</summary>
    public const string PresentMode = "PresentMode";

    /// <summary>Frame type; identifies frames produced by frame generation.</summary>
    public const string FrameType = "FrameType";

    /// <summary>Monotonic time stamp of the present, in seconds.</summary>
    public const string TimeInSeconds = "TimeInSeconds";

    /// <summary>Interval between presents, in milliseconds.</summary>
    public const string MsBetweenPresents = "MsBetweenPresents";

    /// <summary>Interval between display changes, in milliseconds.</summary>
    public const string MsBetweenDisplayChange = "MsBetweenDisplayChange";

    /// <summary>Time from the present call until rendering completed, in milliseconds.</summary>
    public const string MsRenderPresentLatency = "MsRenderPresentLatency";

    /// <summary>PresentMon 1.x name for <see cref="MsRenderPresentLatency"/>.</summary>
    public const string MsUntilRenderComplete = "MsUntilRenderComplete";

    /// <summary>Time from the present call until the frame was displayed, in milliseconds.</summary>
    public const string MsUntilDisplayed = "MsUntilDisplayed";

    /// <summary>GPU busy time for the frame, in milliseconds.</summary>
    public const string MsGpuBusy = "MsGPUBusy";

    /// <summary>CPU busy time for the frame, in milliseconds.</summary>
    public const string MsCpuBusy = "MsCPUBusy";

    /// <summary>Input-to-display latency for the frame, in milliseconds.</summary>
    public const string MsPcLatency = "MsPCLatency";

    /// <summary>Animation error for the frame, in milliseconds.</summary>
    public const string MsAnimationError = "MsAnimationError";

    /// <summary>Marker PresentMon writes into the first column when it reports an error.</summary>
    public const string ErrorMarker = "<error>";

    /// <summary>Placeholder PresentMon writes for a value it could not determine.</summary>
    public const string NotAvailable = "NA";
}
