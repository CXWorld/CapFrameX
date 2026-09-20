using System.Text.Json;
using CapFrameX.Data.Session.Contracts;
using CapFrameX.Service.Analysis;
using CapFrameX.Service.Contracts.Analysis;
using CapFrameX.Service.Contracts.Bridge;
using CapFrameX.Service.Contracts.Records;
using CapFrameX.Service.Core.Bridge;
using CapFrameX.Service.Data;
using CapFrameX.Service.Data.Models;
using CapFrameX.Service.Records;
using CapFrameX.Statistics.NetStandard.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CapFrameX.Service.Application.Records;

/// <summary>What one import did.</summary>
/// <param name="Imported">Captures that entered the database.</param>
/// <param name="AlreadyKnown">Captures the database already held.</param>
/// <param name="Failed">Files that could not be read.</param>
/// <param name="Errors">Why each of those failed, for the user to act on.</param>
public sealed record RecordImportResult(
    int Imported,
    int AlreadyKnown,
    int Failed,
    IReadOnlyList<string> Errors)
{
    /// <summary>Files the import looked at.</summary>
    public int Total => Imported + AlreadyKnown + Failed;
}

/// <summary>
/// Reads capture files into the database.
/// </summary>
/// <remarks>
/// An imported record keeps nothing but its origin on disk: the frames, the sensor readings and
/// everything else the file held are copied in, so the record survives the file being moved,
/// deleted or left on a drive that is not attached. That is what makes an import different from
/// the folder scan, which only points at files and depends on them staying put.
/// </remarks>
/// <param name="context">The service database.</param>
/// <param name="reader">Reads the capture files.</param>
/// <param name="analysis">Computes the metrics the record list shows.</param>
/// <param name="options">Supplies the sparkline budget.</param>
/// <param name="events">Tells the frontend that records arrived.</param>
/// <param name="logger">Records what was imported and what refused to be.</param>
public sealed class RecordImporter(
    CapFrameXDbContext context,
    RecordFileReader reader,
    AnalysisService analysis,
    RecordIndexOptions options,
    IBridgeEventPublisher events,
    ILogger<RecordImporter> logger)
{
    /// <summary>Name of the suite imported captures land in.</summary>
    public const string SuiteName = "Imported";

    /// <summary>Identity of that suite, fixed so a second import does not make a second one.</summary>
    public static readonly Guid SuiteId = new("b6f0f5c0-6a58-4a2f-9c3d-0a7f1c2e4d11");

    private static readonly AnalysisRequest ListMetrics = new()
    {
        Metrics = [EMetric.Average, EMetric.P1, EMetric.P99],
    };

    /// <summary>Imports every capture in a folder.</summary>
    /// <param name="directory">The folder to read.</param>
    /// <param name="recursive">Whether to look in its subfolders too.</param>
    /// <param name="cancellationToken">Cancels the import.</param>
    public async Task<RecordImportResult> ImportDirectoryAsync(
        string directory,
        bool recursive = true,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);

        if (!Directory.Exists(directory))
        {
            return new RecordImportResult(0, 0, 0, [$"'{directory}' does not exist."]);
        }

        var files = Directory.EnumerateFiles(
            directory,
            "*" + RecordFileReader.Extension,
            recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly);

        return await ImportAsync(files, cancellationToken);
    }

    /// <summary>Imports the named capture files.</summary>
    /// <param name="paths">The files to read.</param>
    /// <param name="cancellationToken">Cancels the import.</param>
    public async Task<RecordImportResult> ImportAsync(
        IEnumerable<string> paths,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(paths);

        var imported = 0;
        var known = 0;
        var failed = 0;
        var errors = new List<string>();
        var suite = await SuiteAsync(cancellationToken);

        foreach (var path in paths)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var outcome = await ImportOneAsync(path, suite, cancellationToken);

            switch (outcome.Status)
            {
                case ImportStatus.Imported:
                    imported++;
                    break;
                case ImportStatus.AlreadyKnown:
                    known++;
                    break;
                default:
                    failed++;

                    // Capped: a folder of a thousand unreadable files should not answer with a
                    // thousand lines, and the first few say what is wrong with them.
                    if (errors.Count < 20 && outcome.Error is not null)
                    {
                        errors.Add(outcome.Error);
                    }

                    break;
            }
        }

        if (imported > 0)
        {
            await context.SaveChangesAsync(cancellationToken);
            events.Publish(BridgeEventTypes.RecordsChanged, new RecordsChangedDto(imported, 0, 0));
        }

        logger.LogInformation(
            "Import: {Imported} imported, {Known} already known, {Failed} unreadable.",
            imported,
            known,
            failed);

        return new RecordImportResult(imported, known, failed, errors);
    }

    private async Task<(ImportStatus Status, string? Error)> ImportOneAsync(
        string path,
        Suite suite,
        CancellationToken cancellationToken)
    {
        string content;

        try
        {
            content = await File.ReadAllTextAsync(path, cancellationToken);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return (ImportStatus.Failed, $"'{path}' could not be read: {exception.Message}");
        }

        var read = reader.Parse(content, path);

        if (read.Session is not { } session)
        {
            return (ImportStatus.Failed, read.Error);
        }

        var hash = string.IsNullOrWhiteSpace(session.Hash) ? null : session.Hash;

        if (hash is not null && await KnownAsync(hash, cancellationToken))
        {
            // The same capture, whether it arrived through a scan, a previous import, or the same
            // file under a different name.
            return (ImportStatus.AlreadyKnown, null);
        }

        var record = new Session
        {
            Id = Guid.NewGuid(),
            SuiteId = suite.Id,
            Hash = hash,
            ImportedFrom = path,
        };

        Describe(record, session, path);
        AddRuns(record, session, RecordFileParts.Read(content));

        context.Sessions.Add(record);

        return (ImportStatus.Imported, null);
    }

    /// <summary>
    /// Checks the database and what this import has already staged.
    /// </summary>
    /// <remarks>
    /// The local lookup matters: a folder often holds the same capture twice, and rows added in
    /// this import are not in the database until it ends.
    /// </remarks>
    private async Task<bool> KnownAsync(string hash, CancellationToken cancellationToken) =>
        context.Sessions.Local.Any(session => session.Hash == hash) ||
        await context.Sessions.AnyAsync(session => session.Hash == hash, cancellationToken);

    private void Describe(Session record, ISession session, string path)
    {
        var summary = RecordSummaryFactory.Create(session, path, options.SparklinePoints);
        var metrics = analysis.Analyze(session, ListMetrics).Metrics;

        record.GameName = summary.GameName ?? string.Empty;
        record.ProcessName = summary.ProcessName ?? string.Empty;
        record.Processor = summary.Processor ?? string.Empty;
        record.Gpu = summary.Gpu ?? string.Empty;
        record.CreatedAt = summary.CreatedAt.UtcDateTime;
        record.UpdatedAt = DateTime.UtcNow;

        record.Comment = session.Info?.Comment;
        record.Motherboard = session.Info?.Motherboard;
        record.SystemRam = session.Info?.SystemRam;
        record.Os = session.Info?.OS ?? string.Empty;
        record.ApiInfo = session.Info?.ApiInfo;
        record.GpuDriverVersion = session.Info?.GPUDriverVersion;
        record.BaseDriverVersion = session.Info?.BaseDriverVersion;
        record.DriverPackage = session.Info?.DriverPackage;
        record.PresentationMode = session.Info?.PresentationMode;
        record.ResolutionInfo = session.Info?.ResolutionInfo;

        record.IndexVersion = RecordIndexPlanner.CurrentIndexVersion;
        record.DurationSeconds = summary.DurationSeconds;
        record.RunCount = summary.RunCount;
        record.FrameCount = summary.FrameCount;
        record.SparklineJson = JsonSerializer.Serialize(summary.Sparkline);
        record.HasPcLatency = summary.HasPcLatency;
        record.HasDisplayChange = summary.HasDisplayChange;

        record.AverageFps = Metric(metrics, EMetric.Average);
        record.P1Fps = Metric(metrics, EMetric.P1);
        record.P99Fps = Metric(metrics, EMetric.P99);
    }

    private static void AddRuns(Session record, ISession session, IReadOnlyList<RecordRunParts> parts)
    {
        var runs = session.Runs ?? [];

        for (var index = 0; index < runs.Count; index++)
        {
            var run = runs[index];
            var payload = index < parts.Count ? parts[index] : null;

            record.Runs.Add(new SessionRun
            {
                Id = Guid.NewGuid(),
                SessionId = record.Id,
                RunIndex = index,
                Hash = run?.Hash,
                CreatedAt = record.CreatedAt,
                PresentMonRuntime = run?.PresentMonRuntime,
                SampleTime = run?.SampleTime ?? 0,
                CaptureDataJson = payload?.CaptureData,
                SensorDataJson = payload?.SensorData,
                RtssFrameTimesJson = payload?.RtssFrameTimes,
                PmdGpuPowerJson = payload?.PmdGpuPower,
                PmdCpuPowerJson = payload?.PmdCpuPower,
                PmdSystemPowerJson = payload?.PmdSystemPower,
            });
        }
    }

    private static double? Metric(IReadOnlyList<MetricDto> metrics, EMetric metric) =>
        metrics.FirstOrDefault(value => value.Key == MetricCatalog.Key(metric))?.Value;

    private async Task<Suite> SuiteAsync(CancellationToken cancellationToken)
    {
        var known = await context.Suites.FindAsync([SuiteId], cancellationToken);

        if (known is not null)
        {
            return known;
        }

        var now = DateTime.UtcNow;
        var suite = new Suite
        {
            Id = SuiteId,
            Name = SuiteName,
            Description = "Captures imported from files.",
            Type = SuiteType.Miscellaneous,
            CreatedAt = now,
            UpdatedAt = now,
        };

        context.Suites.Add(suite);

        return suite;
    }

    private enum ImportStatus
    {
        Imported,
        AlreadyKnown,
        Failed,
    }
}
