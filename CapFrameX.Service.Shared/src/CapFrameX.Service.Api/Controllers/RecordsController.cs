using System.Text.Json;
using CapFrameX.Service.Contracts.Records;
using CapFrameX.Service.Data;
using CapFrameX.Service.Data.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CapFrameX.Service.Api.Controllers;

/// <summary>
/// The captures the service has indexed.
/// </summary>
/// <remarks>
/// Served from the index rather than from the capture folder: the list has to appear at once even
/// with thousands of records, and reading every file for it would take seconds and a lot of memory
/// for a handful of fields per capture.
/// </remarks>
/// <param name="context">The service database.</param>
[ApiController]
[Route("api/records")]
public sealed class RecordsController(CapFrameXDbContext context) : ControllerBase
{
    /// <summary>Largest page the API hands out at once.</summary>
    public const int MaximumPageSize = 500;

    /// <summary>Page size when the caller does not ask for one.</summary>
    public const int DefaultPageSize = 200;

    /// <summary>Lists the indexed captures, newest first.</summary>
    /// <param name="search">Matches game or process name; case is ignored.</param>
    /// <param name="skip">Records to skip.</param>
    /// <param name="take">Records to return, capped at <see cref="MaximumPageSize"/>.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    [HttpGet]
    public async Task<ActionResult<RecordsListResponse>> List(
        [FromQuery] string? search = null,
        [FromQuery] int skip = 0,
        [FromQuery] int take = DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        var query = context.Sessions.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLowerInvariant();

            // Not LIKE: the user types into a search box, where '%' and '_' are characters rather
            // than wildcards.
            query = query.Where(session =>
                session.GameName.ToLower().Contains(term) ||
                session.ProcessName.ToLower().Contains(term));
        }

        var total = await query.CountAsync(cancellationToken);

        var sessions = await query
            .OrderByDescending(session => session.CreatedAt)
            .Skip(Math.Max(skip, 0))
            .Take(Math.Clamp(take, 1, MaximumPageSize))
            .ToListAsync(cancellationToken);

        return Ok(new RecordsListResponse(sessions.Select(Summary).ToArray(), total));
    }

    /// <summary>Returns one indexed capture.</summary>
    /// <param name="id">Identity of the record.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<RecordSummaryDto>> Get(Guid id, CancellationToken cancellationToken)
    {
        var session = await context.Sessions
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);

        if (session is null)
        {
            return Problem(
                title: "Record not found.",
                detail: $"No record with id '{id}' is indexed.",
                statusCode: StatusCodes.Status404NotFound);
        }

        return Ok(Summary(session));
    }

    private static RecordSummaryDto Summary(Session session) =>
        new(
            Id: session.Id,
            Name: Name(session),
            GameName: NullIfBlank(session.GameName),
            ProcessName: NullIfBlank(session.ProcessName),
            CreatedAt: new DateTimeOffset(DateTime.SpecifyKind(session.CreatedAt, DateTimeKind.Utc)),
            DurationSeconds: session.DurationSeconds ?? 0,
            RunCount: session.RunCount ?? 0,
            FrameCount: session.FrameCount ?? 0,
            Sparkline: Sparkline(session.SparklineJson),
            Processor: NullIfBlank(session.Processor),
            Gpu: NullIfBlank(session.Gpu),
            HasPcLatency: session.HasPcLatency,
            HasDisplayChange: session.HasDisplayChange,
            // Filled once the record has been analysed; the index itself computes no metrics.
            AverageFps: null,
            P1Fps: null,
            P99Fps: null);

    private static string Name(Session session)
    {
        var fileName = session.SourceFilePath is null
            ? null
            : Path.GetFileNameWithoutExtension(session.SourceFilePath);

        return NullIfBlank(fileName) ?? NullIfBlank(session.GameName) ?? session.Id.ToString();
    }

    private static IReadOnlyList<double> Sparkline(string? json)
    {
        if (string.IsNullOrEmpty(json))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<double[]>(json) ?? [];
        }
        catch (JsonException)
        {
            // Written by an older indexer, or by hand: the list is better without it than not at
            // all.
            return [];
        }
    }

    private static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
}
