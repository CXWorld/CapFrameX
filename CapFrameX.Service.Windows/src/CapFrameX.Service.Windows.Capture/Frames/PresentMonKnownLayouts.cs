namespace CapFrameX.Service.Capture.Frames;

/// <summary>
/// The column sets the pinned PresentMon build is known to produce.
/// </summary>
/// <remarks>
/// These are a starting value, not the truth: the capture service replaces the layout with the
/// header PresentMon actually writes as soon as the first output line arrives. They exist so that
/// the service can answer questions about its columns before it has ever been started, and so the
/// service degrades to "slightly stale indices" rather than to "no indices" if a future build
/// stops emitting a header.
/// </remarks>
public static class PresentMonKnownLayouts
{
    /// <summary>Columns of the pinned build when PC latency tracking is on.</summary>
    public const string WithPcLatency =
        "Application,ProcessID,SwapChainAddress,PresentRuntime,SyncInterval,PresentFlags,AllowsTearing,PresentMode," +
        "FrameType,TimeInSeconds,MsBetweenSimulationStart,MsBetweenPresents,MsBetweenDisplayChange,MsInPresentAPI," +
        "MsRenderPresentLatency,MsUntilDisplayed,MsPCLatency,CPUStartQPCTimeInMs,MsBetweenAppStart,MsCPUBusy," +
        "MsCPUWait,MsGPULatency,MsGPUTime,MsGPUBusy,MsGPUWait,MsAnimationError,AnimationTime,MsFlipDelay," +
        "MsInstrumentedLatency";

    /// <summary>Columns of the pinned build when PC latency tracking is off.</summary>
    public const string WithoutPcLatency =
        "Application,ProcessID,SwapChainAddress,PresentRuntime,SyncInterval,PresentFlags,AllowsTearing,PresentMode," +
        "FrameType,TimeInSeconds,MsBetweenSimulationStart,MsBetweenPresents,MsBetweenDisplayChange,MsInPresentAPI," +
        "MsRenderPresentLatency,MsUntilDisplayed,CPUStartQPCTimeInMs,MsBetweenAppStart,MsCPUBusy,MsCPUWait," +
        "MsGPULatency,MsGPUTime,MsGPUBusy,MsGPUWait,MsAnimationError,AnimationTime,MsFlipDelay,MsInstrumentedLatency";

    /// <summary>
    /// Start of the header line, used to tell the one header apart from the frame rows that follow
    /// it. A frame row starts with the executable name, which cannot collide with this.
    /// </summary>
    public const string HeaderPrefix = PresentMonColumns.Application + "," + PresentMonColumns.ProcessId;
}
