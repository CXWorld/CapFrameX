using CapFrameX.Service.Analysis;
using CapFrameX.Service.Contracts.Settings;
using CapFrameX.Statistics.NetStandard.Contracts;

namespace CapFrameX.Service.Application.Settings;

/// <summary>What a patch would make of the settings, or why it cannot.</summary>
/// <param name="Errors">Everything wrong with the patch; empty when it is good.</param>
/// <param name="Result">The settings the patch would produce, when it is good.</param>
public readonly record struct SettingsValidation(IReadOnlyList<string> Errors, PersistedSettings? Result)
{
    /// <summary>Whether the patch can be applied.</summary>
    public bool IsValid => Errors.Count == 0 && Result is not null;
}

/// <summary>
/// Checks a settings patch and works out what it would produce.
/// </summary>
/// <remarks>
/// Every value here reaches code that has no defence of its own: rounding digits outside 0 to 15
/// make <c>Math.Round</c> throw, which the statistics provider catches and turns into a metric that
/// is silently absent, and a stuttering factor of one or less makes every ordinary frame a stutter.
/// The whole patch is checked before any of it is kept, so a request with one bad value changes
/// nothing rather than half of what it asked for - and it reports every fault at once rather than
/// making the user find them one request at a time.
/// </remarks>
public static class SettingsValidator
{
    /// <summary>Largest number of decimal places <c>Math.Round</c> accepts.</summary>
    public const int MaximumRoundingDigits = 15;

    /// <summary>Works out what a patch would produce.</summary>
    /// <param name="current">The settings as they are now.</param>
    /// <param name="patch">What to change.</param>
    public static SettingsValidation Validate(PersistedSettings current, AppSettingsPatch patch)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(patch);

        var errors = new List<string>();
        var result = Copy(current);

        if (patch.Analysis is { } analysis)
        {
            Analysis(analysis, result, errors);
        }

        if (patch.Paths?.CaptureDirectory is { } directory)
        {
            Directory(directory, result, errors);
        }

        if (patch.Appearance?.Theme is { } theme)
        {
            if (!AppearanceThemes.All.Contains(theme, StringComparer.OrdinalIgnoreCase))
            {
                errors.Add($"'{theme}' is not a theme. Known themes: {string.Join(", ", AppearanceThemes.All)}.");
            }
            else
            {
                result.Theme = theme.ToLowerInvariant();
            }
        }

        return new SettingsValidation(errors, errors.Count == 0 ? result : null);
    }

    /// <summary>A copy, so a rejected patch leaves the live settings untouched.</summary>
    /// <param name="settings">The settings to copy.</param>
    public static PersistedSettings Copy(PersistedSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        return new PersistedSettings
        {
            StutteringFactor = settings.StutteringFactor,
            StutteringThreshold = settings.StutteringThreshold,
            MovingAverageWindowSize = settings.MovingAverageWindowSize,
            IntervalAverageWindowTime = settings.IntervalAverageWindowTime,
            FpsValuesRoundingDigits = settings.FpsValuesRoundingDigits,
            OutlierMethod = settings.OutlierMethod,
            Metrics = [.. settings.Metrics],
            LShapeMetric = settings.LShapeMetric,
            CaptureDirectory = settings.CaptureDirectory,
            Theme = settings.Theme,
        };
    }

    private static void Analysis(AnalysisSettingsPatch patch, PersistedSettings result, List<string> errors)
    {
        if (patch.StutteringFactor is { } factor)
        {
            // At one or less every frame at or above its local average is a stutter, which would
            // report a perfectly even capture as stuttering the whole way through.
            if (factor <= 1 || factor > 100 || double.IsNaN(factor))
            {
                errors.Add("stutteringFactor has to be greater than 1 and at most 100.");
            }
            else
            {
                result.StutteringFactor = factor;
            }
        }

        if (patch.StutteringThreshold is { } threshold)
        {
            if (threshold <= 0 || threshold > 1000 || double.IsNaN(threshold))
            {
                errors.Add("stutteringThreshold has to be greater than 0 and at most 1000 fps.");
            }
            else
            {
                result.StutteringThreshold = threshold;
            }
        }

        if (patch.MovingAverageWindowSize is { } window)
        {
            if (window < 1 || window > 100_000)
            {
                errors.Add("movingAverageWindowSize has to be between 1 and 100000 samples.");
            }
            else
            {
                result.MovingAverageWindowSize = window;
            }
        }

        if (patch.IntervalAverageWindowTime is { } interval)
        {
            if (interval < 1 || interval > 60_000)
            {
                errors.Add("intervalAverageWindowTime has to be between 1 and 60000 milliseconds.");
            }
            else
            {
                result.IntervalAverageWindowTime = interval;
            }
        }

        if (patch.FpsValuesRoundingDigits is { } digits)
        {
            if (digits < 0 || digits > MaximumRoundingDigits)
            {
                errors.Add($"fpsValuesRoundingDigits has to be between 0 and {MaximumRoundingDigits}.");
            }
            else
            {
                result.FpsValuesRoundingDigits = digits;
            }
        }

        if (patch.OutlierMethod is { } outliers)
        {
            if (!OutlierMethods.TryParse(outliers, out var method))
            {
                errors.Add($"'{outliers}' is not an outlier method. Known methods: {string.Join(", ", OutlierMethods.Names)}.");
            }
            else
            {
                result.OutlierMethod = method.ToString();
            }
        }

        if (patch.Metrics is { } metrics)
        {
            Metrics(metrics, result, errors);
        }

        if (patch.LShapeMetric is { } lShape)
        {
            if (!Enum.TryParse<ELShapeMetrics>(lShape, ignoreCase: true, out var parsed) || !Enum.IsDefined(parsed))
            {
                errors.Add($"'{lShape}' is not an L-shape metric. Known metrics: frametimes, fps.");
            }
            else
            {
                result.LShapeMetric = parsed.ToString();
            }
        }
    }

    private static void Metrics(IReadOnlyList<string> metrics, PersistedSettings result, List<string> errors)
    {
        if (metrics.Count == 0)
        {
            // An empty tile row is a broken view rather than a choice; clearing the setting back to
            // the default is what the user means by "none of my own".
            result.Metrics = [];

            return;
        }

        var resolved = new List<string>(metrics.Count);

        foreach (var metric in metrics)
        {
            if (!MetricCatalog.TryParse(metric, out var parsed))
            {
                errors.Add($"'{metric}' is not a metric. Known metrics: {string.Join(", ", MetricCatalog.Supported.Select(MetricCatalog.Key))}.");

                return;
            }

            resolved.Add(MetricCatalog.Key(parsed));
        }

        result.Metrics = resolved;
    }

    private static void Directory(string directory, PersistedSettings result, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(directory))
        {
            // Clearing it means "wherever this platform puts captures", which is what a fresh
            // installation does.
            result.CaptureDirectory = null;

            return;
        }

        if (!Path.IsPathRooted(directory))
        {
            errors.Add($"'{directory}' is not an absolute path.");

            return;
        }

        try
        {
            // Creating it is the point: the user is saying where captures will live, and a folder
            // that cannot be made is a setting that would fail silently every scan.
            System.IO.Directory.CreateDirectory(directory);
            result.CaptureDirectory = Path.GetFullPath(directory);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            errors.Add($"'{directory}' cannot be used: {exception.Message}");
        }
    }
}
