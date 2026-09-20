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
    /// Identity of the capture itself, as CapFrameX computes it over its runs.
    /// </summary>
    /// <remarks>
    /// What tells two rows apart from the same capture arriving twice - through an import of a
    /// folder that was already watched, or the same file imported from two places. It comes from
    /// the capture rather than from the file, so a copy under another name is still recognised.
    /// </remarks>
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
    /// <summary>
    /// Where the capture file lives, or <c>null</c> for a session the service recorded itself.
    /// </summary>
    /// <remarks>
    /// The file is the record; this table is an index over it. Copying frame data in here would
    /// double the storage and create two versions of the same capture that can drift apart - and
    /// CapFrameX 1.x keeps writing those files.
    /// </remarks>
    public string? SourceFilePath { get; set; }

    /// <summary>Size of the capture file when it was indexed.</summary>
    public long? SourceFileSize { get; set; }

    /// <summary>Last write time of the capture file when it was indexed, in UTC.</summary>
    public DateTime? SourceModifiedUtc { get; set; }

    /// <summary>
    /// Where an imported capture was read from, if it was imported.
    /// </summary>
    /// <remarks>
    /// Provenance, not a source: the frames of an imported record live in this database, and the
    /// file it came from may be gone or on a drive that is not attached. Deliberately not
    /// <see cref="SourceFilePath"/>, because the folder scan owns every row that has one and would
    /// drop an imported record the moment it looked in a folder the file is not in.
    /// </remarks>
    public string? ImportedFrom { get; set; }

    /// <summary>When this row last changed, in UTC.</summary>
    /// <remarks>
    /// What tells a cached copy of a record apart from the row it was made from, now that a record
    /// has no file whose size and time could say so.
    /// </remarks>
    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// Version of the indexer that wrote this row, so a changed projection can re-index without a
    /// schema migration.
    /// </summary>
    public int IndexVersion { get; set; }

    /// <summary>Length of the capture across its runs, in seconds.</summary>
    public double? DurationSeconds { get; set; }

    /// <summary>Frames across all runs.</summary>
    public int? FrameCount { get; set; }

    /// <summary>Runs the capture contains.</summary>
    public int? RunCount { get; set; }

    /// <summary>
    /// Average frame rate over the whole capture, as the analysis computes it.
    /// </summary>
    /// <remarks>
    /// Stored rather than computed per request because the record list shows it for every row, and
    /// computing it means parsing the capture file. It comes from the same adapter the analysis
    /// view uses, so the list and the open record cannot disagree.
    /// </remarks>
    public double? AverageFps { get; set; }

    /// <summary>1st percentile frame rate over the whole capture.</summary>
    public double? P1Fps { get; set; }

    /// <summary>99th percentile frame rate over the whole capture.</summary>
    public double? P99Fps { get; set; }

    /// <summary>Decimated frame times for the record list, as a JSON array.</summary>
    public string? SparklineJson { get; set; }

    /// <summary>Whether the capture carries input-to-display latency.</summary>
    public bool HasPcLatency { get; set; }

    /// <summary>Whether the capture carries display-side frame times.</summary>
    public bool HasDisplayChange { get; set; }

    public ICollection<SessionRun> Runs { get; set; } = new List<SessionRun>();
}
