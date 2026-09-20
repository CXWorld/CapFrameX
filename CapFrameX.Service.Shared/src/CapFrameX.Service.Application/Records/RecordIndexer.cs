using CapFrameX.Service.Contracts.Bridge;
using CapFrameX.Service.Contracts.Records;
using CapFrameX.Service.Core.Bridge;
using CapFrameX.Service.Records;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CapFrameX.Service.Application.Records;

/// <summary>
/// Keeps the index following the capture folder while the service runs.
/// </summary>
/// <remarks>
/// The folder is not the service's alone: CapFrameX 1.x writes into it, the user copies records in
/// and deletes them, and a share can change without anything local happening. Watching it is
/// therefore the normal case rather than a convenience, and every change ends in the same full
/// scan - the planner is cheap, and a scan cannot get out of step with the folder the way a queue
/// of individual file events can.
/// </remarks>
public sealed class RecordIndexer : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly RecordIndexOptions _options;
    private readonly IBridgeEventPublisher _events;
    private readonly ILogger<RecordIndexer> _logger;
    private readonly SemaphoreSlim _changed = new(0, 1);

    private int _moved;

    /// <summary>Creates the indexer.</summary>
    /// <param name="scopes">Provides a scope per scan, because the index holds a database context.</param>
    /// <param name="options">Which folder to watch, and how long to let it settle.</param>
    /// <param name="events">Receives what a scan changed.</param>
    /// <param name="logger">Records scans and watcher trouble.</param>
    public RecordIndexer(
        IServiceScopeFactory scopes,
        RecordIndexOptions options,
        IBridgeEventPublisher events,
        ILogger<RecordIndexer> logger)
    {
        _scopes = scopes ?? throw new ArgumentNullException(nameof(scopes));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _events = events ?? throw new ArgumentNullException(nameof(events));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // The first scan reads every capture the user has; the host must not wait for it.
        await Task.Yield();

        // Watching before scanning, not after: a capture written in between is then either caught
        // by the watcher or still found by the scan. The other order has a gap where it is neither.
        var watcher = Watch();
        _options.Invalidated += OnInvalidated;

        try
        {
            await ScanAsync(stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await _changed.WaitAsync(stoppingToken);
                    await SettleAsync(stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    return;
                }

                if (Interlocked.Exchange(ref _moved, 0) == 1)
                {
                    // The user pointed the service at another folder. The old watcher is reporting
                    // about a place nobody is looking at any more.
                    watcher?.Dispose();
                    watcher = Watch();
                }

                await ScanAsync(stoppingToken);
            }
        }
        finally
        {
            _options.Invalidated -= OnInvalidated;
            watcher?.Dispose();
        }
    }

    private void OnInvalidated(bool moved)
    {
        if (moved)
        {
            Interlocked.Exchange(ref _moved, 1);
        }

        Signal();
    }

    /// <summary>
    /// Waits until the folder has been quiet for the settle delay.
    /// </summary>
    /// <remarks>
    /// Writing one capture raises several events, and the last of them can arrive well after the
    /// first on a long record. Scanning on the first would read a half-written file, and the retry
    /// would then have to wait for the next change to arrive.
    /// </remarks>
    private async Task SettleAsync(CancellationToken stoppingToken)
    {
        while (await _changed.WaitAsync(_options.SettleDelay, stoppingToken))
        {
        }
    }

    private async Task ScanAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = _scopes.CreateScope();
            var index = scope.ServiceProvider.GetRequiredService<RecordIndex>();

            var result = await index.ScanAsync(stoppingToken);

            if (!result.IsEmpty)
            {
                _events.Publish(
                    BridgeEventTypes.RecordsChanged,
                    new RecordsChangedDto(result.Added, result.Updated, result.Removed));
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Shutting down.
        }
        catch (Exception exception)
        {
            // One bad scan - a locked database, a folder that vanished mid-walk - must not end the
            // indexer for the rest of the session.
            _logger.LogError(exception, "Scanning '{Directory}' failed.", _options.CaptureDirectory);
        }
    }

    private FileSystemWatcher? Watch()
    {
        try
        {
            // The service owns this folder; creating it is what makes the watcher possible at all,
            // and it is where captures are about to be written anyway.
            Directory.CreateDirectory(_options.CaptureDirectory);

            var watcher = new FileSystemWatcher(_options.CaptureDirectory, "*" + RecordFileReader.Extension)
            {
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size,
            };

            watcher.Created += OnChanged;
            watcher.Changed += OnChanged;
            watcher.Deleted += OnChanged;
            watcher.Renamed += OnChanged;

            // A full buffer drops events. The scan does not depend on which ones, so it is enough
            // to treat the overflow as a change like any other.
            watcher.Error += OnError;

            watcher.EnableRaisingEvents = true;

            return watcher;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            // Without a watcher the index still holds what the start-up scan found, which beats
            // refusing to serve records at all.
            _logger.LogWarning(exception, "Cannot watch '{Directory}' for changes.", _options.CaptureDirectory);

            return null;
        }
    }

    private void OnChanged(object sender, FileSystemEventArgs e) => Signal();

    private void OnError(object sender, ErrorEventArgs e)
    {
        _logger.LogWarning(e.GetException(), "Watching '{Directory}' reported an error.", _options.CaptureDirectory);
        Signal();
    }

    private void Signal()
    {
        try
        {
            _changed.Release();
        }
        catch (SemaphoreFullException)
        {
            // A scan is already pending, and one scan covers every change that led to it.
        }
    }
}
