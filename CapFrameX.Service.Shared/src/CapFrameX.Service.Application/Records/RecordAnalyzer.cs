using CapFrameX.Data.Session.Contracts;
using CapFrameX.Service.Analysis;
using CapFrameX.Service.Contracts.Analysis;
using CapFrameX.Service.Contracts.Records;
using CapFrameX.Service.Data;
using CapFrameX.Service.Records;
using Microsoft.EntityFrameworkCore;

namespace CapFrameX.Service.Application.Records;

/// <summary>Why a record could not be analysed.</summary>
public enum RecordLoadStatus
{
    /// <summary>The capture was read.</summary>
    Ok,

    /// <summary>No record with that identity is indexed.</summary>
    NotIndexed,

    /// <summary>The record has no capture file behind it, so there is nothing to read.</summary>
    NoSourceFile,

    /// <summary>The file is there but could not be used.</summary>
    Unreadable,
}

/// <summary>The outcome of loading one record.</summary>
/// <param name="Status">Whether it worked, and if not, why.</param>
/// <param name="Session">The parsed capture, on success.</param>
/// <param name="Error">What went wrong, phrased for the caller.</param>
public readonly record struct RecordLoad(RecordLoadStatus Status, ISession? Session, string? Error);

/// <summary>
/// Answers questions about one indexed record.
/// </summary>
/// <remarks>
/// The index knows where a capture lives; the file holds its frames; the analysis turns those into
/// numbers. This is the one place that puts the three together, so the endpoints stay free of file
/// handling and the cache sits between every caller and the disk.
/// </remarks>
/// <param name="context">The service database.</param>
/// <param name="reader">Reads the capture files.</param>
/// <param name="cache">Holds recently read captures.</param>
/// <param name="analysis">Computes the numbers.</param>
public sealed class RecordAnalyzer(
    CapFrameXDbContext context,
    RecordFileReader reader,
    SessionCache cache,
    AnalysisService analysis)
{
    /// <summary>Reads one record's capture, from the cache where possible.</summary>
    /// <param name="id">Identity of the record.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    public async Task<RecordLoad> LoadAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var record = await context.Sessions
            .AsNoTracking()
            .Include(session => session.Runs)
            .FirstOrDefaultAsync(session => session.Id == id, cancellationToken);

        if (record is null)
        {
            return new RecordLoad(RecordLoadStatus.NotIndexed, null, $"No record with id '{id}' is indexed.");
        }

        // A record either carries its capture or points at one. An imported record carries it, so
        // it reads the same whether the file it came from is still there, moved, or on a drive
        // nobody plugged in.
        //
        // In the order they were recorded: the database returns rows in whatever order suits it,
        // and the statistics read the runs as one sequence, where order changes the answer.
        var runs = record.Runs.OrderBy(run => run.RunIndex).ToArray();

        if (StoredRecordFactory.HasCapture(runs))
        {
            return Stored(record, runs);
        }

        if (string.IsNullOrEmpty(record.SourceFilePath))
        {
            return new RecordLoad(
                RecordLoadStatus.NoSourceFile,
                null,
                $"Record '{id}' holds no capture and points at no file.");
        }

        return await FromFileAsync(record, cancellationToken);
    }

    private RecordLoad Stored(Data.Models.Session record, IReadOnlyList<Data.Models.SessionRun> runs)
    {
        // The row's own timestamp stands in for the file's size and time: an edit changes it, so a
        // cached copy of the old one cannot be served.
        var key = new SessionCacheKey(record.Id, 0, record.UpdatedAt);

        if (cache.TryGet(key, out var cached) && cached is not null)
        {
            return new RecordLoad(RecordLoadStatus.Ok, cached, null);
        }

        var session = StoredRecordFactory.Rebuild(record, runs);
        cache.Set(key, session);

        return new RecordLoad(RecordLoadStatus.Ok, session, null);
    }

    private async Task<RecordLoad> FromFileAsync(Data.Models.Session record, CancellationToken cancellationToken)
    {
        var key = new SessionCacheKey(record.Id, record.SourceFileSize ?? 0, record.SourceModifiedUtc ?? default);

        if (cache.TryGet(key, out var cached) && cached is not null)
        {
            return new RecordLoad(RecordLoadStatus.Ok, cached, null);
        }

        var read = await reader.ReadAsync(record.SourceFilePath!, cancellationToken);

        if (read.Session is not { } session)
        {
            // The file was indexed once, so this is the folder having changed underneath us: the
            // indexer will notice, and until it does the caller gets the reason rather than a
            // half-empty chart.
            return new RecordLoad(RecordLoadStatus.Unreadable, null, read.Error);
        }

        cache.Set(key, session);

        return new RecordLoad(RecordLoadStatus.Ok, session, null);
    }

    /// <summary>Reads everything the analysis view needs to open one record.</summary>
    /// <param name="id">Identity of the record.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    public async Task<(RecordLoad Load, RecordDetailDto? Detail)> DetailAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var record = await context.Sessions
            .AsNoTracking()
            .FirstOrDefaultAsync(session => session.Id == id, cancellationToken);

        if (record is null)
        {
            return (new RecordLoad(RecordLoadStatus.NotIndexed, null, $"No record with id '{id}' is indexed."), null);
        }

        var load = await LoadAsync(id, cancellationToken);

        if (load.Session is not { } session)
        {
            return (load, null);
        }

        var detail = new RecordDetailDto(
            Summary: RecordProjection.Summary(record),
            Info: RecordDetailFactory.Info(session),
            Runs: RecordDetailFactory.Runs(session),
            Chips: RecordDetailFactory.Chips(session),
            Source: RecordProjection.Source(record));

        return (load, detail);
    }

    /// <summary>Analyses one record.</summary>
    /// <param name="id">Identity of the record.</param>
    /// <param name="request">Which part of it, and which metrics.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    public async Task<(RecordLoad Load, AnalysisDto? Analysis)> AnalyzeAsync(
        Guid id,
        AnalysisRequest request,
        CancellationToken cancellationToken = default)
    {
        var load = await LoadAsync(id, cancellationToken);

        return (load, load.Session is null ? null : analysis.Analyze(load.Session, request));
    }

    /// <summary>Returns one record's curves.</summary>
    /// <param name="id">Identity of the record.</param>
    /// <param name="request">Which part of it, and which curves.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    public async Task<(RecordLoad Load, SeriesResponse? Series)> SeriesAsync(
        Guid id,
        SeriesRequest request,
        CancellationToken cancellationToken = default)
    {
        var load = await LoadAsync(id, cancellationToken);

        return (load, load.Session is null ? null : analysis.Series(load.Session, request));
    }
}
