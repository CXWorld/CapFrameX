using CapFrameX.Statistics.NetStandard;
using CapFrameX.Statistics.NetStandard.Contracts;

namespace CapFrameX.Service.Analysis.Tests;

/// <summary>
/// The catalogue is what the frontend names a metric by, so what it lists and what it calls things
/// are both promises.
/// </summary>
public sealed class MetricCatalogTests
{
    private static readonly AnalysisSettings Settings = new();

    [Fact]
    public void Every_metric_has_its_own_key()
    {
        var keys = MetricCatalog.Supported.Select(MetricCatalog.Key).ToArray();

        Assert.Equal(keys.Length, keys.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public void A_key_leads_back_to_its_metric()
    {
        foreach (var metric in MetricCatalog.Supported)
        {
            Assert.True(MetricCatalog.TryParse(MetricCatalog.Key(metric), out var parsed));
            Assert.Equal(metric, parsed);
        }
    }

    [Theory]
    [InlineData("P95")]
    [InlineData("p95")]
    [InlineData(" p95 ")]
    public void Casing_and_padding_do_not_matter(string key)
    {
        // It arrives from a query string that a person may have typed.
        Assert.True(MetricCatalog.TryParse(key, out var parsed));
        Assert.Equal(EMetric.P95, parsed);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    [InlineData("p42")]
    [InlineData("cpuFpsPerWatt")]
    public void What_the_service_cannot_compute_does_not_parse(string? key)
    {
        Assert.False(MetricCatalog.TryParse(key, out _));
    }

    [Fact]
    public void The_catalogue_lists_exactly_what_the_provider_can_compute()
    {
        // The list is written out by hand, so this is what notices when EMetric grows: a new metric
        // the provider computes has to be offered, and one it answers with NaN must not be.
        var provider = new FrametimeStatisticProvider(Settings);
        var frametimes = Captures.Steady();

        foreach (EMetric metric in Enum.GetValues<EMetric>())
        {
            if (metric == EMetric.None)
            {
                continue;
            }

            var computable = !double.IsNaN(provider.GetFpsMetricValue(frametimes, metric));

            Assert.Equal(computable, MetricCatalog.Supported.Contains(metric));
        }
    }

    [Fact]
    public void The_default_tiles_are_all_offered()
    {
        Assert.All(MetricCatalog.Default, metric => Assert.Contains(metric, MetricCatalog.Supported));
    }

    [Fact]
    public void The_gpu_metrics_are_marked_as_needing_the_gpu_column()
    {
        // They share a branch with Average, P1 and the 1% low average, so feeding them frame times
        // would return the plain metric under a GPU label.
        Assert.Equal(MetricSource.GpuActive, MetricCatalog.Source(EMetric.GpuActiveAverage));
        Assert.Equal(MetricSource.GpuActive, MetricCatalog.Source(EMetric.GpuActiveP1));
        Assert.Equal(MetricSource.GpuActive, MetricCatalog.Source(EMetric.GpuActiveOnePercentLowAverage));
        Assert.Equal(MetricSource.Frametimes, MetricCatalog.Source(EMetric.Average));
    }

    [Fact]
    public void A_metric_outside_the_catalogue_is_refused_rather_than_guessed()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => MetricCatalog.Source(EMetric.CpuFpsPerWatt));
    }

    [Fact]
    public void A_metric_carries_the_label_CapFrameX_has_always_used()
    {
        var described = MetricCatalog.Describe(EMetric.OnePercentLowAverage, 50);

        Assert.Equal("onePercentLowAverage", described.Key);
        Assert.Equal("1% Low Avg", described.Label);
        Assert.Equal("fps", described.Unit);
        Assert.Equal(50, described.Value);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void A_value_that_is_not_a_number_becomes_no_value(double value)
    {
        // JSON cannot carry it and a tile cannot show it; an absent value says what happened.
        Assert.Null(MetricCatalog.Describe(EMetric.Average, value).Value);
    }
}
