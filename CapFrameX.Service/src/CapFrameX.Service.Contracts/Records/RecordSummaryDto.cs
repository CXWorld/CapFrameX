namespace CapFrameX.Service.Contracts.Records;

public sealed record RecordSummaryDto(
    Guid Id,
    string Name,
    string? GameName,
    string? ProcessName,
    DateTimeOffset CreatedAt,
    double? AverageFps,
    double? P1Fps,
    double? P99Fps,
    int RunCount);
