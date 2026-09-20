using System.Text.Json;
using CapFrameX.Service.Analysis;
using CapFrameX.Service.Application.Records;
using CapFrameX.Service.Contracts.Analysis;
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
/// <param name="analyzer">Reads and analyses the capture behind a record.</param>
[ApiController]
[Route("api/records")]
public sealed class RecordsController(CapFrameXDbContext context, RecordAnalyzer analyzer) : ControllerBase
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

    /// <summary>Analyses one indexed capture.</summary>
    /// <param name="id">Identity of the record.</param>
    /// <param name="run">Which run, or nothing for the whole capture.</param>
    /// <param name="start">Where the window begins, in seconds.</param>
    /// <param name="end">Where the window ends, in seconds.</param>
    /// <param name="outliers">How to remove outliers first.</param>
    /// <param name="metrics">Comma-separated metric keys, in the order the tiles show them.</param>
    /// <param name="lshape">Whether the L-shape is drawn over frame times or frame rate.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    [HttpGet("{id:guid}/analysis")]
    public async Task<ActionResult<AnalysisDto>> Analysis(
        Guid id,
        [FromQuery] int? run,
        [FromQuery] double? start,
        [FromQuery] double? end,
        [FromQuery] string? outliers,
        [FromQuery] string? metrics,
        [FromQuery] string? lshape,
        CancellationToken cancellationToken)
    {
        if (!AnalysisQuery.TryMetrics(metrics, out var wanted, out var error) ||
            !AnalysisQuery.TryOutliers(outliers, out var method, out error) ||
            !AnalysisQuery.TryLShape(lshape, out var lShapeMetric, out error))
        {
            return BadRequest(error);
        }

        var request = new AnalysisRequest
        {
            Run = run,
            StartSeconds = start,
            EndSeconds = end,
            OutlierMethod = method,
            Metrics = wanted,
            LShapeMetric = lShapeMetric,
        };

        try
        {
            var (load, analysis) = await analyzer.AnalyzeAsync(id, request, cancellationToken);

            return analysis is null ? Failed(load) : Ok(analysis);
        }
        catch (ArgumentOutOfRangeException exception)
        {
            // Asking for a run the capture does not have is the caller's mistake, not a fault.
            return BadRequest(exception.Message);
        }
    }

    /// <summary>Returns the curves behind the chart for one indexed capture.</summary>
    /// <param name="id">Identity of the record.</param>
    /// <param name="run">Which run, or nothing for the whole capture.</param>
    /// <param name="start">Where the window begins, in seconds.</param>
    /// <param name="end">Where the window ends, in seconds.</param>
    /// <param name="kinds">Comma-separated curve names.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    [HttpGet("{id:guid}/series")]
    public async Task<ActionResult<SeriesResponse>> Series(
        Guid id,
        [FromQuery] int? run,
        [FromQuery] double? start,
        [FromQuery] double? end,
        [FromQuery] string? kinds,
        CancellationToken cancellationToken)
    {
        if (!AnalysisQuery.TryKinds(kinds, out var wanted, out var error))
        {
            return BadRequest(error);
        }

        var request = new SeriesRequest
        {
            Run = run,
            StartSeconds = start,
            EndSeconds = end,
            Kinds = wanted,
        };

        try
        {
            var (load, series) = await analyzer.SeriesAsync(id, request, cancellationToken);

            return series is null ? Failed(load) : Ok(series);
        }
        catch (ArgumentOutOfRangeException exception)
        {
            return BadRequest(exception.Message);
        }
    }

    private ObjectResult BadRequest(string? detail) =>
        Problem(
            title: "The request could not be read.",
            detail: detail,
            statusCode: StatusCodes.Status400BadRequest);

    private ObjectResult Failed(RecordLoad load) =>
        load.Status switch
        {
            RecordLoadStatus.NotIndexed => Problem(
                title: "Record not found.",
                detail: load.Error,
                statusCode: StatusCodes.Status404NotFound),

            // The record is there, the capture behind it is not: a state of the folder rather than
            // a bad request, and one the indexer will resolve by itself.
            _ => Problem(
                title: "The capture behind this record cannot be read.",
                detail: load.Error,
                statusCode: StatusCodes.Status409Conflict),
        };

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
            AverageFps: session.AverageFps,
            P1Fps: session.P1Fps,
            P99Fps: session.P99Fps);

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
