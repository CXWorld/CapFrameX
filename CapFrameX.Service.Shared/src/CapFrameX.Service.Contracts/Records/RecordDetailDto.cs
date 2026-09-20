namespace CapFrameX.Service.Contracts.Records;

/// <summary>
/// One of the pills above the analysis: a fact about the machine or the capture, short enough to
/// read at a glance.
/// </summary>
/// <param name="Key">What kind of fact it is, so the frontend can order and style them.</param>
/// <param name="Label">What it says.</param>
public sealed record ChipDto(string Key, string Label);

/// <summary>Kinds of chip the service produces.</summary>
public static class ChipKeys
{
    /// <summary>The processor.</summary>
    public const string Processor = "processor";

    /// <summary>The graphics card.</summary>
    public const string Gpu = "gpu";

    /// <summary>System memory.</summary>
    public const string Memory = "memory";

    /// <summary>Graphics API the game presented through.</summary>
    public const string Api = "api";

    /// <summary>Display resolution.</summary>
    public const string Resolution = "resolution";

    /// <summary>How the game was presented - fullscreen, borderless, windowed.</summary>
    public const string Presentation = "presentation";

    /// <summary>Platform switches that were on: resizable BAR, HAGS, game mode.</summary>
    public const string Features = "features";

    /// <summary>Graphics driver version.</summary>
    public const string Driver = "driver";
}

/// <summary>
/// What the capture recorded about the machine and the run.
/// </summary>
/// <remarks>
/// The fields a user may correct - processor, graphics card, memory, motherboard, game name,
/// comment, resolution - are the ones CapFrameX 1.x has always allowed editing, and a change here
/// is written back into the capture file.
/// </remarks>
/// <param name="GameName">Game as the capture recorded it.</param>
/// <param name="ProcessName">Executable name without extension.</param>
/// <param name="Comment">The user's note about this capture.</param>
/// <param name="Processor">CPU.</param>
/// <param name="Motherboard">Mainboard.</param>
/// <param name="SystemRam">Memory.</param>
/// <param name="Gpu">Graphics card.</param>
/// <param name="GpuCount">How many of them.</param>
/// <param name="GpuCoreClock">Core clock in MHz.</param>
/// <param name="GpuMemoryClock">Memory clock in MHz.</param>
/// <param name="BaseDriverVersion">Driver version.</param>
/// <param name="DriverPackage">Driver package.</param>
/// <param name="GpuDriverVersion">Graphics driver version.</param>
/// <param name="Os">Operating system.</param>
/// <param name="ApiInfo">Graphics API.</param>
/// <param name="ResizableBar">Whether resizable BAR was on, where the capture knows.</param>
/// <param name="WinGameMode">Whether Windows game mode was on, where the capture knows.</param>
/// <param name="Hags">Whether hardware-accelerated GPU scheduling was on, where the capture knows.</param>
/// <param name="PresentationMode">How the game was presented.</param>
/// <param name="ResolutionInfo">Display resolution.</param>
/// <param name="AppVersion">Which CapFrameX wrote the capture.</param>
/// <param name="DeviceName">Name of the machine.</param>
public sealed record RecordInfoDto(
    string? GameName,
    string? ProcessName,
    string? Comment,
    string? Processor,
    string? Motherboard,
    string? SystemRam,
    string? Gpu,
    string? GpuCount,
    string? GpuCoreClock,
    string? GpuMemoryClock,
    string? BaseDriverVersion,
    string? DriverPackage,
    string? GpuDriverVersion,
    string? Os,
    string? ApiInfo,
    bool? ResizableBar,
    bool? WinGameMode,
    bool? Hags,
    string? PresentationMode,
    string? ResolutionInfo,
    string? AppVersion,
    string? DeviceName);

/// <summary>One run inside a capture, as the run selector shows it.</summary>
/// <param name="Index">Its position in the capture, which is what the analysis asks for.</param>
/// <param name="DurationSeconds">The span its frames cover.</param>
/// <param name="FrameCount">How many frames it has.</param>
/// <param name="PresentMonRuntime">Which presentation runtime it was captured through.</param>
/// <param name="HasPcLatency">Whether it carries input-to-display latency.</param>
/// <param name="HasDisplayChange">Whether it carries display-side frame times.</param>
/// <param name="HasGpuActive">Whether it carries GPU-busy times.</param>
public sealed record RecordRunDto(
    int Index,
    double DurationSeconds,
    int FrameCount,
    string? PresentMonRuntime,
    bool HasPcLatency,
    bool HasDisplayChange,
    bool HasGpuActive);

/// <summary>Where the capture behind a record lives.</summary>
/// <param name="FilePath">Full path of the file.</param>
/// <param name="FileSize">Its size in bytes.</param>
/// <param name="ModifiedUtc">When it was last written.</param>
/// <param name="IndexVersion">Which indexer version projected it.</param>
public sealed record RecordSourceDto(string? FilePath, long? FileSize, DateTimeOffset? ModifiedUtc, int IndexVersion);

/// <summary>
/// One capture as the analysis view opens it.
/// </summary>
/// <remarks>
/// It carries the summary as well, so a deep link into one record does not have to fetch the list
/// first.
/// </remarks>
/// <param name="Summary">The same fields the record list shows.</param>
/// <param name="Info">What the capture recorded about the machine.</param>
/// <param name="Runs">Its runs, in the order they were recorded.</param>
/// <param name="Chips">The pills above the analysis.</param>
/// <param name="Source">Where the capture file lives.</param>
public sealed record RecordDetailDto(
    RecordSummaryDto Summary,
    RecordInfoDto Info,
    IReadOnlyList<RecordRunDto> Runs,
    IReadOnlyList<ChipDto> Chips,
    RecordSourceDto Source);
