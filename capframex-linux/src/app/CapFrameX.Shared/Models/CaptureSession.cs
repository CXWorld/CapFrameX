namespace CapFrameX.Shared.Models;

/// <summary>
/// Represents a captured frametime recording session
/// </summary>
public class CaptureSession
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string GameName { get; set; } = string.Empty;
    public string GpuName { get; set; } = string.Empty;
    public string CpuName { get; set; } = string.Empty;
    public string Resolution { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public TimeSpan Duration => EndTime - StartTime;
    public string FilePath { get; set; } = string.Empty;
    public string Comment { get; set; } = string.Empty;

    public List<FrameData> Frames { get; set; } = new();

    public int FrameCount => Frames.Count;
}

/// <summary>
/// Metadata for a session (without frame data, for listing)
/// </summary>
public class SessionMetadata
{
    public string Id { get; set; } = string.Empty;
    public string GameName { get; set; } = string.Empty;
    public string GpuName { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public TimeSpan Duration { get; set; }
    public int FrameCount { get; set; }
    public string FilePath { get; set; } = string.Empty;

    // Quick statistics
    public float AverageFps { get; set; }
    public float P1Fps { get; set; }
    public float P01Fps { get; set; }
}
