using System.Collections.Concurrent;
using CapFrameX.Service.Analysis;
using CapFrameX.Service.Application.Records;
using CapFrameX.Service.Contracts.Bridge;
using CapFrameX.Service.Contracts.Records;
using CapFrameX.Service.Core.Bridge;
using CapFrameX.Service.Data;
using CapFrameX.Service.Records;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;

namespace CapFrameX.Service.Application.Tests.Records;

/// <summary>
/// The indexer against a real folder, a real watcher and a real database.
/// </summary>
/// <remarks>
/// The watcher is the part worth testing here, and it cannot be faked convincingly: whether a
/// change in the folder actually reaches the service is exactly what a fake would assume. The
/// settle delay is turned down so the test finishes, and every wait is a poll with a generous
/// ceiling rather than a fixed sleep, so a slow machine costs time instead of a red build.
/// </remarks>
public sealed class RecordIndexerTests : IAsyncLifetime
{
    private static readonly TimeSpan Ceiling = TimeSpan.FromSeconds(30);

    private readonly string _root = Path.Combine(Path.GetTempPath(), "cfx-indexer-" + Guid.NewGuid().ToString("N"));
    private readonly RecordingPublisher _events = new();

    private string _captures = string.Empty;
    private string _databasePath = string.Empty;
    private ServiceProvider _provider = null!;

    public async Task InitializeAsync()
    {
        _captures = Path.Combine(_root, "Captures");
        Directory.CreateDirectory(_captures);
        _databasePath = Path.Combine(_root, "capframex.db");

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<CapFrameXDbContext>(options => options.UseSqlite($"Data Source={_databasePath}"));
        services.AddSingleton(new RecordIndexOptions
        {
            CaptureDirectory = _captures,
            SettleDelay = TimeSpan.FromMilliseconds(50),
        });
        services.AddSingleton<RecordFileReader>();
        services.AddSingleton<AnalysisSettings>();
        services.AddSingleton<AnalysisService>();
        services.AddScoped<RecordIndex>();
        services.AddSingleton<IBridgeEventPublisher>(_events);

        _provider = services.BuildServiceProvider();

        await using var context = Context();
        await context.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await _provider.DisposeAsync();

        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
            // A temp folder that outlives the run is not a test failure.
        }
    }

    [Fact]
    public async Task What_was_in_the_folder_before_the_service_started_is_indexed()
    {
        Write("before", CaptureFixture.Capture());

        using var indexer = Start();

        Assert.True(await WaitForRecordsAsync(1));
    }

    [Fact]
    public async Task A_capture_written_while_the_service_runs_is_picked_up()
    {
        // The first record is what proves the start-up scan is over, so the second one can only
        // have arrived through the watcher.
        Write("first", CaptureFixture.Capture("First"));
        using var indexer = Start();
        Assert.True(await WaitForRecordsAsync(1));

        Write("later", CaptureFixture.Capture("Later"));

        Assert.True(await WaitForRecordsAsync(2));
    }

    [Fact]
    public async Task A_capture_deleted_while_the_service_runs_leaves_the_index()
    {
        var path = Write("gone", CaptureFixture.Capture());
        using var indexer = Start();
        Assert.True(await WaitForRecordsAsync(1));

        File.Delete(path);

        Assert.True(await WaitForRecordsAsync(0));
    }

    [Fact]
    public async Task The_frontend_is_told_what_changed()
    {
        Write("first", CaptureFixture.Capture("First"));
        using var indexer = Start();
        Assert.True(await WaitForRecordsAsync(1));

        Write("announced", CaptureFixture.Capture("Announced"));
        Assert.True(await WaitForRecordsAsync(2));

        var published = Assert.Single(
            _events.Published.Where(e => e.Type == BridgeEventTypes.RecordsChanged).Skip(1));
        var payload = Assert.IsType<RecordsChangedDto>(published.Payload);
        Assert.Equal(1, payload.Added);
    }

    [Fact]
    public async Task A_scan_that_changed_nothing_is_not_announced()
    {
        // The frontend reloads the list on this event, so a file the index cannot use has to stay
        // silent - and the folder is full of those on some machines.
        using var indexer = Start();
        Assert.True(await WaitForRecordsAsync(0));

        Write("notes", "{ \"this\": \"is not a capture\" }");

        await Task.Delay(TimeSpan.FromSeconds(1));

        Assert.Empty(_events.Published);
    }

    private RecordIndexer Start()
    {
        var indexer = new RecordIndexer(
            _provider.GetRequiredService<IServiceScopeFactory>(),
            _provider.GetRequiredService<RecordIndexOptions>(),
            _events,
            NullLogger<RecordIndexer>.Instance);

        indexer.StartAsync(CancellationToken.None).GetAwaiter().GetResult();

        return indexer;
    }

    private CapFrameXDbContext Context() =>
        new(new DbContextOptionsBuilder<CapFrameXDbContext>()
            .UseSqlite($"Data Source={_databasePath}")
            .Options);

    private async Task<bool> WaitForRecordsAsync(int expected)
    {
        var deadline = DateTime.UtcNow + Ceiling;

        while (DateTime.UtcNow < deadline)
        {
            await using var context = Context();

            if (await context.Sessions.CountAsync() == expected)
            {
                return true;
            }

            await Task.Delay(25);
        }

        return false;
    }

    private string Write(string name, string content)
    {
        var path = Path.Combine(_captures, name + RecordFileReader.Extension);
        File.WriteAllText(path, content);

        return path;
    }

    private sealed class RecordingPublisher : IBridgeEventPublisher
    {
        private readonly ConcurrentQueue<(string Type, object Payload)> _published = new();

        public IReadOnlyList<(string Type, object Payload)> Published => _published.ToArray();

        public void Publish(string type, object payload, int version = 1) => _published.Enqueue((type, payload));
    }
}
