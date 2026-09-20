namespace CapFrameX.Service.Contracts.Records;

/// <summary>
/// One capture as the record list shows it.
/// </summary>
/// <param name="Id">Identity of the indexed record.</param>
/// <param name="Name">Display name, taken from the file name.</param>
/// <param name="GameName">Game as the capture recorded it.</param>
/// <param name="ProcessName">Executable name without extension.</param>
/// <param name="CreatedAt">When the capture was taken.</param>
/// <param name="DurationSeconds">Length of the capture across all its runs.</param>
/// <param name="RunCount">Number of runs in the capture.</param>
/// <param name="FrameCount">Frames across all runs.</param>
/// <param name="Sparkline">Decimated frame times for the list.</param>
/// <param name="Processor">CPU the capture was taken on.</param>
/// <param name="Gpu">GPU the capture was taken on.</param>
/// <param name="HasPcLatency">Whether the capture carries input-to-display latency.</param>
/// <param name="HasDisplayChange">Whether the capture carries display-side frame times.</param>
/// <param name="AverageFps">Average frame rate, once the record has been analysed.</param>
/// <param name="P1Fps">1st percentile frame rate, once the record has been analysed.</param>
/// <param name="P99Fps">99th percentile frame rate, once the record has been analysed.</param>
public sealed record RecordSummaryDto(
    Guid Id,
    string Name,
    string? GameName,
    string? ProcessName,
    DateTimeOffset CreatedAt,
    double DurationSeconds,
    int RunCount,
    int FrameCount,
    IReadOnlyList<double> Sparkline,
    string? Processor,
    string? Gpu,
    bool HasPcLatency,
    bool HasDisplayChange,
    double? AverageFps,
    double? P1Fps,
    double? P99Fps);
