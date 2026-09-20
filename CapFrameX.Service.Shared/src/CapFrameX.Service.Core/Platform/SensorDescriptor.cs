namespace CapFrameX.Service.Core.Platform;

/// <summary>
/// One sensor a telemetry source offers. <see cref="Id"/> stays platform-specific;
/// <see cref="MetricKey"/> is how the sensor joins the platform-neutral vocabulary, and is
/// <c>null</c> for sensors that have no counterpart there.
/// </summary>
/// <param name="Id">Stable, platform-specific sensor id.</param>
/// <param name="Name">Display name.</param>
/// <param name="HardwareName">Hardware the sensor belongs to.</param>
/// <param name="Unit">Unit of the values, for example <c>%</c>, <c>W</c> or <c>°C</c>.</param>
/// <param name="MetricKey">Well-known metric key, when this sensor serves one.</param>
/// <param name="MinInterval">Shortest interval at which the value actually changes.</param>
public sealed record SensorDescriptor(
    string Id,
    string Name,
    string HardwareName,
    string Unit,
    string? MetricKey = null,
    TimeSpan? MinInterval = null);
