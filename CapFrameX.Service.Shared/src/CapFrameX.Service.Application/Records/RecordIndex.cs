using System.Text.Json;
using CapFrameX.Service.Analysis;
using CapFrameX.Service.Contracts.Analysis;
using CapFrameX.Service.Data;
using CapFrameX.Service.Data.Models;
using CapFrameX.Service.Records;
using CapFrameX.Statistics.NetStandard.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CapFrameX.Service.Application.Records;

/// <summary>
/// Brings the index in line with the capture folder.
/// </summary>
/// <remarks>
/// The capture file stays the record. This only projects what the record list needs out of it, so
/// a folder the user fills by hand, a share, or captures written by CapFrameX 1.x are all in the
/// list without anything being copied or converted.
/// </remarks>
public sealed class RecordIndex
{
    /// <summary>Name of the suite that holds captures found in the folder.</summary>
    public const string DefaultSuiteName = "Captures";

    /// <summary>
    /// Identity of that suite. Fixed rather than generated so a rebuilt index does not leave a
    /// second one behind.
    /// </summary>
    public static readonly Guid DefaultSuiteId = new("b6f0f5c0-6a58-4a2f-9c3d-0a7f1c2e4d10");

    /// <summary>
    /// The metrics the record list shows, computed once per record at index time.
    /// </summary>
    /// <remarks>
    /// Through the analysis adapter rather than a second calculation here: a list that disagreed
    /// with the record it opens would be worse than a list with no numbers at all.
    /// </remarks>
    private static readonly AnalysisRequest ListMetrics = new()
    {
        Metrics = [EMetric.Average, EMetric.P1, EMetric.P99],
    };

    private readonly CapFrameXDbContext _context;
    private readonly RecordFileReader _reader;
    private readonly AnalysisService _analysis;
    private readonly RecordIndexOptions _options;
    private readonly ILogger<RecordIndex> _logger;

    /// <summary>Creates the index over one database and one folder.</summary>
    /// <param name="context">The service database.</param>
    /// <param name="reader">Reads the capture files.</param>
    /// <param name="analysis">Computes the metrics the record list shows.</param>
    /// <param name="options">Which folder, and how the projection is built.</param>
    /// <param name="logger">Receives what a scan did and what it could not read.</param>
    public RecordIndex(
        CapFrameXDbContext context,
        RecordFileReader reader,
        AnalysisService analysis,
        RecordIndexOptions options,
        ILogger<RecordIndex> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _reader = reader ?? throw new ArgumentNullException(nameof(reader));
        _analysis = analysis ?? throw new ArgumentNullException(nameof(analysis));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>Compares the folder against the index and applies the difference.</summary>
    /// <param name="cancellationToken">Cancels the scan.</param>
    public async Task<RecordIndexResult> ScanAsync(CancellationToken cancellationToken = default)
    {
        var plan = RecordIndexPlanner.Plan(OnDisk(), await IndexedAsync(cancellationToken));

        if (plan.IsEmpty)
        {
            return default;
        }

        var added = 0;
        var updated = 0;
        var failed = 0;

        foreach (var file in plan.Added)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var projection = await SummaryAsync(file, cancellationToken);

            if (projection is null)
            {
                failed++;
                continue;
            }

            var session = new Session { Id = Guid.NewGuid(), SuiteId = await SuiteIdAsync(cancellationToken) };
            Apply(session, projection, file);
            _context.Sessions.Add(session);
            added++;
        }

        foreach (var file in plan.Updated)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var session = await _context.Sessions.FirstOrDefaultAsync(
                candidate => candidate.SourceFilePath == file.Path,
                cancellationToken);

            if (session is null)
            {
                // Between the listing and here somebody removed the row; the next scan adds it.
                continue;
            }

            var projection = await SummaryAsync(file, cancellationToken);

            if (projection is null)
            {
                // The row keeps the size and time it was indexed with, so the next scan sees the
                // file as changed again and retries it.
                failed++;
                continue;
            }

            Apply(session, projection, file);
            updated++;
        }

        foreach (var record in plan.Removed)
        {
            _context.Sessions.Remove(new Session { Id = record.Id });
        }

        await _context.SaveChangesAsync(cancellationToken);

        var result = new RecordIndexResult(added, updated, plan.Removed.Count, failed);

        _logger.LogInformation(
            "Record index: {Added} added, {Updated} updated, {Removed} removed, {Failed} unreadable in '{Directory}'.",
            result.Added,
            result.Updated,
            result.Removed,
            result.Failed,
            _options.CaptureDirectory);

        return result;
    }

    /// <summary>
    /// Re-reads one record from its file.
    /// </summary>
    /// <remarks>
    /// After the service itself changed a capture, so the answer to the next request is the edited
    /// one rather than what the watcher will get round to in a second or two. It writes the file's
    /// new size and time as well, which is what keeps that scan from finding work that is already
    /// done.
    /// </remarks>
    /// <param name="id">Identity of the record.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>Whether the row now describes the file.</returns>
    public async Task<bool> RefreshAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var record = await _context.Sessions.FirstOrDefaultAsync(session => session.Id == id, cancellationToken);

        if (record?.SourceFilePath is not { Length: > 0 } path)
        {
            return false;
        }

        var info = new FileInfo(path);

        if (!info.Exists)
        {
            return false;
        }

        var file = new RecordFile(path, info.Length, info.LastWriteTimeUtc);
        var projection = await SummaryAsync(file, cancellationToken);

        if (projection is null)
        {
            return false;
        }

        Apply(record, projection, file);
        await _context.SaveChangesAsync(cancellationToken);

        return true;
    }

    private List<RecordFile> OnDisk()
    {
        var directory = _options.CaptureDirectory;

        if (!Directory.Exists(directory))
        {
            // The folder appears when the user first captures. Until then there is nothing to
            // compare against - which is not the same as everything having been deleted, so the
            // plan below must not see an empty listing here.
            return [];
        }

        var files = new List<RecordFile>();

        foreach (var path in Directory.EnumerateFiles(
            directory,
            "*" + RecordFileReader.Extension,
            SearchOption.AllDirectories))
        {
            FileInfo info;

            try
            {
                info = new FileInfo(path);
                _ = info.Length;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // Deleted or locked between the listing and the stat: it is simply not there.
                continue;
            }

            files.Add(new RecordFile(path, info.Length, info.LastWriteTimeUtc));
        }

        return files;
    }

    private async Task<List<IndexedRecord>> IndexedAsync(CancellationToken cancellationToken)
    {
        var rows = await _context.Sessions
            .AsNoTracking()
            .Where(session => session.SourceFilePath != null)
            .Select(session => new
            {
                session.Id,
                session.SourceFilePath,
                session.SourceFileSize,
                session.SourceModifiedUtc,
                session.IndexVersion,
            })
            .ToListAsync(cancellationToken);

        return rows
            .Select(row => new IndexedRecord(
                row.Id,
                row.SourceFilePath!,
                row.SourceFileSize ?? 0,
                row.SourceModifiedUtc ?? default,
                row.IndexVersion))
            .ToList();
    }

    /// <summary>What one capture contributes to the index.</summary>
    /// <param name="Summary">The fields the list shows.</param>
    /// <param name="Metrics">The frame-rate metrics it shows beside them.</param>
    private sealed record Projection(RecordSummary Summary, IReadOnlyList<MetricDto> Metrics);

    private async Task<Projection?> SummaryAsync(RecordFile file, CancellationToken cancellationToken)
    {
        var read = await _reader.ReadAsync(file.Path, cancellationToken);

        if (read.Session is null)
        {
            _logger.LogDebug("Record index: {Error}", read.Error);

            return null;
        }

        return new Projection(
            RecordSummaryFactory.Create(read.Session, file.Path, _options.SparklinePoints),
            _analysis.Analyze(read.Session, ListMetrics).Metrics);
    }

    private static void Apply(Session session, Projection projection, RecordFile file)
    {
        var summary = projection.Summary;
        session.GameName = summary.GameName ?? string.Empty;
        session.ProcessName = summary.ProcessName ?? string.Empty;
        session.Processor = summary.Processor ?? string.Empty;
        session.Gpu = summary.Gpu ?? string.Empty;
        session.CreatedAt = summary.CreatedAt.UtcDateTime;

        session.SourceFilePath = file.Path;
        session.SourceFileSize = file.Size;
        session.SourceModifiedUtc = file.ModifiedUtc;
        session.IndexVersion = RecordIndexPlanner.CurrentIndexVersion;

        session.DurationSeconds = summary.DurationSeconds;
        session.RunCount = summary.RunCount;
        session.FrameCount = summary.FrameCount;
        session.SparklineJson = JsonSerializer.Serialize(summary.Sparkline);
        session.HasPcLatency = summary.HasPcLatency;
        session.HasDisplayChange = summary.HasDisplayChange;

        session.AverageFps = Metric(projection, EMetric.Average);
        session.P1Fps = Metric(projection, EMetric.P1);
        session.P99Fps = Metric(projection, EMetric.P99);
    }

    private static double? Metric(Projection projection, EMetric metric) =>
        projection.Metrics.FirstOrDefault(value => value.Key == MetricCatalog.Key(metric))?.Value;

    private async Task<Guid> SuiteIdAsync(CancellationToken cancellationToken)
    {
        var known = await _context.Suites.FindAsync([DefaultSuiteId], cancellationToken);

        if (known is not null)
        {
            return known.Id;
        }

        var now = DateTime.UtcNow;

        _context.Suites.Add(new Suite
        {
            Id = DefaultSuiteId,
            Name = DefaultSuiteName,
            Description = "Captures found in the capture folder.",
            Type = SuiteType.Miscellaneous,
            CreatedAt = now,
            UpdatedAt = now,
        });

        return DefaultSuiteId;
    }
}
