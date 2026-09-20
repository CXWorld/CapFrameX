using CapFrameX.Service.Application.Settings;
using CapFrameX.Service.Contracts.Settings;
using Microsoft.AspNetCore.Mvc;

namespace CapFrameX.Service.Api.Controllers;

/// <summary>
/// What the user has set.
/// </summary>
/// <remarks>
/// A change takes effect at once rather than on the next start: the analysis reads these options on
/// every request, and the indexer watches the folder they name. Whoever made the change gets the
/// new settings back, and everyone else hears about it as <c>settings.changed</c> on the event
/// stream.
/// </remarks>
/// <param name="settings">The live settings.</param>
[ApiController]
[Route("api/settings")]
public sealed class SettingsController(SettingsStore settings) : ControllerBase
{
    /// <summary>Returns the settings as they are.</summary>
    [HttpGet]
    public ActionResult<AppSettingsDto> Get() => Ok(settings.Current);

    /// <summary>Changes the settings.</summary>
    /// <remarks>
    /// A section left out is left alone, and so is a field inside one. The whole patch is checked
    /// before any of it is applied, so one bad value changes nothing rather than half of what was
    /// asked for, and the answer names every fault at once.
    /// </remarks>
    /// <param name="patch">What to change.</param>
    /// <param name="cancellationToken">Cancels the change.</param>
    [HttpPatch]
    public async Task<ActionResult<AppSettingsDto>> Patch(
        [FromBody] AppSettingsPatch patch,
        CancellationToken cancellationToken)
    {
        if (patch is null)
        {
            return Problem(
                title: "The request could not be read.",
                detail: "A patch needs a body naming the settings to change.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        var change = await settings.ApplyAsync(patch, cancellationToken);

        if (!change.IsValid)
        {
            return Problem(
                title: "The settings were not changed.",
                detail: string.Join(" ", change.Errors),
                statusCode: StatusCodes.Status400BadRequest);
        }

        return Ok(change.Settings);
    }
}
