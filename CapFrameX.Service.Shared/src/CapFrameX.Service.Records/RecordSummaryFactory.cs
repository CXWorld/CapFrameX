using CapFrameX.Data.Session.Contracts;

namespace CapFrameX.Service.Records;

/// <summary>
/// Projects a capture onto what the record list shows.
/// </summary>
public static class RecordSummaryFactory
{
    /// <summary>Builds the summary for one capture.</summary>
    /// <param name="session">The parsed capture.</param>
    /// <param name="filePath">Where it was read from.</param>
    /// <param name="sparklinePoints">Upper bound for the sparkline.</param>
    public static RecordSummary Create(
        ISession session,
        string filePath,
        int sparklinePoints = FrametimeSparkline.DefaultPointCount)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        var runs = session.Runs ?? [];
        var frameTimes = new List<double>();
        var duration = 0d;
        var hasPcLatency = false;
        var hasDisplayChange = false;

        foreach (var run in runs)
        {
            var data = run?.CaptureData;

            if (data is null)
            {
                // A run without frame data is still a run; it just contributes nothing. Dropping
                // the whole record here would hide a capture that is merely partly broken.
                continue;
            }

            if (data.MsBetweenPresents is { Length: > 0 } presents)
            {
                frameTimes.AddRange(presents);
            }

            // Runs are separate recordings, so the capture is as long as its runs together.
            duration += Span(data.TimeInSeconds);

            hasPcLatency |= data.PcLatency is { Length: > 0 };
            hasDisplayChange |= data.MsBetweenDisplayChange is { Length: > 0 };
        }

        return new RecordSummary(
            FilePath: filePath,
            Name: Path.GetFileNameWithoutExtension(filePath),
            GameName: NullIfBlank(session.Info?.GameName),
            ProcessName: NullIfBlank(session.Info?.ProcessName),
            CreatedAt: CreatedAt(session),
            DurationSeconds: duration,
            RunCount: runs.Count,
            FrameCount: frameTimes.Count,
            Sparkline: FrametimeSparkline.Decimate(frameTimes, sparklinePoints),
            Processor: NullIfBlank(session.Info?.Processor),
            Gpu: NullIfBlank(session.Info?.GPU),
            HasPcLatency: hasPcLatency,
            HasDisplayChange: hasDisplayChange);
    }

    /// <summary>The time the frames cover, which is not the same as their count times a frame time.</summary>
    private static double Span(double[]? timeInSeconds) =>
        timeInSeconds is { Length: > 1 }
            ? timeInSeconds[^1] - timeInSeconds[0]
            : 0d;

    private static DateTimeOffset CreatedAt(ISession session)
    {
        var creationDate = session.Info?.CreationDate ?? default;

        // The legacy model stores an unspecified DateTime; treating it as local would shift every
        // record by the reader's offset, so it is read as what it is - UTC.
        return creationDate == default
            ? default
            : new DateTimeOffset(DateTime.SpecifyKind(creationDate, DateTimeKind.Utc));
    }

    private static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
}
