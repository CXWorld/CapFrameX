using CapFrameX.Service.Contracts.Bridge;
using CapFrameX.Service.Contracts.Records;
using CapFrameX.Service.Core.Bridge;
using CapFrameX.Service.Core.Platform;
using CapFrameX.Service.Data;
using CapFrameX.Service.Records;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CapFrameX.Service.Application.Records;

/// <summary>How a change to a record went.</summary>
public enum RecordChangeStatus
{
    /// <summary>It was applied.</summary>
    Ok,

    /// <summary>Nothing about the record differed from what was asked for.</summary>
    Unchanged,

    /// <summary>No record with that identity is indexed.</summary>
    NotIndexed,

    /// <summary>The record has no capture file behind it to change.</summary>
    NoSourceFile,

    /// <summary>The file is there but could not be read.</summary>
    Unreadable,

    /// <summary>The file could not be written, and is unchanged.</summary>
    NotWritable,

    /// <summary>The file could not be moved to the trash, and is still there.</summary>
    NotRemovable,
}

/// <summary>The outcome of one change.</summary>
/// <param name="Status">How it went.</param>
/// <param name="Error">What went wrong, phrased for the caller.</param>
public readonly record struct RecordChange(RecordChangeStatus Status, string? Error = null)
{
    /// <summary>Whether the record is now what the caller asked for.</summary>
    public bool IsSuccess => Status is RecordChangeStatus.Ok or RecordChangeStatus.Unchanged;
}

/// <summary>
/// Changes a record: its editable fields, or its existence.
/// </summary>
/// <remarks>
/// Both go through the capture file, because the file is the record. An edit is written into it so
/// the correction survives being copied to another machine and CapFrameX 1.x reads it too, and a
/// deletion moves it to the platform's trash rather than unlinking it - a record is hours of
/// benchmarking that cannot be recaptured. The index is brought up to date immediately afterwards
/// rather than left to the folder watcher, so the next request answers with what just happened.
/// </remarks>
/// <param name="context">The service database.</param>
/// <param name="reader">Reads the capture files.</param>
/// <param name="writer">Writes them back.</param>
/// <param name="index">Re-projects a record after its file changed.</param>
/// <param name="trash">Moves a capture to the platform's trash.</param>
/// <param name="events">Tells the frontend what changed.</param>
/// <param name="logger">Records what was changed and what refused to be.</param>
public sealed class RecordStore(
    CapFrameXDbContext context,
    RecordFileReader reader,
    RecordFileWriter writer,
    RecordIndex index,
    IFileTrash trash,
    IBridgeEventPublisher events,
    ILogger<RecordStore> logger)
{
    /// <summary>Applies a patch to one record and writes it into the capture file.</summary>
    /// <param name="id">Identity of the record.</param>
    /// <param name="edit">What to change; a field left out is left alone.</param>
    /// <param name="cancellationToken">Cancels the change.</param>
    public async Task<RecordChange> EditAsync(Guid id, RecordEdit edit, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(edit);

        var path = await SourceFileAsync(id, cancellationToken);

        if (path.Change is { } refused)
        {
            return refused;
        }

        // Read fresh rather than through the cache: the parsed capture there is shared with
        // whoever is looking at the record right now, and it would show them an edit that has not
        // been written yet - or, if the write fails, one that never will be.
        var read = await reader.ReadAsync(path.Path!, cancellationToken);

        if (read.Session is not { } session)
        {
            return new RecordChange(RecordChangeStatus.Unreadable, read.Error);
        }

        if (!RecordFileWriter.Apply(session, edit))
        {
            // Writing anyway would change the file's timestamp, wake the watcher and re-index a
            // record for nothing.
            return new RecordChange(RecordChangeStatus.Unchanged);
        }

        try
        {
            await writer.SaveAsync(path.Path!, session, cancellationToken);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(exception, "Editing '{Path}' failed.", path.Path);

            return new RecordChange(
                RecordChangeStatus.NotWritable,
                $"'{path.Path}' could not be written: {exception.Message}");
        }

        await index.RefreshAsync(id, cancellationToken);

        events.Publish(BridgeEventTypes.RecordsChanged, new RecordsChangedDto(0, 1, 0));
        logger.LogInformation("Record '{Id}' was edited.", id);

        return new RecordChange(RecordChangeStatus.Ok);
    }

    /// <summary>Moves one record's capture to the trash and drops it from the index.</summary>
    /// <param name="id">Identity of the record.</param>
    /// <param name="cancellationToken">Cancels the removal.</param>
    public async Task<RecordChange> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var record = await context.Sessions.FirstOrDefaultAsync(session => session.Id == id, cancellationToken);

        if (record is null)
        {
            return new RecordChange(RecordChangeStatus.NotIndexed, $"No record with id '{id}' is indexed.");
        }

        if (record.SourceFilePath is { Length: > 0 } path)
        {
            var result = await trash.MoveAsync(path, cancellationToken);

            if (!result.IsRemoved)
            {
                // The row stays: a record the list no longer shows while its file is still in the
                // folder would come back on the next scan anyway, under a new identity.
                logger.LogWarning("Trashing '{Path}' failed: {Error}", path, result.Error);

                return new RecordChange(RecordChangeStatus.NotRemovable, result.Error);
            }
        }

        context.Sessions.Remove(record);
        await context.SaveChangesAsync(cancellationToken);

        events.Publish(BridgeEventTypes.RecordsChanged, new RecordsChangedDto(0, 0, 1));
        logger.LogInformation("Record '{Id}' was moved to the trash.", id);

        return new RecordChange(RecordChangeStatus.Ok);
    }

    private async Task<(string? Path, RecordChange? Change)> SourceFileAsync(Guid id, CancellationToken cancellationToken)
    {
        var path = await context.Sessions
            .AsNoTracking()
            .Where(session => session.Id == id)
            .Select(session => session.SourceFilePath)
            .FirstOrDefaultAsync(cancellationToken);

        if (path is null && !await context.Sessions.AnyAsync(session => session.Id == id, cancellationToken))
        {
            return (null, new RecordChange(RecordChangeStatus.NotIndexed, $"No record with id '{id}' is indexed."));
        }

        if (string.IsNullOrEmpty(path))
        {
            return (null, new RecordChange(
                RecordChangeStatus.NoSourceFile,
                $"Record '{id}' was recorded by the service and has no capture file."));
        }

        return (path, null);
    }
}
