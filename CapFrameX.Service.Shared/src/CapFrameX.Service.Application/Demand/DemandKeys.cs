namespace CapFrameX.Service.Application.Demand;

/// <summary>
/// The demand keys the service reacts to. Everything expensive hangs off one of these, so that a
/// mode showing only an overlay never starts a frame source and never touches low-level hardware.
/// </summary>
public static class DemandKeys
{
    /// <summary>Frames of one process are needed.</summary>
    /// <param name="processId">Process being observed.</param>
    public static string Frames(int processId) => $"frames[{processId}]";

    /// <summary>The list of presenting processes is being shown.</summary>
    public const string ProcessList = "process.list";

    /// <summary>One sensor is needed.</summary>
    /// <param name="sensorId">Platform-specific sensor id.</param>
    public static string Sensor(string sensorId) => $"sensor[{sensorId}]";

    /// <summary>The overlay is applied and visible.</summary>
    public const string Overlay = "overlay";

    /// <summary>The record library is being browsed, which keeps the indexer awake.</summary>
    public const string RecordIndex = "records.index";
}
