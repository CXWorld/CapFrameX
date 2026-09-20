using System.Linq.Expressions;
using CapFrameX.Service.Contracts.Records;
using CapFrameX.Service.Data;
using CapFrameX.Service.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace CapFrameX.Service.Application.Records;

/// <summary>What the record list can be ordered by.</summary>
public enum RecordSortField
{
    /// <summary>When the capture was taken.</summary>
    Created,

    /// <summary>Game name.</summary>
    Game,

    /// <summary>Executable name.</summary>
    Process,

    /// <summary>How long the capture runs.</summary>
    Duration,

    /// <summary>How many frames it holds.</summary>
    Frames,

    /// <summary>Average frame rate.</summary>
    AverageFps,

    /// <summary>1st percentile frame rate.</summary>
    P1Fps,

    /// <summary>99th percentile frame rate.</summary>
    P99Fps,
}

/// <summary>One page of the record list, as the caller asked for it.</summary>
public sealed record RecordListRequest
{
    /// <summary>Largest page the library hands out at once.</summary>
    public const int MaximumPageSize = 500;

    /// <summary>Page size when the caller does not ask for one.</summary>
    public const int DefaultPageSize = 200;

    /// <summary>Free text, matched against game, process and the file the record came from.</summary>
    public string? Search { get; init; }

    /// <summary>One game, matched exactly - what a filter chip selects.</summary>
    public string? Game { get; init; }

    /// <summary>Earliest capture time to include.</summary>
    public DateTimeOffset? From { get; init; }

    /// <summary>Latest capture time to include, inclusive.</summary>
    public DateTimeOffset? To { get; init; }

    /// <summary>What to order by.</summary>
    public RecordSortField Sort { get; init; } = RecordSortField.Created;

    /// <summary>Whether to order the other way round.</summary>
    public bool Descending { get; init; } = true;

    /// <summary>Records to skip.</summary>
    public int Skip { get; init; }

    /// <summary>Records to return.</summary>
    public int Take { get; init; } = DefaultPageSize;
}

/// <summary>
/// The record list.
/// </summary>
/// <remarks>
/// Served from the index rather than from the capture folder: the list has to appear at once even
/// with thousands of records, and reading every file for it would take seconds and a lot of memory
/// for a handful of fields per capture.
/// </remarks>
/// <param name="context">The service database.</param>
public sealed class RecordLibrary(CapFrameXDbContext context)
{
    /// <summary>Returns one page of the list.</summary>
    /// <param name="request">What to include, in which order.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    public async Task<RecordsListResponse> ListAsync(
        RecordListRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var query = Filter(context.Sessions.AsNoTracking(), request);
        var total = await query.CountAsync(cancellationToken);

        var records = await Order(query, request)
            .Skip(Math.Max(request.Skip, 0))
            .Take(Math.Clamp(request.Take, 1, RecordListRequest.MaximumPageSize))
            .ToListAsync(cancellationToken);

        return new RecordsListResponse([.. records.Select(RecordProjection.Summary)], total);
    }

    /// <summary>The games the index holds, for the filter the list offers.</summary>
    /// <param name="cancellationToken">Cancels the query.</param>
    public async Task<IReadOnlyList<string>> GamesAsync(CancellationToken cancellationToken = default) =>
        await context.Sessions
            .AsNoTracking()
            .Where(record => record.GameName != "")
            .Select(record => record.GameName)
            .Distinct()
            .OrderBy(game => game)
            .ToListAsync(cancellationToken);

    private static IQueryable<Session> Filter(IQueryable<Session> query, RecordListRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim().ToLowerInvariant();

            // Game, process and the file the record came from. The last one matters because the
            // list shows a file name: searching for what is on screen has to find it, and the
            // record's display name is derived from that path.
            //
            // Not LIKE: the text comes from a search box, where '%' and '_' are characters rather
            // than wildcards.
            query = query.Where(record =>
                record.GameName.ToLower().Contains(term) ||
                record.ProcessName.ToLower().Contains(term) ||
                (record.SourceFilePath != null && record.SourceFilePath.ToLower().Contains(term)) ||
                (record.ImportedFrom != null && record.ImportedFrom.ToLower().Contains(term)));
        }

        if (!string.IsNullOrWhiteSpace(request.Game))
        {
            // Exactly, because this is a chip the user picked from the games that exist, not
            // something they typed.
            var game = request.Game.Trim().ToLowerInvariant();

            query = query.Where(record => record.GameName.ToLower() == game);
        }

        if (request.From is { } from)
        {
            var start = from.UtcDateTime;

            query = query.Where(record => record.CreatedAt >= start);
        }

        if (request.To is { } to)
        {
            var end = to.UtcDateTime;

            query = query.Where(record => record.CreatedAt <= end);
        }

        return query;
    }

    /// <summary>
    /// Orders the page.
    /// </summary>
    /// <remarks>
    /// Two things the obvious version gets wrong. A record whose metric was never computed sorts
    /// last either way rather than heading the list on an ascending sort, because "not measured" is
    /// not "slowest". And every order ends in the identity: without a tiebreaker the database may
    /// return equal rows in any order it likes, and a page boundary would then repeat one record
    /// and skip another.
    /// </remarks>
    private static IQueryable<Session> Order(IQueryable<Session> query, RecordListRequest request)
    {
        var down = request.Descending;

        var ordered = request.Sort switch
        {
            RecordSortField.Game => down
                ? query.OrderByDescending(record => record.GameName)
                : query.OrderBy(record => record.GameName),
            RecordSortField.Process => down
                ? query.OrderByDescending(record => record.ProcessName)
                : query.OrderBy(record => record.ProcessName),
            RecordSortField.Duration => Measured(
                query, record => record.DurationSeconds == null, record => record.DurationSeconds, down),
            RecordSortField.Frames => Measured(
                query, record => record.FrameCount == null, record => record.FrameCount, down),
            RecordSortField.AverageFps => Measured(
                query, record => record.AverageFps == null, record => record.AverageFps, down),
            RecordSortField.P1Fps => Measured(
                query, record => record.P1Fps == null, record => record.P1Fps, down),
            RecordSortField.P99Fps => Measured(
                query, record => record.P99Fps == null, record => record.P99Fps, down),
            _ => down
                ? query.OrderByDescending(record => record.CreatedAt)
                : query.OrderBy(record => record.CreatedAt),
        };

        return down ? ordered.ThenByDescending(record => record.Id) : ordered.ThenBy(record => record.Id);
    }

    private static IOrderedQueryable<Session> Measured<TValue>(
        IQueryable<Session> query,
        Expression<Func<Session, bool>> missing,
        Expression<Func<Session, TValue?>> value,
        bool descending)
        where TValue : struct
    {
        var measuredFirst = query.OrderBy(missing);

        return descending ? measuredFirst.ThenByDescending(value) : measuredFirst.ThenBy(value);
    }
}
