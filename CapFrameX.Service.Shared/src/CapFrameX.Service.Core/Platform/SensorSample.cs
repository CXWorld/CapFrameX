namespace CapFrameX.Service.Core.Platform;

/// <summary>One sensor reading.</summary>
/// <param name="SensorId">Id of the sensor that produced the value.</param>
/// <param name="Value">The value, or <c>null</c> when the sensor could not be read this time.</param>
/// <param name="Timestamp">When the value was read.</param>
public readonly record struct SensorSample(string SensorId, double? Value, DateTimeOffset Timestamp);
