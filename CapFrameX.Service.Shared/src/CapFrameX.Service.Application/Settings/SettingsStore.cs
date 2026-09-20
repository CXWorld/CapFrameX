using System.Text;
using System.Text.Json;
using CapFrameX.Service.Analysis;
using CapFrameX.Service.Application.Records;
using CapFrameX.Service.Contracts.Bridge;
using CapFrameX.Service.Contracts.Settings;
using CapFrameX.Service.Core.Bridge;
using CapFrameX.Service.Core.Platform;
using CapFrameX.Service.Data;
using CapFrameX.Statistics.NetStandard.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CapFrameX.Service.Application.Settings;

/// <summary>The outcome of a settings change.</summary>
/// <param name="Errors">Everything wrong with the patch; empty when it was applied.</param>
/// <param name="Settings">The settings as they now are.</param>
public readonly record struct SettingsChange(IReadOnlyList<string> Errors, AppSettingsDto Settings)
{
    /// <summary>Whether the patch was applied.</summary>
    public bool IsValid => Errors.Count == 0;
}

/// <summary>
/// The user's settings: what they are, and what changing them does.
/// </summary>
/// <remarks>
/// A singleton, because the settings are the live ones. The analysis reads
/// <see cref="AnalysisSettings"/> on every call and the indexer watches
/// <see cref="RecordIndexOptions"/>, so changing a setting here takes effect at once rather than on
/// the next start - which is what the settings view promises by recomputing the open analysis.
/// </remarks>
/// <param name="paths">Where the settings file lives, and where captures live by default.</param>
/// <param name="analysis">The live analysis options, updated in place.</param>
/// <param name="index">The live index options, updated in place.</param>
/// <param name="scopes">Provides a scope for re-projecting records after a change.</param>
/// <param name="events">Tells the frontend that the settings changed.</param>
/// <param name="logger">Records what was changed and what could not be read.</param>
public sealed class SettingsStore(
    IAppPaths paths,
    AnalysisSettings analysis,
    RecordIndexOptions index,
    IServiceScopeFactory scopes,
    IBridgeEventPublisher events,
    ILogger<SettingsStore> logger)
{
    /// <summary>Name of the settings file inside the configuration directory.</summary>
    /// <remarks>
    /// Beside CapFrameX 1.x's <c>AppSettings.json</c> rather than inside it. Two applications
    /// rewriting one file would each drop what the other had added, and these are a different set
    /// of settings in any case.
    /// </remarks>
    public const string FileName = "ServiceSettings.json";

    private static readonly JsonSerializerOptions Format = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    private readonly Lock _gate = new();

    private PersistedSettings _settings = new();
    private bool _loaded;

    /// <summary>Where the settings file lives.</summary>
    public string Path => System.IO.Path.Combine(paths.ConfigurationDirectory, FileName);

    /// <summary>The settings as they are.</summary>
    public AppSettingsDto Current
    {
        get
        {
            lock (_gate)
            {
                EnsureLoaded();

                return Describe(_settings);
            }
        }
    }

    /// <summary>
    /// Reads the settings file and puts what it says into effect.
    /// </summary>
    /// <remarks>
    /// Called once at start-up, before anything serves a request, so the first analysis already
    /// uses the user's options rather than the defaults.
    /// </remarks>
    public void Load()
    {
        lock (_gate)
        {
            EnsureLoaded();
            ApplyToRuntime(_settings);
        }
    }

    /// <summary>Applies a patch, writes it to the file and announces it.</summary>
    /// <param name="patch">What to change; a field left out is left alone.</param>
    /// <param name="cancellationToken">Cancels the change.</param>
    public async Task<SettingsChange> ApplyAsync(AppSettingsPatch patch, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(patch);

        PersistedSettings updated;
        bool reproject;

        lock (_gate)
        {
            EnsureLoaded();

            var validation = SettingsValidator.Validate(_settings, patch);

            if (!validation.IsValid)
            {
                return new SettingsChange(validation.Errors, Describe(_settings));
            }

            updated = validation.Result!;

            // Only the rounding digits change numbers the index has already stored; the rest are
            // read per request.
            reproject = updated.FpsValuesRoundingDigits != _settings.FpsValuesRoundingDigits;
            _settings = updated;
            ApplyToRuntime(updated);
        }

        await SaveAsync(updated, cancellationToken);

        if (reproject)
        {
            await ReprojectAsync(cancellationToken);
        }

        var described = Describe(updated);
        events.Publish(BridgeEventTypes.SettingsChanged, described);
        logger.LogInformation("Settings changed.");

        return new SettingsChange([], described);
    }

    private void EnsureLoaded()
    {
        if (_loaded)
        {
            return;
        }

        _loaded = true;

        try
        {
            if (File.Exists(Path))
            {
                _settings = JsonSerializer.Deserialize<PersistedSettings>(File.ReadAllText(Path), Format) ?? new();
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            // Starting with the defaults beats refusing to start. The file is left alone until the
            // user changes something, so whatever is in it can still be recovered by hand.
            logger.LogWarning(exception, "'{Path}' could not be read; the defaults are in use.", Path);
            _settings = new PersistedSettings();
        }
    }

    /// <summary>
    /// Puts the settings into the objects that are actually read while the service runs.
    /// </summary>
    /// <remarks>
    /// In place rather than by replacing them: the statistics provider and the indexer hold these
    /// by reference, and handing out a new instance would leave them on the old one.
    /// </remarks>
    private void ApplyToRuntime(PersistedSettings settings)
    {
        analysis.StutteringFactor = settings.StutteringFactor;
        analysis.StutteringThreshold = settings.StutteringThreshold;
        analysis.MovingAverageWindowSize = settings.MovingAverageWindowSize;
        analysis.IntervalAverageWindowTime = settings.IntervalAverageWindowTime;
        analysis.FpsValuesRoundingDigits = settings.FpsValuesRoundingDigits;

        index.CaptureDirectory = settings.CaptureDirectory ?? paths.CaptureDirectory;
    }

    private async Task SaveAsync(PersistedSettings settings, CancellationToken cancellationToken)
    {
        var temporary = Path + ".tmp";

        try
        {
            Directory.CreateDirectory(paths.ConfigurationDirectory);

            // Written beside the file and moved into place, so an interrupted write cannot leave
            // the user without their settings.
            await File.WriteAllTextAsync(
                temporary,
                JsonSerializer.Serialize(settings, Format),
                new UTF8Encoding(false),
                cancellationToken);

            File.Move(temporary, Path, overwrite: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // The change is in effect either way; it just will not survive a restart, and saying so
            // is better than undoing what the user just asked for.
            logger.LogWarning(exception, "Settings could not be written to '{Path}'.", Path);
        }
    }

    /// <summary>
    /// Marks every indexed record for re-reading.
    /// </summary>
    /// <remarks>
    /// The list shows numbers the index computed once, and the rounding digits that produced them
    /// have just changed. Without this the list and the record it opens would disagree in the last
    /// decimal place until each capture happened to be touched again.
    /// </remarks>
    private async Task ReprojectAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            var context = scope.ServiceProvider.GetRequiredService<CapFrameXDbContext>();

            await context.Sessions
                .Where(session => session.SourceFilePath != null)
                .ExecuteUpdateAsync(session => session.SetProperty(record => record.IndexVersion, 0), cancellationToken);

            index.Invalidate();
        }
        catch (Exception exception) when (exception is DbUpdateException or InvalidOperationException)
        {
            logger.LogWarning(exception, "Records could not be marked for re-reading.");
        }
    }

    private AppSettingsDto Describe(PersistedSettings settings) =>
        new(
            Analysis: new AnalysisSettingsDto(
                StutteringFactor: settings.StutteringFactor,
                StutteringThreshold: settings.StutteringThreshold,
                MovingAverageWindowSize: settings.MovingAverageWindowSize,
                IntervalAverageWindowTime: settings.IntervalAverageWindowTime,
                FpsValuesRoundingDigits: settings.FpsValuesRoundingDigits,
                OutlierMethod: settings.OutlierMethod,
                Metrics: settings.Metrics.Count > 0
                    ? [.. settings.Metrics]
                    : [.. MetricCatalog.Default.Select(MetricCatalog.Key)],
                LShapeMetric: settings.LShapeMetric),
            Paths: new PathSettingsDto(settings.CaptureDirectory ?? paths.CaptureDirectory),
            Appearance: new AppearanceSettingsDto(settings.Theme));

    /// <summary>The analysis request the settings describe, for a caller that named nothing.</summary>
    /// <remarks>
    /// So the tile row and the L-shape a user configured are what they get when they open a record,
    /// without the frontend having to repeat the settings on every request.
    /// </remarks>
    public AnalysisRequest DefaultAnalysisRequest()
    {
        var current = Current.Analysis;

        OutlierMethods.TryParse(current.OutlierMethod, out var outliers);
        Enum.TryParse<ELShapeMetrics>(current.LShapeMetric, ignoreCase: true, out var lShape);

        var metrics = new List<EMetric>(current.Metrics.Count);

        foreach (var key in current.Metrics)
        {
            if (MetricCatalog.TryParse(key, out var metric))
            {
                metrics.Add(metric);
            }
        }

        return new AnalysisRequest
        {
            OutlierMethod = outliers,
            Metrics = metrics.Count > 0 ? metrics : null,
            LShapeMetric = Enum.IsDefined(lShape) ? lShape : ELShapeMetrics.Frametimes,
        };
    }
}
