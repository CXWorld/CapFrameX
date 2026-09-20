namespace CapFrameX.Service.Contracts.Bridge;

public sealed record BridgeEventEnvelope(
    string Type,
    int Version,
    long Sequence,
    DateTimeOffset Timestamp,
    object Payload);
