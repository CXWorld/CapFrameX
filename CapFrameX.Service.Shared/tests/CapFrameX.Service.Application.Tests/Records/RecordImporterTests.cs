using CapFrameX.Service.Analysis;
using CapFrameX.Service.Application.Records;
using CapFrameX.Service.Core.Bridge;
using CapFrameX.Service.Data;
using CapFrameX.Service.Data.Models;
using CapFrameX.Service.Records;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace CapFrameX.Service.Application.Tests.Records;

/// <summary>
/// Reading capture files into the database.
/// </summary>
/// <remarks>
/// An imported record has to stand on its own: the file it came from may be moved, deleted or on a
/// drive nobody plugged in, and the record still has to open. That is the difference from the
/// folder scan, and it is what most of these tests are about.
/// </remarks>
public sealed class RecordImporterTests : IAsyncLifetime
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "cfx-import-" + Guid.NewGuid().ToString("N"));
    private readonly RecordingPublisher _events = new();

    private string _source = string.Empty;
    private string _databasePath = string.Empty;

    public async Task InitializeAsync()
    {
        _source = Path.Combine(_root, "Source");
        Directory.CreateDirectory(_source);
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
    public async Task A_folder_of_captures_becomes_records()
    {
        Write("one", "One");
        Write("two", "Two");

        var result = await ImportAsync();

        Assert.Equal(2, result.Imported);
        Assert.Equal(0, result.Failed);
        Assert.Equal(2, (await RecordsAsync()).Count);
    }

    [Fact]
    public async Task An_imported_record_carries_its_frames()
    {
        // The point of importing rather than indexing: the row holds the capture.
        Write("one", "One");

        await ImportAsync();

        await using var context = Context();
        var run = Assert.Single(await context.SessionRuns.ToListAsync());
        Assert.False(string.IsNullOrEmpty(run.CaptureDataJson));
        Assert.Contains("MsBetweenPresents", run.CaptureDataJson!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_imported_record_is_not_the_folder_scan_business()
    {
        // It has no SourceFilePath, so a scan of some other folder cannot decide it has vanished.
        Write("one", "One");

        await ImportAsync();

        var record = Assert.Single(await RecordsAsync());
        Assert.Null(record.SourceFilePath);
        Assert.NotNull(record.ImportedFrom);
    }

    [Fact]
    public async Task An_imported_record_opens_after_its_file_is_gone()
    {
        var path = Write("one", "One");
        await ImportAsync();

        File.Delete(path);

        await using var context = Context();
        var record = await context.Sessions.Include(session => session.Runs).SingleAsync();
        var session = StoredRecordFactory.Rebuild(record, [.. record.Runs]);

        Assert.Equal("One", session.Info.GameName);
        Assert.Equal(CaptureFixture.FramesPerRun, session.Runs[0].CaptureData.MsBetweenPresents.Length);
    }

    [Fact]
    public async Task Every_run_of_a_capture_is_imported_in_order()
    {
        Write("two-runs", "Two runs", runs: 2);

        await ImportAsync();

        await using var context = Context();
        var record = await context.Sessions.Include(session => session.Runs).SingleAsync();
        Assert.Equal(2, record.Runs.Count);
        Assert.Equal(2, record.RunCount);
        Assert.All(record.Runs, run => Assert.False(string.IsNullOrEmpty(run.CaptureDataJson)));
    }

    [Fact]
    public async Task The_same_capture_is_not_imported_twice()
    {
        // Importing a folder again after adding one file to it is the normal case.
        Write("one", "One");
        await ImportAsync();

        Write("two", "Two");
        var second = await ImportAsync();

        Assert.Equal(1, second.Imported);
        Assert.Equal(1, second.AlreadyKnown);
        Assert.Equal(2, (await RecordsAsync()).Count);
    }

    [Fact]
    public async Task The_same_capture_under_another_name_is_recognised()
    {
        // Identity comes from the capture, not from the file, so a copy is not a second record.
        var content = File.ReadAllText(Write("one", "One"));
        File.WriteAllText(Path.Combine(_source, "copy.json"), content);

        var result = await ImportAsync();

        Assert.Equal(1, result.Imported);
        Assert.Equal(1, result.AlreadyKnown);
    }

    [Fact]
    public async Task A_capture_the_folder_scan_already_found_is_not_imported_again()
    {
        // Both paths store the capture identity, so the two cannot produce one record twice.
        var path = Write("one", "One");

        await using (var context = Context())
        {
            await Index(context, _source).ScanAsync();
        }

        var result = await ImportAsync();

        Assert.Equal(0, result.Imported);
        Assert.Equal(1, result.AlreadyKnown);
        Assert.Single(await RecordsAsync());
        Assert.True(File.Exists(path));
    }

    [Fact]
    public async Task A_file_that_is_not_a_capture_costs_one_file_rather_than_the_import()
    {
        File.WriteAllText(Path.Combine(_source, "notes.json"), "{ \"this\": \"is not a capture\" }");
        Write("one", "One");

        var result = await ImportAsync();

        Assert.Equal(1, result.Imported);
        Assert.Equal(1, result.Failed);
        Assert.NotEmpty(result.Errors);
    }

    [Fact]
    public async Task A_folder_that_is_not_there_is_reported_rather_than_thrown()
    {
        var result = await ImportAsync(Path.Combine(_root, "no-such-folder"));

        Assert.Equal(0, result.Total);
        Assert.NotEmpty(result.Errors);
    }

    [Fact]
    public async Task Subfolders_are_read_unless_the_caller_says_otherwise()
    {
        Write("top", "Top");
        var nested = Path.Combine(_source, "Cyberpunk 2077");
        Directory.CreateDirectory(nested);
        File.WriteAllText(Path.Combine(nested, "deep.json"), CaptureFixture.Capture("Deep"));

        Assert.Equal(1, (await ImportAsync(recursive: false)).Imported);
        Assert.Equal(1, (await ImportAsync()).Imported);
    }

    [Fact]
    public async Task An_imported_record_knows_what_the_list_shows()
    {
        Write("one", "One");

        await ImportAsync();

        var record = Assert.Single(await RecordsAsync());
        Assert.Equal("One", record.GameName);
        Assert.NotNull(record.AverageFps);
        Assert.NotNull(record.SparklineJson);
        Assert.True(record.DurationSeconds > 0);
        Assert.Equal(CaptureFixture.FramesPerRun, record.FrameCount);
    }

    [Fact]
    public async Task Everything_imported_lands_in_one_suite()
    {
        Write("one", "One");
        await ImportAsync();
        Write("two", "Two");
        await ImportAsync();

        await using var context = Context();
        var suite = Assert.Single(await context.Suites.ToListAsync());
        Assert.Equal(RecordImporter.SuiteId, suite.Id);
    }

    [Fact]
    public async Task The_frontend_is_told_when_records_arrive()
    {
        Write("one", "One");

        await ImportAsync();

        Assert.Single(_events.Published);
    }

    [Fact]
    public async Task An_import_that_brought_nothing_stays_silent()
    {
        // The frontend reloads the list on the event; an import of a folder it already has must
        // not make it.
        Write("one", "One");
        await ImportAsync();
        _events.Clear();

        await ImportAsync();

        Assert.Empty(_events.Published);
    }

    private CapFrameXDbContext Context() =>
        new(new DbContextOptionsBuilder<CapFrameXDbContext>()
            .UseSqlite($"Data Source={_databasePath}")
            .Options);

    private RecordIndex Index(CapFrameXDbContext context, string directory) =>
        new(context,
            new RecordFileReader(),
            new AnalysisService(new AnalysisSettings()),
            new RecordIndexOptions { CaptureDirectory = directory },
            NullLogger<RecordIndex>.Instance);

    private async Task<RecordImportResult> ImportAsync(string? directory = null, bool recursive = true)
    {
        await using var context = Context();

        var importer = new RecordImporter(
            context,
            new RecordFileReader(),
            new AnalysisService(new AnalysisSettings()),
            new RecordIndexOptions { CaptureDirectory = _source },
            _events,
            NullLogger<RecordImporter>.Instance);

        return await importer.ImportDirectoryAsync(directory ?? _source, recursive);
    }

    private async Task<List<Session>> RecordsAsync()
    {
        await using var context = Context();

        return await context.Sessions.AsNoTracking().ToListAsync();
    }

    private string Write(string name, string game, int runs = 1)
    {
        var path = Path.Combine(_source, name + RecordFileReader.Extension);
        File.WriteAllText(path, CaptureFixture.Capture(game, runs));

        return path;
    }

    private sealed class RecordingPublisher : IBridgeEventPublisher
    {
        private readonly List<(string Type, object Payload)> _published = [];

        public IReadOnlyList<(string Type, object Payload)> Published => [.. _published];

        public void Clear() => _published.Clear();

        public void Publish(string type, object payload, int version = 1) => _published.Add((type, payload));
    }
}
