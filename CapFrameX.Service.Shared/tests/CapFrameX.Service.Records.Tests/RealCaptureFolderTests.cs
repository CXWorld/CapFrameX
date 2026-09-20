using CapFrameX.Service.Records;

namespace CapFrameX.Service.Records.Tests;

/// <summary>
/// Reads the capture folder of whoever runs the tests, when there is one.
/// </summary>
/// <remarks>
/// Hand-written fixtures pin the shape the reader expects; this pins that the shape is the one real
/// files actually have. It is skipped where no capture folder exists, so it never fails on a build
/// agent - which also means a green run is no evidence that it ran.
/// </remarks>
public sealed class RealCaptureFolderTests
{
    [RequiresRealCapturesFact]
    public async Task Every_real_capture_in_the_users_folder_is_readable()
    {
        var records = CaptureFolder.Records();

        var reader = new RecordFileReader();
        var failures = new List<string>();

        foreach (var record in records)
        {
            var result = await reader.ReadAsync(record);

            if (!result.IsSuccess)
            {
                failures.Add($"{Path.GetFileName(record)}: {result.Error}");
            }
        }

        Assert.True(
            failures.Count == 0,
            $"{failures.Count} of {records.Length} real captures could not be read:{Environment.NewLine}"
            + string.Join(Environment.NewLine, failures.Take(10)));
    }

    [RequiresRealCapturesFact]
    public async Task A_real_capture_carries_the_fields_the_index_needs()
    {
        var records = CaptureFolder.Records();

        var session = (await new RecordFileReader().ReadAsync(records[0])).Session;

        Assert.NotNull(session);
        Assert.False(string.IsNullOrWhiteSpace(session.Info.ProcessName));
        Assert.NotEmpty(session.Runs);
        Assert.NotNull(session.Runs[0].CaptureData);
        Assert.NotEmpty(session.Runs[0].CaptureData!.MsBetweenPresents);
    }

    [RequiresRealCapturesFact]
    public async Task Summaries_of_real_captures_are_usable_in_a_list()
    {
        var reader = new RecordFileReader();
        var summaries = new List<RecordSummary>();

        foreach (var record in CaptureFolder.Records())
        {
            var session = (await reader.ReadAsync(record)).Session;

            if (session is not null)
            {
                summaries.Add(RecordSummaryFactory.Create(session, record));
            }
        }

        Assert.NotEmpty(summaries);

        // A list entry with no duration, no frames or no sparkline is an entry a user cannot read
        // anything from, so none of the real captures may produce one.
        Assert.All(summaries, summary =>
        {
            Assert.False(string.IsNullOrWhiteSpace(summary.Name));
            Assert.True(summary.RunCount > 0, $"{summary.Name} has no runs.");
            Assert.True(summary.FrameCount > 0, $"{summary.Name} has no frames.");
            Assert.True(summary.DurationSeconds > 0, $"{summary.Name} has no duration.");
            Assert.NotEmpty(summary.Sparkline);
            Assert.True(
                summary.Sparkline.Length <= FrametimeSparkline.DefaultPointCount,
                $"{summary.Name} has an oversized sparkline.");
        });
    }
}
