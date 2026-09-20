using CapFrameX.Service.Analysis;
using CapFrameX.Service.Application.Records;
using CapFrameX.Service.Contracts.Analysis;
using CapFrameX.Service.Core.Bridge;
using CapFrameX.Service.Data;
using CapFrameX.Service.Records;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace CapFrameX.Service.Application.Tests.Records;

/// <summary>
/// Importing captures somebody actually recorded.
/// </summary>
/// <remarks>
/// The generated fixtures carry the handful of columns the tests assert on. Real captures carry
/// sensor blocks, frame types, dropped frames, several runs and fields written by CapFrameX
/// versions nobody remembers - which is where a round trip through the database either holds or
/// quietly loses something. It reports itself skipped where there are no captures, so a green run
/// on a build agent is no evidence it ran.
/// </remarks>
public sealed class RealCaptureImportTests : IAsyncLifetime
{
    /// <summary>How many real captures to import; enough to meet the variety, quick enough to run.</summary>
    private const int Sample = 25;

    private readonly string _root = Path.Combine(Path.GetTempPath(), "cfx-real-" + Guid.NewGuid().ToString("N"));
    private readonly RecordingPublisher _events = new();

    private string _source = string.Empty;
    private string _databasePath = string.Empty;

    public async Task InitializeAsync()
    {
        _source = Path.Combine(_root, "Source");
        Directory.CreateDirectory(_source);
        _databasePath = Path.Combine(_root, "capframex.db");

        foreach (var path in RealCaptures.Take(Sample))
        {
            File.Copy(path, Path.Combine(_source, Path.GetFileName(path)), overwrite: true);
        }

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

    [RealCapturesFact]
    public async Task Every_real_capture_imports()
    {
        var result = await ImportAsync();

        Assert.Equal(0, result.Failed);
        Assert.True(result.Imported > 0);
        Assert.Equal(Directory.GetFiles(_source).Length, result.Total);
    }

    [RealCapturesFact]
    public async Task An_imported_capture_analyses_the_same_as_the_file_it_came_from()
    {
        // The round trip through the database has to change nothing: the same frames, so the same
        // numbers. This is what a lossy store would fail.
        await ImportAsync();

        var analysis = new AnalysisService(new AnalysisSettings());
        var reader = new RecordFileReader();
        var compared = 0;

        await using var context = Context();

        foreach (var record in await context.Sessions.Include(session => session.Runs).ToListAsync())
        {
            var fromFile = reader.Parse(await File.ReadAllTextAsync(record.ImportedFrom!), record.ImportedFrom!);
            var stored = StoredRecordFactory.Rebuild(record, [.. record.Runs.OrderBy(run => run.RunIndex)]);

            var expected = analysis.Analyze(fromFile.Session!, Metrics);
            var actual = analysis.Analyze(stored, Metrics);

            Assert.Equal(expected.Window.FrameCount, actual.Window.FrameCount);
            Assert.Equal(
                expected.Metrics.Select(metric => metric.Value),
                actual.Metrics.Select(metric => metric.Value));
            Assert.Equal(expected.FramePacing.SpikeCount, actual.FramePacing.SpikeCount);

            compared++;
        }

        Assert.True(compared > 0);
    }

    [RealCapturesFact]
    public async Task Every_run_of_a_real_capture_keeps_its_frames()
    {
        // Several of these captures have more than one run, and a run silently dropped would show
        // up as a record that is shorter than the file it came from.
        await ImportAsync();

        var reader = new RecordFileReader();

        await using var context = Context();

        foreach (var record in await context.Sessions.Include(session => session.Runs).ToListAsync())
        {
            var fromFile = reader.Parse(await File.ReadAllTextAsync(record.ImportedFrom!), record.ImportedFrom!);

            Assert.Equal(fromFile.Session!.Runs.Count, record.Runs.Count);
            Assert.All(record.Runs, run => Assert.False(string.IsNullOrEmpty(run.CaptureDataJson)));

            // The order is part of the capture: the statistics read the runs as one sequence.
            Assert.Equal(
                Enumerable.Range(0, record.Runs.Count),
                record.Runs.Select(run => run.RunIndex).Order());
        }
    }

    [RealCapturesFact]
    public async Task What_the_capture_carried_beside_the_frames_is_kept_too()
    {
        // Sensor readings go through a converter that is lossy in one direction, which is why the
        // payload is stored as the text the file held rather than serialised back from the model.
        await ImportAsync();

        await using var context = Context();
        var runs = await context.SessionRuns.ToListAsync();

        Assert.Contains(runs, run => !string.IsNullOrEmpty(run.SensorDataJson));
    }

    private static readonly AnalysisRequest Metrics = new()
    {
        Metrics = [.. MetricCatalog.Supported.Where(metric => MetricCatalog.Source(metric) == MetricSource.Frametimes)],
    };

    private static string[] RealCaptures { get; } = Find();

    private static string[] Find()
    {
        var folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "CapFrameX",
            "Captures");

        return Directory.Exists(folder)
            ? Directory.GetFiles(folder, "*" + RecordFileReader.Extension, SearchOption.AllDirectories)
            : [];
    }

    /// <summary>A test that needs the running user's own captures.</summary>
    public sealed class RealCapturesFactAttribute : FactAttribute
    {
        /// <summary>Creates the attribute and skips the test when there are none.</summary>
        public RealCapturesFactAttribute()
        {
            if (RealCaptures.Length == 0)
            {
                Skip = "No capture files in the running user's capture folder.";
            }
        }
    }

    private CapFrameXDbContext Context() =>
        new(new DbContextOptionsBuilder<CapFrameXDbContext>()
            .UseSqlite($"Data Source={_databasePath}")
            .Options);

    private async Task<RecordImportResult> ImportAsync()
    {
        await using var context = Context();

        var importer = new RecordImporter(
            context,
            new RecordFileReader(),
            new AnalysisService(new AnalysisSettings()),
            new RecordIndexOptions { CaptureDirectory = _source },
            _events,
            NullLogger<RecordImporter>.Instance);

        return await importer.ImportDirectoryAsync(_source);
    }

    private sealed class RecordingPublisher : IBridgeEventPublisher
    {
        public void Publish(string type, object payload, int version = 1)
        {
        }
    }
}
