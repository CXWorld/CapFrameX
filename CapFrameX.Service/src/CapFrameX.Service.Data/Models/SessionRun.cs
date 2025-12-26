namespace CapFrameX.Service.Data.Models;

/// <summary>
/// Represents a single benchmark run within a session.
/// Contains frame capture data and hardware sensor data.
/// Based on legacy ISessionRun, ISessionCaptureData, and ISessionSensorData.
/// </summary>
public class SessionRun
{
    /// <summary>
    /// Unique identifier for the run
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Session this run belongs to
    /// </summary>
    public Guid SessionId { get; set; }
    public Session Session { get; set; } = null!;

    /// <summary>
    /// Hash from legacy system for backwards compatibility
    /// </summary>
    public string? Hash { get; set; }

    /// <summary>
    /// Creation timestamp
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// PresentMon runtime version used for capture
    /// </summary>
    public string? PresentMonRuntime { get; set; }

    /// <summary>
    /// Sample time in seconds (total duration)
    /// </summary>
    public double SampleTime { get; set; }

    // Capture Data - stored as JSON to handle large arrays efficiently
    /// <summary>
    /// Frame timing data as JSON (TimeInSeconds, MsBetweenPresents, etc.)
    /// Contains arrays: TimeInSeconds[], MsBetweenPresents[], MsBetweenDisplayChange[],
    /// Dropped[], MsInPresentApi[], MsUntilRenderComplete[], MsUntilDisplayed[], etc.
    /// </summary>
    public string CaptureDataJson { get; set; } = "{}";

    /// <summary>
    /// Hardware sensor data as JSON (CPU/GPU temps, clocks, usage, power)
    /// Dictionary-based format from SessionSensorData2
    /// </summary>
    public string SensorDataJson { get; set; } = "{}";

    /// <summary>
    /// RTSS frame times as JSON array (if available)
    /// </summary>
    public string? RtssFrameTimesJson { get; set; }

    /// <summary>
    /// PMD GPU power data as JSON array (if available)
    /// </summary>
    public string? PmdGpuPowerJson { get; set; }

    /// <summary>
    /// PMD CPU power data as JSON array (if available)
    /// </summary>
    public string? PmdCpuPowerJson { get; set; }

    /// <summary>
    /// PMD system power data as JSON array (if available)
    /// </summary>
    public string? PmdSystemPowerJson { get; set; }

    // Pre-computed metrics for fast queries
    /// <summary>
    /// Maximum FPS
    /// </summary>
    public double? MaxFps { get; set; }

    /// <summary>
    /// 99th percentile FPS
    /// </summary>
    public double? P99Fps { get; set; }

    /// <summary>
    /// 95th percentile FPS
    /// </summary>
    public double? P95Fps { get; set; }

    /// <summary>
    /// Average FPS
    /// </summary>
    public double? AverageFps { get; set; }

    /// <summary>
    /// Median FPS
    /// </summary>
    public double? MedianFps { get; set; }

    /// <summary>
    /// 5th percentile FPS
    /// </summary>
    public double? P5Fps { get; set; }

    /// <summary>
    /// 1st percentile FPS
    /// </summary>
    public double? P1Fps { get; set; }

    /// <summary>
    /// 0.1 percentile FPS
    /// </summary>
    public double? P0_1Fps { get; set; }

    /// <summary>
    /// 0.01 percentile FPS (minimum)
    /// </summary>
    public double? P0_01Fps { get; set; }

    /// <summary>
    /// Average CPU temperature (°C)
    /// </summary>
    public double? AvgCpuTemp { get; set; }

    /// <summary>
    /// Average GPU temperature (°C)
    /// </summary>
    public double? AvgGpuTemp { get; set; }

    /// <summary>
    /// Average CPU power (W)
    /// </summary>
    public double? AvgCpuPower { get; set; }

    /// <summary>
    /// Average GPU power (W)
    /// </summary>
    public double? AvgGpuPower { get; set; }

    /// <summary>
    /// Average CPU usage (%)
    /// </summary>
    public double? AvgCpuUsage { get; set; }

    /// <summary>
    /// Average GPU usage (%)
    /// </summary>
    public double? AvgGpuUsage { get; set; }
}
