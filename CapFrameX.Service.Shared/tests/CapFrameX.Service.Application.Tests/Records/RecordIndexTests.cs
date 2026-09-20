using System.Text.Json;
using CapFrameX.Service.Analysis;
using CapFrameX.Statistics.NetStandard.Contracts;
using CapFrameX.Service.Application.Records;
using CapFrameX.Service.Data;
using CapFrameX.Service.Data.Models;
using CapFrameX.Service.Records;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace CapFrameX.Service.Application.Tests.Records;

/// <summary>
/// The index against a real capture folder and a real SQLite file.
/// </summary>
/// <remarks>
/// Both are the point: the in-memory provider ignores the unique index that keeps a file out of
/// the index twice, and a fake file system would not reproduce what the scan actually compares -
/// size and last write time as the operating system reports them.
/// </remarks>
public sealed class RecordIndexTests : IAsyncLifetime
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "cfx-index-" + Guid.NewGuid().ToString("N"));
    private string _captures = string.Empty;
    private string _databasePath = string.Empty;

    public async Task InitializeAsync()
    {
        _captures = Path.Combine(_root, "Captures");
        Directory.CreateDirectory(_captures);
        _databasePath = Path.Combine(_root, "capframex.db");

        await using var context = Context();
        await context.Database.MigrateAsync();
    }

    public Task DisposeAsync()
    {
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
            // A temp folder that outlives the run is not a test failure.
        }

        return Task.CompletedTask;
    }

    [Fact]
    public async Task Scanning_an_empty_folder_changes_nothing()
    {
        var result = await ScanAsync();

        Assert.True(result.IsEmpty);
        Assert.Empty(await SessionsAsync());
    }

    [Fact]
    public async Task A_missing_capture_folder_is_not_an_error()
    {
        // The folder appears when the user first captures; until then there is simply nothing.
        Directory.Delete(_captures, recursive: true);

        var result = await ScanAsync();

        Assert.True(result.IsEmpty);
    }

    [Fact]
    public async Task A_capture_file_becomes_a_record()
    {
        var path = Write("run-one", CaptureFixture.Capture());

        var result = await ScanAsync();

        Assert.Equal(1, result.Added);
        var session = Assert.Single(await SessionsAsync());
        Assert.Equal("Cyberpunk 2077", session.GameName);
        Assert.Equal("Cyberpunk2077", session.ProcessName);
        Assert.Equal(path, session.SourceFilePath);
        Assert.Equal(1, session.RunCount);
        Assert.Equal(CaptureFixture.FramesPerRun, session.FrameCount);
        Assert.Equal(RecordIndexPlanner.CurrentIndexVersion, session.IndexVersion);
    }

    [Fact]
    public async Task A_record_keeps_what_the_list_needs_without_the_capture_file()
    {
        Write("run-one", CaptureFixture.Capture());

        await ScanAsync();

        var session = Assert.Single(await SessionsAsync());
        Assert.NotNull(session.DurationSeconds);
        Assert.True(session.DurationSeconds > 0);
        Assert.NotNull(session.SparklineJson);
        Assert.NotEmpty(JsonSerializer.Deserialize<double[]>(session.SparklineJson!)!);
        Assert.Equal("Ryzen 9 9950X", session.Processor);
        Assert.Equal("RTX 5090", session.Gpu);
    }

    [Fact]
    public async Task A_record_carries_the_metrics_the_list_shows()
    {
        Write("run-one", CaptureFixture.Capture());

        await ScanAsync();

        var session = Assert.Single(await SessionsAsync());
        Assert.NotNull(session.AverageFps);
        Assert.NotNull(session.P1Fps);
        Assert.NotNull(session.P99Fps);
    }

    [Fact]
    public async Task The_stored_metrics_are_the_ones_the_analysis_returns()
    {
        // The list and the open record answer the same question, so they must answer it with the
        // same number - which is why the index calls the analysis rather than computing its own.
        var path = Write("run-one", CaptureFixture.Capture());
        await ScanAsync();

        var read = new RecordFileReader().Parse(File.ReadAllText(path), path);
        var expected = new AnalysisService(new AnalysisSettings())
            .Analyze(read.Session!, new AnalysisRequest { Metrics = [EMetric.Average, EMetric.P1, EMetric.P99] })
            .Metrics;

        var session = Assert.Single(await SessionsAsync());
        Assert.Equal(expected[0].Value, session.AverageFps);
        Assert.Equal(expected[1].Value, session.P1Fps);
        Assert.Equal(expected[2].Value, session.P99Fps);
    }

    [Fact]
    public async Task A_record_indexed_by_an_older_version_is_re_read_and_gains_what_it_was_missing()
    {
        // What the version column is for: the projection grew, and no schema migration can fill in
        // a number that was never computed.
        Write("run-one", CaptureFixture.Capture());
        await ScanAsync();

        await using (var context = Context())
        {
            var stale = await context.Sessions.SingleAsync();
            stale.IndexVersion = RecordIndexPlanner.CurrentIndexVersion - 1;
            stale.AverageFps = null;
            await context.SaveChangesAsync();
        }

        var result = await ScanAsync();

        Assert.Equal(1, result.Updated);
        var session = Assert.Single(await SessionsAsync());
        Assert.NotNull(session.AverageFps);
        Assert.Equal(RecordIndexPlanner.CurrentIndexVersion, session.IndexVersion);
    }

    [Fact]
    public async Task The_frame_data_is_not_copied_into_the_database()
    {
        // The file is the record. Copying the frames in would double the storage and create a
        // second version of the same capture that can drift from the first.
        Write("run-one", CaptureFixture.Capture());

        await ScanAsync();

        await using var context = Context();
        Assert.Empty(await context.SessionRuns.ToListAsync());
    }

    [Fact]
    public async Task What_the_capture_recorded_is_flagged_on_the_record()
    {
        Write("plain", CaptureFixture.Capture("Plain"));
        Write("latency", CaptureFixture.Capture("Latency", pcLatency: true));

        await ScanAsync();

        var sessions = await SessionsAsync();
        Assert.False(sessions.Single(s => s.GameName == "Plain").HasPcLatency);
        Assert.True(sessions.Single(s => s.GameName == "Latency").HasPcLatency);
        Assert.All(sessions, session => Assert.True(session.HasDisplayChange));
    }

    [Fact]
    public async Task Scanning_again_leaves_the_record_alone()
    {
        Write("run-one", CaptureFixture.Capture());
        await ScanAsync();
        var first = Assert.Single(await SessionsAsync()).Id;

        var second = await ScanAsync();

        Assert.True(second.IsEmpty);
        Assert.Equal(first, Assert.Single(await SessionsAsync()).Id);
    }

    [Fact]
    public async Task A_rewritten_capture_updates_its_record_in_place()
    {
        var path = Write("run-one", CaptureFixture.Capture());
        await ScanAsync();
        var id = Assert.Single(await SessionsAsync()).Id;

        Write("run-one", CaptureFixture.Capture(runs: 2));
        File.SetLastWriteTimeUtc(path, File.GetLastWriteTimeUtc(path).AddMinutes(1));

        var result = await ScanAsync();

        Assert.Equal(1, result.Updated);
        var session = Assert.Single(await SessionsAsync());
        Assert.Equal(id, session.Id);
        Assert.Equal(2, session.RunCount);
        Assert.Equal(2 * CaptureFixture.FramesPerRun, session.FrameCount);
    }

    [Fact]
    public async Task A_refreshed_record_picks_up_what_the_service_just_wrote()
    {
        // After the service edits a capture, the next request has to answer with the edit rather
        // than with whatever the folder watcher gets round to a second or two later.
        var path = Write("run-one", CaptureFixture.Capture("Before"));
        await ScanAsync();
        var id = Assert.Single(await SessionsAsync()).Id;

        Write("run-one", CaptureFixture.Capture("After"));

        await using (var context = Context())
        {
            var index = Index(context);
            Assert.True(await index.RefreshAsync(id));
        }

        Assert.Equal("After", Assert.Single(await SessionsAsync()).GameName);
    }

    [Fact]
    public async Task A_refresh_leaves_the_next_scan_nothing_to_do()
    {
        // It writes the file's new size and time as well; without that the watcher would re-read a
        // record that is already current.
        var path = Write("run-one", CaptureFixture.Capture("Before"));
        await ScanAsync();
        var id = Assert.Single(await SessionsAsync()).Id;

        Write("run-one", CaptureFixture.Capture("After", runs: 2));
        File.SetLastWriteTimeUtc(path, File.GetLastWriteTimeUtc(path).AddMinutes(1));

        await using (var context = Context())
        {
            await Index(context).RefreshAsync(id);
        }

        Assert.True((await ScanAsync()).IsEmpty);
    }

    [Fact]
    public async Task Refreshing_something_with_no_file_behind_it_does_nothing()
    {
        await using var context = Context();

        Assert.False(await Index(context).RefreshAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task A_deleted_capture_loses_its_record()
    {
        var path = Write("run-one", CaptureFixture.Capture());
        await ScanAsync();

        File.Delete(path);
        var result = await ScanAsync();

        Assert.Equal(1, result.Removed);
        Assert.Empty(await SessionsAsync());
    }

    [Fact]
    public async Task A_file_that_is_not_a_capture_is_skipped_and_the_rest_still_index()
    {
        // The folder belongs to the user, so a leftover from a crashed capture or something that
        // merely ends in .json has to cost one file, not the scan.
        Write("broken", "{ \"this\": \"is not a capture\" }");
        Write("good", CaptureFixture.Capture());

        var result = await ScanAsync();

        Assert.Equal(1, result.Added);
        Assert.Equal(1, result.Failed);
        Assert.Single(await SessionsAsync());
    }

    [Fact]
    public async Task A_file_that_could_not_be_read_is_tried_again_on_the_next_scan()
    {
        // A capture caught half-written must not be written off: nothing about it was stored, so
        // the next scan still sees it as new.
        var path = Write("half", "{ \"Runs\": [");
        await ScanAsync();

        Write("half", CaptureFixture.Capture());
        File.SetLastWriteTimeUtc(path, File.GetLastWriteTimeUtc(path).AddMinutes(1));
        var result = await ScanAsync();

        Assert.Equal(1, result.Added);
        Assert.Single(await SessionsAsync());
    }

    [Fact]
    public async Task Captures_in_subfolders_are_indexed()
    {
        var folder = Path.Combine(_captures, "Cyberpunk 2077");
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "nested.json"), CaptureFixture.Capture());

        var result = await ScanAsync();

        Assert.Equal(1, result.Added);
    }

    [Fact]
    public async Task Every_record_lands_in_one_capture_suite()
    {
        Write("one", CaptureFixture.Capture("One"));
        await ScanAsync();
        Write("two", CaptureFixture.Capture("Two"));
        await ScanAsync();

        await using var context = Context();
        var suite = Assert.Single(await context.Suites.ToListAsync());
        Assert.Equal(RecordIndex.DefaultSuiteId, suite.Id);
        Assert.Equal(RecordIndex.DefaultSuiteName, suite.Name);
        Assert.Equal(2, await context.Sessions.CountAsync(s => s.SuiteId == suite.Id));
    }

    [Fact]
    public async Task A_session_the_service_recorded_itself_survives_a_scan()
    {
        // It has no file in the folder, so a scan of the folder says nothing about it.
        var suiteId = Guid.NewGuid();

        await using (var context = Context())
        {
            context.Suites.Add(new Suite { Id = suiteId, Name = "Live", Type = SuiteType.Miscellaneous });
            context.Sessions.Add(new Session
            {
                Id = Guid.NewGuid(),
                SuiteId = suiteId,
                GameName = "Recorded live",
                ProcessName = "game",
                Processor = "cpu",
                Gpu = "gpu",
                Os = "Windows 11",
                CreatedAt = DateTime.UtcNow,
            });
            await context.SaveChangesAsync();
        }

        var result = await ScanAsync();

        Assert.True(result.IsEmpty);
        Assert.Single(await SessionsAsync());
    }

    private CapFrameXDbContext Context() =>
        new(new DbContextOptionsBuilder<CapFrameXDbContext>()
            .UseSqlite($"Data Source={_databasePath}")
            .Options);

    private async Task<RecordIndexResult> ScanAsync()
    {
        await using var context = Context();

        return await Index(context).ScanAsync();
    }

    private RecordIndex Index(CapFrameXDbContext context) =>
        new(context,
            new RecordFileReader(),
            new AnalysisService(new AnalysisSettings()),
            new RecordIndexOptions { CaptureDirectory = _captures },
            NullLogger<RecordIndex>.Instance);

    private async Task<List<Session>> SessionsAsync()
    {
        await using var context = Context();

        return await context.Sessions.AsNoTracking().ToListAsync();
    }

    private string Write(string name, string content)
    {
        var path = Path.Combine(_captures, name + RecordFileReader.Extension);
        File.WriteAllText(path, content);

        return path;
    }
}
