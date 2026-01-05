namespace CapFrameX.Shared.Models;

/// <summary>
/// Represents a single frame timing measurement
/// </summary>
public record FrameData
{
    public ulong FrameNumber { get; init; }
    public ulong TimestampNs { get; init; }
    public float FrametimeMs { get; init; }
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
    public float FrametimeMs { get; init; }
    public float Fps { get; init; }
}
