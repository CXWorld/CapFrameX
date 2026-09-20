namespace CapFrameX.Service.Records;

/// <summary>
/// What the record list needs about one capture, without loading its frame data again.
/// </summary>
/// <param name="FilePath">Where the capture lives.</param>
/// <param name="Name">Display name, taken from the file name.</param>
/// <param name="GameName">Game as the capture recorded it.</param>
/// <param name="ProcessName">Executable name without extension.</param>
/// <param name="CreatedAt">When the capture was taken.</param>
/// <param name="DurationSeconds">Length of the capture across all its runs.</param>
/// <param name="RunCount">Number of runs in the capture.</param>
/// <param name="FrameCount">Frames across all runs.</param>
/// <param name="Sparkline">Decimated frame times for the list, see <see cref="FrametimeSparkline"/>.</param>
/// <param name="Processor">CPU the capture was taken on.</param>
/// <param name="Gpu">GPU the capture was taken on.</param>
/// <param name="HasPcLatency">Whether the capture carries input-to-display latency.</param>
/// <param name="HasDisplayChange">Whether the capture carries display-side frame times.</param>
public sealed record RecordSummary(
    string FilePath,
    string Name,
    string? GameName,
    string? ProcessName,
    DateTimeOffset CreatedAt,
    double DurationSeconds,
    int RunCount,
    int FrameCount,
    double[] Sparkline,
    string? Processor,
    string? Gpu,
    bool HasPcLatency,
    bool HasDisplayChange);
