namespace CapFrameX.Service.Contracts.Bridge;

public static class BridgeEventTypes
{
    public const string AppHeartbeat = "app.heartbeat";
    public const string CaptureStatusChanged = "capture.statusChanged";
    public const string SensorsSnapshot = "sensors.snapshot";
    public const string SensorsDeviceChanged = "sensors.deviceChanged";
    public const string RecordsChanged = "records.changed";

    /// <summary>
    /// The user changed a setting. The payload is the settings as they now are, so a client that
    /// did not make the change does not have to ask.
    /// </summary>
    public const string SettingsChanged = "settings.changed";
}
