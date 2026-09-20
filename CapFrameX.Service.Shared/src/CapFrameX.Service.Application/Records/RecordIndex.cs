using System.Text.Json;
using CapFrameX.Service.Data;
using CapFrameX.Service.Data.Models;
using CapFrameX.Service.Records;
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

    private readonly CapFrameXDbContext _context;
    private readonly RecordFileReader _reader;
    private readonly RecordIndexOptions _options;
    private readonly ILogger<RecordIndex> _logger;

    /// <summary>Creates the index over one database and one folder.</summary>
    /// <param name="context">The service database.</param>
    /// <param name="reader">Reads the capture files.</param>
    /// <param name="options">Which folder, and how the projection is built.</param>
    /// <param name="logger">Receives what a scan did and what it could not read.</param>
    public RecordIndex(
        CapFrameXDbContext context,
        RecordFileReader reader,
        RecordIndexOptions options,
        ILogger<RecordIndex> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _reader = reader ?? throw new ArgumentNullException(nameof(reader));
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

            var summary = await SummaryAsync(file, cancellationToken);

            if (summary is null)
            {
                failed++;
                continue;
            }

            var session = new Session { Id = Guid.NewGuid(), SuiteId = await SuiteIdAsync(cancellationToken) };
            Apply(session, summary, file);
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

            var summary = await SummaryAsync(file, cancellationToken);

            if (summary is null)
            {
                // The row keeps the size and time it was indexed with, so the next scan sees the
                // file as changed again and retries it.
                failed++;
                continue;
            }

            Apply(session, summary, file);
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

    private async Task<RecordSummary?> SummaryAsync(RecordFile file, CancellationToken cancellationToken)
    {
        var read = await _reader.ReadAsync(file.Path, cancellationToken);

        if (read.Session is null)
        {
            _logger.LogDebug("Record index: {Error}", read.Error);

            return null;
        }

        return RecordSummaryFactory.Create(read.Session, file.Path, _options.SparklinePoints);
    }

    private void Apply(Session session, RecordSummary summary, RecordFile file)
    {
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
    }

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
