using CapFrameX.Service.Analysis;
using CapFrameX.Service.Contracts.Analysis;
using CapFrameX.Statistics.NetStandard.Contracts;

namespace CapFrameX.Service.Api.Controllers;

/// <summary>
/// Turns the query string of an analysis call into a request, or into the reason it cannot be one.
/// </summary>
/// <remarks>
/// Everything a caller names has to come back or be refused. A metric quietly dropped would shrink
/// the tile row without saying why, and a curve quietly dropped would leave a gap in the chart that
/// looks like missing data.
/// </remarks>
internal static class AnalysisQuery
{
    /// <summary>
    /// The outlier methods that actually remove anything.
    /// </summary>
    /// <remarks>
    /// <see cref="ERemoveOutlierMethod"/> declares three more - interquartile range, three sigma,
    /// two and a half sigma - which the 1.x provider accepts and then returns the sequence
    /// unchanged for. Offering them here would let the UI show a setting that does nothing.
    /// </remarks>
    public static IReadOnlyList<ERemoveOutlierMethod> SupportedOutlierMethods { get; } =
        [ERemoveOutlierMethod.None, ERemoveOutlierMethod.DeciPercentile];

    /// <summary>Reads the metric list.</summary>
    /// <param name="metrics">Comma-separated metric keys, or nothing for the default tiles.</param>
    /// <param name="parsed">What they name.</param>
    /// <param name="error">Why they could not be read.</param>
    public static bool TryMetrics(string? metrics, out IReadOnlyList<EMetric>? parsed, out string? error)
    {
        parsed = null;
        error = null;

        var keys = Split(metrics);

        if (keys.Length == 0)
        {
            return true;
        }

        var resolved = new List<EMetric>(keys.Length);

        foreach (var key in keys)
        {
            if (!MetricCatalog.TryParse(key, out var metric))
            {
                error = $"'{key}' is not a metric. Known metrics: {Join(MetricCatalog.Supported.Select(MetricCatalog.Key))}.";

                return false;
            }

            resolved.Add(metric);
        }

        parsed = resolved;

        return true;
    }

    /// <summary>Reads the outlier method.</summary>
    /// <param name="outliers">Its name, or nothing for none.</param>
    /// <param name="parsed">What it names.</param>
    /// <param name="error">Why it could not be read.</param>
    public static bool TryOutliers(string? outliers, out ERemoveOutlierMethod parsed, out string? error)
    {
        parsed = ERemoveOutlierMethod.None;
        error = null;

        if (string.IsNullOrWhiteSpace(outliers))
        {
            return true;
        }

        foreach (var method in SupportedOutlierMethods)
        {
            if (string.Equals(method.ToString(), outliers.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                parsed = method;

                return true;
            }
        }

        error = $"'{outliers}' is not an outlier method. Known methods: {Join(SupportedOutlierMethods.Select(m => m.ToString()))}.";

        return false;
    }

    /// <summary>Reads which curve the L-shape is drawn over.</summary>
    /// <param name="lShape">Its name, or nothing for frame times.</param>
    /// <param name="parsed">What it names.</param>
    /// <param name="error">Why it could not be read.</param>
    public static bool TryLShape(string? lShape, out ELShapeMetrics parsed, out string? error)
    {
        parsed = ELShapeMetrics.Frametimes;
        error = null;

        if (string.IsNullOrWhiteSpace(lShape))
        {
            return true;
        }

        if (Enum.TryParse(lShape.Trim(), ignoreCase: true, out parsed) && Enum.IsDefined(parsed))
        {
            return true;
        }

        parsed = ELShapeMetrics.Frametimes;
        error = $"'{lShape}' is not an L-shape metric. Known metrics: frametimes, fps.";

        return false;
    }

    /// <summary>Reads the list of curves.</summary>
    /// <param name="kinds">Comma-separated curve names, or nothing for the default.</param>
    /// <param name="parsed">What they name.</param>
    /// <param name="error">Why they could not be read.</param>
    public static bool TryKinds(string? kinds, out IReadOnlyList<string>? parsed, out string? error)
    {
        parsed = null;
        error = null;

        var names = Split(kinds);

        if (names.Length == 0)
        {
            return true;
        }

        foreach (var name in names)
        {
            if (!SeriesKinds.All.Contains(name, StringComparer.OrdinalIgnoreCase))
            {
                error = $"'{name}' is not a series. Known series: {Join(SeriesKinds.All)}.";

                return false;
            }
        }

        parsed = names;

        return true;
    }

    private static string[] Split(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? []
            : value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static string Join(IEnumerable<string> values) => string.Join(", ", values);
}
