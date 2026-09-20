using CapFrameX.Service.Analysis;
using CapFrameX.Service.Application.Records;
using CapFrameX.Service.Application.Settings;
using CapFrameX.Service.Contracts.Analysis;
using CapFrameX.Service.Contracts.Records;
using Microsoft.AspNetCore.Mvc;

namespace CapFrameX.Service.Api.Controllers;

/// <summary>
/// The captures the service has indexed.
/// </summary>
/// <remarks>
/// Served from the index rather than from the capture folder: the list has to appear at once even
/// with thousands of records, and reading every file for it would take seconds and a lot of memory
/// for a handful of fields per capture.
/// </remarks>
/// <param name="library">Reads the list.</param>
/// <param name="analyzer">Reads and analyses the capture behind a record.</param>
/// <param name="store">Changes a record, or removes it.</param>
/// <param name="settings">Supplies the analysis options the user configured.</param>
[ApiController]
[Route("api/records")]
public sealed class RecordsController(
    RecordLibrary library,
    RecordAnalyzer analyzer,
    RecordStore store,
    SettingsStore settings) : ControllerBase
{
    /// <summary>Lists the indexed captures, newest first.</summary>
    /// <param name="search">Free text, matched against game and process name; case is ignored.</param>
    /// <param name="game">One game, matched exactly - what a filter chip selects.</param>
    /// <param name="from">Earliest capture time to include.</param>
    /// <param name="to">Latest capture time to include, inclusive.</param>
    /// <param name="sort">
    /// What to order by, optionally prefixed with '-' to reverse it; defaults to newest first.
    /// </param>
    /// <param name="skip">Records to skip.</param>
    /// <param name="take">
    /// Records to return, capped at <see cref="RecordListRequest.MaximumPageSize"/>.
    /// </param>
    /// <param name="cancellationToken">Cancels the query.</param>
    [HttpGet]
    public async Task<ActionResult<RecordsListResponse>> List(
        [FromQuery] string? search = null,
        [FromQuery] string? game = null,
        [FromQuery] DateTimeOffset? from = null,
        [FromQuery] DateTimeOffset? to = null,
        [FromQuery] string? sort = null,
        [FromQuery] int skip = 0,
        [FromQuery] int take = RecordListRequest.DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        if (!RecordSort.TryParse(sort, out var field, out var descending, out var error))
        {
            return BadRequest(error);
        }

        var request = new RecordListRequest
        {
            Search = search,
            Game = game,
            From = from,
            To = to,
            Sort = field,
            Descending = descending,
            Skip = skip,
            Take = take,
        };

        return Ok(await library.ListAsync(request, cancellationToken));
    }

    /// <summary>The games the index holds, for the filter the list offers.</summary>
    /// <param name="cancellationToken">Cancels the query.</param>
    [HttpGet("games")]
    public async Task<ActionResult<IReadOnlyList<string>>> Games(CancellationToken cancellationToken) =>
        Ok(await library.GamesAsync(cancellationToken));

    /// <summary>Returns everything the analysis view needs to open one record.</summary>
    /// <param name="id">Identity of the record.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<RecordDetailDto>> Get(Guid id, CancellationToken cancellationToken)
    {
        var (load, detail) = await analyzer.DetailAsync(id, cancellationToken);

        return detail is null ? Failed(load) : Ok(detail);
    }

    /// <summary>Corrects what a capture recorded about itself.</summary>
    /// <remarks>
    /// The change is written into the capture file, so it survives being copied elsewhere and
    /// CapFrameX 1.x sees it too. A field left out of the body is left alone.
    /// </remarks>
    /// <param name="id">Identity of the record.</param>
    /// <param name="edit">What to change.</param>
    /// <param name="cancellationToken">Cancels the change.</param>
    [HttpPatch("{id:guid}")]
    public async Task<ActionResult<RecordDetailDto>> Patch(
        Guid id,
        [FromBody] RecordEdit edit,
        CancellationToken cancellationToken)
    {
        if (edit is null)
        {
            return BadRequest("A patch needs a body naming the fields to change.");
        }

        var change = await store.EditAsync(id, edit, cancellationToken);

        if (!change.IsSuccess)
        {
            return Refused(change);
        }

        // The edited record, so the client does not have to ask again to see what it now says.
        var (load, detail) = await analyzer.DetailAsync(id, cancellationToken);

        return detail is null ? Failed(load) : Ok(detail);
    }

    /// <summary>Moves a capture to the platform's trash and drops it from the index.</summary>
    /// <remarks>
    /// Never an outright delete: a record is hours of benchmarking that cannot be recaptured, and
    /// the recycle bin - or the freedesktop trash on Linux - is where the user can get it back with
    /// the tools they already know.
    /// </remarks>
    /// <param name="id">Identity of the record.</param>
    /// <param name="cancellationToken">Cancels the removal.</param>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var change = await store.DeleteAsync(id, cancellationToken);

        return change.IsSuccess ? NoContent() : Refused(change);
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

        // What the user configured, unless this request says otherwise - so opening a record gives
        // the tiles and the curve they chose without the frontend repeating them every time.
        var defaults = settings.DefaultAnalysisRequest();

        var request = defaults with
        {
            Run = run,
            StartSeconds = start,
            EndSeconds = end,
            OutlierMethod = string.IsNullOrWhiteSpace(outliers) ? defaults.OutlierMethod : method,
            Metrics = wanted ?? defaults.Metrics,
            LShapeMetric = string.IsNullOrWhiteSpace(lshape) ? defaults.LShapeMetric : lShapeMetric,
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

    private ObjectResult Refused(RecordChange change) =>
        change.Status switch
        {
            RecordChangeStatus.NotIndexed => Problem(
                title: "Record not found.",
                detail: change.Error,
                statusCode: StatusCodes.Status404NotFound),

            // The record is there; the capture behind it is not in a state the service can change.
            // That is a fact about the folder, not a bad request.
            _ => Problem(
                title: "The capture behind this record could not be changed.",
                detail: change.Error,
                statusCode: StatusCodes.Status409Conflict),
        };

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

}
