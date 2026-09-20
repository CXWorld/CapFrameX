namespace CapFrameX.Service.Contracts.App;

public sealed record ServiceHealthDto(
    string Status,
    string Service,
    DateTimeOffset Timestamp);
