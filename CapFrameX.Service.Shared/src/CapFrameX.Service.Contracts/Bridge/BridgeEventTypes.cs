namespace CapFrameX.Service.Contracts.Bridge;

public static class BridgeEventTypes
{
    public const string AppHeartbeat = "app.heartbeat";
    public const string CaptureStatusChanged = "capture.statusChanged";
    public const string SensorsSnapshot = "sensors.snapshot";
    public const string SensorsDeviceChanged = "sensors.deviceChanged";
    public const string RecordsChanged = "records.changed";
}
