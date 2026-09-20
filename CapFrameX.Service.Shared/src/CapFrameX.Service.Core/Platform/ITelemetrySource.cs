namespace CapFrameX.Service.Core.Platform;

/// <summary>
/// Platform port for sensor data: the LibreHardwareMonitor stack with PawnIO on Windows, kernel
/// interfaces with NVML on Linux. Implementations read only the sensors they are asked for, so the
/// demand registry can keep unused hardware groups untouched.
/// </summary>
public interface ITelemetrySource : IAsyncDisposable
{
    /// <summary>Whether sensors can be read on this machine, with a reason when they cannot.</summary>
    PlatformAvailability Availability { get; }

    /// <summary>Everything this source offers, including sensors without a well-known metric key.</summary>
    /// <param name="cancellationToken">Cancels the enumeration.</param>
    ValueTask<IReadOnlyList<SensorDescriptor>> GetSensorsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads the named sensors. Ids the source does not know are returned with a <c>null</c> value
    /// rather than omitted, so callers can tell "unknown" from "not read this time".
    /// </summary>
    /// <param name="sensorIds">Sensors to read.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    ValueTask<IReadOnlyList<SensorSample>> ReadAsync(
        IReadOnlyCollection<string> sensorIds,
        CancellationToken cancellationToken = default);
}
