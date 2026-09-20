namespace CapFrameX.Service.Contracts.Capture;

public sealed record CaptureStatusDto(
    string State,
    string? Provider,
    bool ProviderAvailable,
    DateTimeOffset? StartedAt,
    string? ActiveProcessName,
    string? UnavailableReason);
