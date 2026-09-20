using CapFrameX.Data.Session.Contracts;
using CapFrameX.Service.Analysis;
using CapFrameX.Service.Contracts.Analysis;
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
            .Where(session => session.Id == id)
            .Select(session => new
            {
                session.SourceFilePath,
                session.SourceFileSize,
                session.SourceModifiedUtc,
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (record is null)
        {
            return new RecordLoad(RecordLoadStatus.NotIndexed, null, $"No record with id '{id}' is indexed.");
        }

        if (string.IsNullOrEmpty(record.SourceFilePath))
        {
            return new RecordLoad(
                RecordLoadStatus.NoSourceFile,
                null,
                $"Record '{id}' was recorded by the service and has no capture file.");
        }

        var key = new SessionCacheKey(id, record.SourceFileSize ?? 0, record.SourceModifiedUtc ?? default);

        if (cache.TryGet(key, out var cached) && cached is not null)
        {
            return new RecordLoad(RecordLoadStatus.Ok, cached, null);
        }

        var read = await reader.ReadAsync(record.SourceFilePath, cancellationToken);

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
