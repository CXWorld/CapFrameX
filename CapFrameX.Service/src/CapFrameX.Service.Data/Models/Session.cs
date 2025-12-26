namespace CapFrameX.Service.Data.Models;

/// <summary>
/// Represents a benchmark session containing hardware/game info and multiple runs.
/// Based on legacy ISession and ISessionInfo structures.
/// </summary>
public class Session
{
    /// <summary>
    /// Unique identifier for the session
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Hash from legacy system for backwards compatibility
    /// </summary>
    public string? Hash { get; set; }

    /// <summary>
    /// Suite this session belongs to
    /// </summary>
    public Guid SuiteId { get; set; }
    public Suite Suite { get; set; } = null!;

    /// <summary>
    /// Creation timestamp
    /// </summary>
    public DateTime CreatedAt { get; set; }

    // Game/Process Information
    /// <summary>
    /// Name of the game or application
    /// </summary>
    public string GameName { get; set; } = string.Empty;

    /// <summary>
    /// Process name
    /// </summary>
    public string ProcessName { get; set; } = string.Empty;

    /// <summary>
    /// Optional comment or notes
    /// </summary>
    public string? Comment { get; set; }

    // Hardware Information
    /// <summary>
    /// CPU model/name
    /// </summary>
    public string Processor { get; set; } = string.Empty;

    /// <summary>
    /// Motherboard model
    /// </summary>
    public string? Motherboard { get; set; }

    /// <summary>
    /// System RAM configuration
    /// </summary>
    public string? SystemRam { get; set; }

    /// <summary>
    /// GPU model/name
    /// </summary>
    public string Gpu { get; set; } = string.Empty;

    /// <summary>
    /// Number of GPUs
    /// </summary>
    public int? GpuCount { get; set; }

    /// <summary>
    /// GPU core clock (MHz)
    /// </summary>
    public int? GpuCoreClock { get; set; }

    /// <summary>
    /// GPU memory clock (MHz)
    /// </summary>
    public int? GpuMemoryClock { get; set; }

    // Driver Information
    /// <summary>
    /// Base driver version
    /// </summary>
    public string? BaseDriverVersion { get; set; }

    /// <summary>
    /// Driver package version
    /// </summary>
    public string? DriverPackage { get; set; }

    /// <summary>
    /// GPU driver version
    /// </summary>
    public string? GpuDriverVersion { get; set; }

    // System Information
    /// <summary>
    /// Operating system
    /// </summary>
    public string Os { get; set; } = string.Empty;

    /// <summary>
    /// Graphics API (DX11, DX12, Vulkan, etc.)
    /// </summary>
    public string? ApiInfo { get; set; }

    /// <summary>
    /// Resizable BAR enabled
    /// </summary>
    public bool? ResizableBar { get; set; }

    /// <summary>
    /// Windows Game Mode enabled
    /// </summary>
    public bool? WinGameMode { get; set; }

    /// <summary>
    /// Hardware Accelerated GPU Scheduling enabled
    /// </summary>
    public bool? Hags { get; set; }

    /// <summary>
    /// Presentation mode (Fullscreen, Borderless, Windowed)
    /// </summary>
    public string? PresentationMode { get; set; }

    /// <summary>
    /// Display resolution
    /// </summary>
    public string? ResolutionInfo { get; set; }

    /// <summary>
    /// Benchmark runs for this session
    /// </summary>
    public ICollection<SessionRun> Runs { get; set; } = new List<SessionRun>();
}
