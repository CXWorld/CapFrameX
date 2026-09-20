using System.Text.Json;
using CapFrameX.Service.Contracts.Records;
using CapFrameX.Service.Data.Models;

namespace CapFrameX.Service.Application.Records;

/// <summary>
/// Turns an indexed row into what the frontend reads.
/// </summary>
/// <remarks>
/// In one place because the list and the detail show the same fields, and a record that read
/// differently depending on which endpoint answered would be a bug nobody could see coming.
/// </remarks>
public static class RecordProjection
{
    /// <summary>The fields the record list shows.</summary>
    /// <param name="record">The indexed row.</param>
    public static RecordSummaryDto Summary(Session record)
    {
        ArgumentNullException.ThrowIfNull(record);

        return new RecordSummaryDto(
            Id: record.Id,
            Name: Name(record),
            GameName: NullIfBlank(record.GameName),
            ProcessName: NullIfBlank(record.ProcessName),
            CreatedAt: new DateTimeOffset(DateTime.SpecifyKind(record.CreatedAt, DateTimeKind.Utc)),
            DurationSeconds: record.DurationSeconds ?? 0,
            RunCount: record.RunCount ?? 0,
            FrameCount: record.FrameCount ?? 0,
            Sparkline: Sparkline(record.SparklineJson),
            Processor: NullIfBlank(record.Processor),
            Gpu: NullIfBlank(record.Gpu),
            HasPcLatency: record.HasPcLatency,
            HasDisplayChange: record.HasDisplayChange,
            AverageFps: record.AverageFps,
            P1Fps: record.P1Fps,
            P99Fps: record.P99Fps);
    }

    /// <summary>Where the capture behind a record lives.</summary>
    /// <param name="record">The indexed row.</param>
    public static RecordSourceDto Source(Session record)
    {
        ArgumentNullException.ThrowIfNull(record);

        return new RecordSourceDto(
            FilePath: record.SourceFilePath ?? record.ImportedFrom,
            FileSize: record.SourceFileSize,
            ModifiedUtc: record.SourceModifiedUtc is { } modified
                ? new DateTimeOffset(DateTime.SpecifyKind(modified, DateTimeKind.Utc))
                : null,
            IndexVersion: record.IndexVersion);
    }

    /// <summary>
    /// What to call the record.
    /// </summary>
    /// <remarks>
    /// The file name, because that is what the user named it and what they will look for. An
    /// imported record keeps the name of the file it came from even though that file is no longer
    /// what it reads; a session the service recorded itself has neither, so it falls back to the
    /// game.
    /// </remarks>
    private static string Name(Session record)
    {
        var origin = record.SourceFilePath ?? record.ImportedFrom;
        var fileName = origin is null ? null : Path.GetFileNameWithoutExtension(origin);

        return NullIfBlank(fileName) ?? NullIfBlank(record.GameName) ?? record.Id.ToString();
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
            // Written by an older indexer, or by hand: the list is better without it than not at all.
            return [];
        }
    }

    private static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
}
