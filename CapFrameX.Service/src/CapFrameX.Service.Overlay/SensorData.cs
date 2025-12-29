namespace CapFrameX.Service.Overlay;

/// <summary>
/// Sensor data received from the CapFrameX service via named pipe
/// </summary>
public record SensorData
{
    // Process info
    public string Application { get; init; } = string.Empty;
    public uint ProcessId { get; init; }

    // Frame timing
    public double MsBetweenPresents { get; init; }
    public double MsInPresentApi { get; init; }
    public double MsUntilDisplayed { get; init; }
    public double MsBetweenDisplayChange { get; init; }
    public double MsUntilRenderComplete { get; init; }
    public double MsUntilRenderStart { get; init; }
    public double MsSinceInput { get; init; }
    public double MsGpuActive { get; init; }
    public bool Dropped { get; init; }

    // GPU telemetry
    public double? GpuPowerW { get; init; }
    public double? GpuPowerLimitW { get; init; }
    public double? GpuVoltageV { get; init; }
    public double? GpuFrequencyMhz { get; init; }
    public double? GpuTemperatureC { get; init; }
    public double? GpuUsage { get; init; }
    public double? GpuFanRpm { get; init; }
    public double? GpuFan2Rpm { get; init; }

    // VRAM telemetry
    public double? VramPowerW { get; init; }
    public double? VramVoltageV { get; init; }
    public double? VramFrequencyMhz { get; init; }
    public double? VramTemperatureC { get; init; }
    public ulong? VramTotalB { get; init; }
    public ulong? VramUsedB { get; init; }

    // CPU telemetry
    public double? CpuPowerW { get; init; }
    public double? CpuPowerLimitW { get; init; }
    public double? CpuFrequencyMhz { get; init; }
    public double? CpuTemperatureC { get; init; }
    public double? CpuUsage { get; init; }

    // Timestamp
    public ulong QpcTime { get; init; }
    public double TimeInSeconds { get; init; }
}
