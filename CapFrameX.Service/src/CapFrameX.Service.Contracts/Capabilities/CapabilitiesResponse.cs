namespace CapFrameX.Service.Contracts.Capabilities;

public sealed record CapabilitiesResponse(
    string Platform,
    string OSDescription,
    string ProcessArchitecture,
    IReadOnlyList<CapabilityDto> Capabilities);
