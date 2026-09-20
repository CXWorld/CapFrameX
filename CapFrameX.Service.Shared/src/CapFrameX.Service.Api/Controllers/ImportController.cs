using System.Runtime.Versioning;
using CapFrameX.Service.Application.Records;
using CapFrameX.Service.Application.Settings;
using CapFrameX.Service.Contracts.Records;
using CapFrameX.Service.Core.Platform;
using CapFrameX.Service.Records;
using Microsoft.AspNetCore.Mvc;

namespace CapFrameX.Service.Api.Controllers;

/// <summary>
/// Bringing captures that already exist into the database.
/// </summary>
/// <remarks>
/// An import copies the capture in rather than pointing at it, so the record survives the file
/// being moved, deleted or left on a drive that is not attached. It is the only way records arrive
/// from outside the folder the service watches - there is no second observed directory, and
/// CapFrameX 1.x's settings are read once, to suggest where to look.
/// </remarks>
/// <param name="importer">Reads the capture files.</param>
/// <param name="paths">Supplies the folder the service already watches.</param>
/// <param name="settings">Remembers whether the first import was offered.</param>
[ApiController]
[Route("api/records/import")]
public sealed class ImportController(
    RecordImporter importer,
    IAppPaths paths,
    SettingsStore settings) : ControllerBase
{
    /// <summary>Folders worth offering to import from, and whether the user was already asked.</summary>
    [HttpGet("sources")]
    public ActionResult<ImportSourcesResponse> Sources()
    {
        var offered = settings.Current.Import.Offered;

        // The suggestions come out of CapFrameX 1.x's settings, which only exist on Windows.
        var sources = OperatingSystem.IsWindows() ? Suggest() : [];

        return Ok(new ImportSourcesResponse(sources, offered));
    }

    /// <summary>Imports captures from a folder or a single file.</summary>
    /// <param name="request">Where to import from.</param>
    /// <param name="cancellationToken">Cancels the import.</param>
    [HttpPost]
    public async Task<ActionResult<ImportResultDto>> Import(
        [FromBody] ImportRequest request,
        CancellationToken cancellationToken)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Path))
        {
            return Problem(
                title: "The request could not be read.",
                detail: "An import needs a path to read from.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        var path = request.Path.Trim();

        if (!Path.IsPathRooted(path))
        {
            return Problem(
                title: "The request could not be read.",
                detail: $"'{path}' is not an absolute path.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        var result = System.IO.File.Exists(path)
            ? await importer.ImportAsync([path], cancellationToken)
            : await importer.ImportDirectoryAsync(path, request.Recursive, cancellationToken);

        // Whoever imported has answered the question, so the first-run dialog is done with.
        await settings.MarkImportOfferedAsync(cancellationToken);

        if (result.Total == 0 && result.Errors.Count > 0)
        {
            return Problem(
                title: "Nothing could be imported.",
                detail: string.Join(" ", result.Errors),
                statusCode: StatusCodes.Status400BadRequest);
        }

        return Ok(new ImportResultDto(
            result.Imported,
            result.AlreadyKnown,
            result.Failed,
            result.Total,
            result.Errors));
    }

    [SupportedOSPlatform("windows")]
    private List<ImportSourceDto> Suggest() =>
        [.. ImportSources
            .Suggest(exclude: paths.CaptureDirectory)
            .Select(source => new ImportSourceDto(source.Path, source.Origin, source.CaptureCount))];
}
