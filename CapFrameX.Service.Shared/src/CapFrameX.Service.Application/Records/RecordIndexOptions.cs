using CapFrameX.Service.Records;

namespace CapFrameX.Service.Application.Records;

/// <summary>
/// What the indexer needs to know about the folder it watches.
/// </summary>
public sealed class RecordIndexOptions
{
    /// <summary>The folder holding the capture files, including its subfolders.</summary>
    public required string CaptureDirectory { get; init; }

    /// <summary>Upper bound for the frame times kept per record for the list.</summary>
    public int SparklinePoints { get; init; } = FrametimeSparkline.DefaultPointCount;

    /// <summary>
    /// How long the folder has to be quiet before a change is acted on.
    /// </summary>
    /// <remarks>
    /// A capture is written in one go, but the file system reports it in several events, and the
    /// last of them can arrive after the first byte by a noticeable margin on a large record.
    /// Scanning on the first event would read a half-written file and index the failure.
    /// </remarks>
    public TimeSpan SettleDelay { get; init; } = TimeSpan.FromSeconds(2);
}
