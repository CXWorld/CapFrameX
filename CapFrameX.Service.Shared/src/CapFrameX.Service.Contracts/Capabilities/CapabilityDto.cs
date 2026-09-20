namespace CapFrameX.Service.Contracts.Capabilities;

public sealed record CapabilityDto(
    string Id,
    string Name,
    string State,
    string Scope,
    string? Reason = null);
