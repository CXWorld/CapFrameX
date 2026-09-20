using CapFrameX.Extensions.NetStandard;
using CapFrameX.Service.Contracts.Analysis;
using CapFrameX.Statistics.NetStandard.Contracts;

namespace CapFrameX.Service.Analysis;

/// <summary>Which sequence a metric has to be computed from.</summary>
public enum MetricSource
{
    /// <summary>The time between presents.</summary>
    Frametimes,

    /// <summary>The time the GPU was busy, which not every capture records.</summary>
    GpuActive,
}

/// <summary>
/// The metrics this service can produce, and what a caller calls them.
/// </summary>
/// <remarks>
/// Only the ones <see cref="IStatisticProvider.GetFpsMetricValue"/> actually computes are listed.
/// The ones it answers with <c>NaN</c> - frames per watt, which needs power data and a coefficient -
/// would otherwise appear in the catalogue and then never produce a number.
/// </remarks>
public static class MetricCatalog
{
    /// <summary>Unit every metric in this catalogue is measured in.</summary>
    public const string Unit = "fps";

    private static readonly IReadOnlyDictionary<EMetric, MetricSource> Sources =
        new Dictionary<EMetric, MetricSource>
        {
            [EMetric.Max] = MetricSource.Frametimes,
            [EMetric.P99] = MetricSource.Frametimes,
            [EMetric.P95] = MetricSource.Frametimes,
            [EMetric.Average] = MetricSource.Frametimes,
            [EMetric.Median] = MetricSource.Frametimes,
            [EMetric.P5] = MetricSource.Frametimes,
            [EMetric.P1] = MetricSource.Frametimes,
            [EMetric.P0dot2] = MetricSource.Frametimes,
            [EMetric.P0dot1] = MetricSource.Frametimes,
            [EMetric.OnePercentLowAverage] = MetricSource.Frametimes,
            [EMetric.ZerodotTwoPercentLowAverage] = MetricSource.Frametimes,
            [EMetric.ZerodotOnePercentLowAverage] = MetricSource.Frametimes,
            [EMetric.OnePercentLowIntegral] = MetricSource.Frametimes,
            [EMetric.ZerodotTwoPercentLowIntegral] = MetricSource.Frametimes,
            [EMetric.ZerodotOnePercentLowIntegral] = MetricSource.Frametimes,
            [EMetric.Min] = MetricSource.Frametimes,
            [EMetric.AdaptiveStd] = MetricSource.Frametimes,

            // These three share their branch with Average, P1 and the 1% low average, so feeding
            // them frametimes would quietly return the plain metric under a GPU label.
            [EMetric.GpuActiveAverage] = MetricSource.GpuActive,
            [EMetric.GpuActiveP1] = MetricSource.GpuActive,
            [EMetric.GpuActiveOnePercentLowAverage] = MetricSource.GpuActive,
        };

    private static readonly IReadOnlyDictionary<string, EMetric> ByKey =
        Sources.Keys.ToDictionary(Key, metric => metric, StringComparer.OrdinalIgnoreCase);

    /// <summary>Every metric this service can compute.</summary>
    public static IReadOnlyList<EMetric> Supported { get; } = [.. Sources.Keys];

    /// <summary>What the analysis returns when the caller names no metrics.</summary>
    /// <remarks>The four tiles the analysis view opens with.</remarks>
    public static IReadOnlyList<EMetric> Default { get; } =
    [
        EMetric.Average,
        EMetric.P95,
        EMetric.OnePercentLowAverage,
        EMetric.ZerodotOnePercentLowAverage,
    ];

    /// <summary>The stable identifier of a metric, which a saved tile layout refers to.</summary>
    /// <param name="metric">The metric.</param>
    public static string Key(EMetric metric)
    {
        var name = metric.ToString();

        return char.ToLowerInvariant(name[0]) + name[1..];
    }

    /// <summary>Which sequence a metric has to be computed from.</summary>
    /// <param name="metric">The metric.</param>
    public static MetricSource Source(EMetric metric) =>
        Sources.TryGetValue(metric, out var source)
            ? source
            : throw new ArgumentOutOfRangeException(nameof(metric), metric, "Not a metric this service computes.");

    /// <summary>Resolves what a caller asked for.</summary>
    /// <param name="key">The identifier, in any casing.</param>
    /// <param name="metric">The metric it names.</param>
    public static bool TryParse(string? key, out EMetric metric)
    {
        if (!string.IsNullOrWhiteSpace(key) && ByKey.TryGetValue(key.Trim(), out metric))
        {
            return true;
        }

        metric = EMetric.None;

        return false;
    }

    /// <summary>Puts a computed value into the shape the frontend binds to.</summary>
    /// <param name="metric">The metric.</param>
    /// <param name="value">Its value, or <c>null</c> where the capture does not allow one.</param>
    public static MetricDto Describe(EMetric metric, double? value) =>
        new(Key(metric), metric.GetShortDescription(), Clean(value), Unit);

    /// <summary>
    /// A metric the provider could not compute comes back as <c>NaN</c>, which JSON cannot carry
    /// and a tile cannot show. It becomes an absent value instead.
    /// </summary>
    private static double? Clean(double? value) =>
        value is null || double.IsNaN(value.Value) || double.IsInfinity(value.Value) ? null : value;
}
