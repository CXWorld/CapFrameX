namespace CapFrameX.Shared.Models;

/// <summary>
/// Represents a single frame timing measurement
/// </summary>
public record FrameData
{
    public ulong FrameNumber { get; init; }
    public ulong TimestampNs { get; init; }
    public float FrametimeMs { get; init; }              // CPU sampled frametime
    public float MsUntilRenderComplete { get; init; }    // Time until render complete (0 if not available)
    public float MsUntilDisplayed { get; init; }         // Time until displayed (0 if not available)
    public float ActualFrametimeMs { get; init; }        // From VK_EXT_present_timing (0 if not available)

    /// <summary>
    /// Whether actual present timing data is available for this frame
    /// </summary>
    public bool HasActualTiming => ActualFrametimeMs > 0;

    public float Fps => FrametimeMs > 0 ? 1000f / FrametimeMs : 0;

    public DateTime Timestamp => DateTime.UnixEpoch.AddTicks((long)(TimestampNs / 100));
}

/// <summary>
/// Frame data point received via IPC
/// </summary>
public record FrameDataPoint
{
    public ulong FrameNumber { get; init; }
    public ulong TimestampNs { get; init; }
    public float FrametimeMs { get; init; }           // CPU sampled frametime
    public float Fps { get; init; }
    public int Pid { get; init; }                      // Source process ID
    public ulong ActualPresentTimeNs { get; init; }    // From VK_EXT_present_timing (0 if not available)
    public float MsUntilRenderComplete { get; init; } // Time until render complete (0 if not available)
    public float MsUntilDisplayed { get; init; }      // Time until displayed (0 if not available)
    public float ActualFrametimeMs { get; init; }      // Frametime from actual present timing (0 if not available)

    /// <summary>
    /// Whether actual present timing data is available for this frame
    /// </summary>
    public bool HasActualTiming => ActualFrametimeMs > 0;
}
