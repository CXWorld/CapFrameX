using CapFrameX.Service.Records;

namespace CapFrameX.Service.Application.Records;

/// <summary>
/// What the indexer needs to know about the folder it watches.
/// </summary>
public sealed class RecordIndexOptions
{
    private string _captureDirectory = string.Empty;

    /// <summary>
    /// The folder holding the capture files, including its subfolders.
    /// </summary>
    /// <remarks>
    /// The user can move it while the service runs, so this is not fixed at start-up. Setting it
    /// invalidates the index, which is what makes the watcher let go of the old folder and take up
    /// the new one.
    /// </remarks>
    public required string CaptureDirectory
    {
        get => _captureDirectory;
        set
        {
            if (string.Equals(_captureDirectory, value, StringComparison.Ordinal))
            {
                return;
            }

            _captureDirectory = value;
            Invalidated?.Invoke(true);
        }
    }

    /// <summary>
    /// Raised when the index has to catch up, with whether the folder itself moved.
    /// </summary>
    /// <remarks>
    /// The one channel the background indexer listens on besides the file system, so that
    /// everything which can make the index stale - the folder moving, the projection changing -
    /// arrives the same way.
    /// </remarks>
    public event Action<bool>? Invalidated;

    /// <summary>
    /// Asks the indexer to go over the folder again although nothing in it changed.
    /// </summary>
    /// <remarks>
    /// For when what the index *stores* has to change rather than what it reads - a metric the
    /// records were projected with, for instance.
    /// </remarks>
    public void Invalidate() => Invalidated?.Invoke(false);

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
