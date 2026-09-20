using CapFrameX.Data.Session.Contracts;
using CapFrameX.Service.Records;
using CapFrameX.Statistics.NetStandard;
using CapFrameX.Statistics.NetStandard.Contracts;

namespace CapFrameX.Service.Analysis.Tests;

/// <summary>
/// The service and the desktop app must answer every question with the same number.
/// </summary>
/// <remarks>
/// They run the same provider, so this is not a test of the mathematics. It guards the mapping
/// around it - the window, the outlier method, the rounding digits, which sequence goes in - which
/// is the part that can drift and the part a user would notice, because a record analysed in both
/// places would then disagree with itself.
/// </remarks>
public sealed class MetricParityTests
{
    private static readonly AnalysisSettings Settings = new();

    public static TheoryData<EMetric> FrametimeMetrics()
    {
        var data = new TheoryData<EMetric>();

        foreach (var metric in MetricCatalog.Supported.Where(m => MetricCatalog.Source(m) == MetricSource.Frametimes))
        {
            data.Add(metric);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(FrametimeMetrics))]
    public void A_metric_equals_what_the_desktop_app_computes(EMetric metric)
    {
        var session = Captures.Of(Captures.Run(AnalysisServiceTests.Uneven()));

        Assert.Equal(Expected(session, metric), Actual(session, metric, new AnalysisRequest { Metrics = [metric] }));
    }

    [Theory]
    [MemberData(nameof(FrametimeMetrics))]
    public void A_metric_still_agrees_after_outliers_are_removed(EMetric metric)
    {
        var session = Captures.Of(Captures.Run(AnalysisServiceTests.Uneven()));
        var request = new AnalysisRequest
        {
            Metrics = [metric],
            OutlierMethod = ERemoveOutlierMethod.DeciPercentile,
        };

        Assert.Equal(
            Expected(session, metric, outliers: ERemoveOutlierMethod.DeciPercentile),
            Actual(session, metric, request));
    }

    [Theory]
    [MemberData(nameof(FrametimeMetrics))]
    public void A_metric_still_agrees_inside_a_window(EMetric metric)
    {
        var session = Captures.Of(Captures.Run(AnalysisServiceTests.Uneven()));
        var request = new AnalysisRequest { Metrics = [metric], StartSeconds = 1, EndSeconds = 4 };

        Assert.Equal(Expected(session, metric, start: 1, end: 4), Actual(session, metric, request));
    }

    [Fact]
    public void The_sequence_the_service_analyses_is_the_one_the_desktop_app_analyses()
    {
        // The service takes the frame times as points, because a spike has to keep the time it
        // happened at. That has to select the same frames as the plain value path, or every number
        // above is right about the wrong window.
        var session = Captures.Of(Captures.Run(AnalysisServiceTests.Uneven()));
        var end = End(session);

        foreach (var outliers in new[] { ERemoveOutlierMethod.None, ERemoveOutlierMethod.DeciPercentile })
        {
            Assert.Equal(
                session.GetFrametimeTimeWindow(0, end, Settings, outliers),
                session.GetFrametimePointsTimeWindow(0, end, Settings, outliers).Select(point => point.Y));
        }
    }

    [Fact]
    public void The_frame_pacing_percentages_are_the_desktop_app_numbers()
    {
        var session = Captures.Of(Captures.Run(AnalysisServiceTests.Uneven()));
        var provider = new FrametimeStatisticProvider(Settings);
        var frametimes = session.GetFrametimeTimeWindow(0, End(session), Settings);

        var pacing = new AnalysisService(Settings).Analyze(session, new AnalysisRequest()).FramePacing;

        Assert.Equal(
            provider.GetStutteringTimePercentage(frametimes, Settings.StutteringFactor),
            pacing.StutterPercent);
        Assert.Equal(
            provider.GetLowFPSTimePercentage(frametimes, Settings.StutteringFactor, Settings.StutteringThreshold),
            pacing.LowFpsPercent);
    }

    [Fact]
    public void The_l_shape_is_the_desktop_app_curve()
    {
        var session = Captures.Of(Captures.Run(AnalysisServiceTests.Uneven()));
        var provider = new FrametimeStatisticProvider(Settings);
        var frametimes = session.GetFrametimeTimeWindow(0, End(session), Settings);

        var curve = new AnalysisService(Settings).Analyze(session, new AnalysisRequest()).LShape;

        Assert.Equal(
            new FrametimeAnalyzer()
                .GetLShapeQuantiles(ELShapeMetrics.Frametimes)
                .Select(quantile => provider.GetPQuantileSequence(frametimes, quantile / 100)),
            curve.Select(point => point.Y));
    }

    [Fact]
    public void Rounding_follows_the_configured_digits()
    {
        // The desktop app rounds inside the provider, so a different digit count here would show up
        // as a record that reads differently in the two applications.
        var session = Captures.Of(Captures.Run(AnalysisServiceTests.Uneven()));
        var settings = new AnalysisSettings { FpsValuesRoundingDigits = 4 };
        var expected = new FrametimeStatisticProvider(settings)
            .GetFpsMetricValue(session.GetFrametimeTimeWindow(0, End(session), settings), EMetric.Average);

        var value = new AnalysisService(settings)
            .Analyze(session, new AnalysisRequest { Metrics = [EMetric.Average] })
            .Metrics
            .Single()
            .Value;

        Assert.Equal(expected, value);
        Assert.NotEqual(expected, Math.Round(expected, 1));
    }

    [RequiresRealCapturesFact]
    public void Every_capture_in_the_folder_agrees_with_the_desktop_app()
    {
        // Hand-built frame times cannot produce what real captures do - dropped frames, several
        // runs, a first frame time of zero. This is where a mapping that only works on tidy data
        // falls over.
        var reader = new RecordFileReader();
        var service = new AnalysisService(Settings);
        var checkedFiles = 0;

        foreach (var path in CaptureFolder.Records())
        {
            var read = reader.Parse(File.ReadAllText(path), path);

            if (read.Session is not { } session || session.Runs.Count == 0)
            {
                continue;
            }

            var analysis = service.Analyze(session, new AnalysisRequest());

            foreach (var metric in MetricCatalog.Default)
            {
                Assert.Equal(Expected(session, metric), Value(analysis, metric));
            }

            checkedFiles++;
        }

        Assert.True(checkedFiles > 0, $"No usable capture under '{CaptureFolder.Path}'.");
    }

    private static double Expected(
        ISession session,
        EMetric metric,
        double start = 0,
        double? end = null,
        ERemoveOutlierMethod outliers = ERemoveOutlierMethod.None) =>
        new FrametimeStatisticProvider(Settings).GetFpsMetricValue(
            session.GetFrametimeTimeWindow(start, end ?? End(session), Settings, outliers),
            metric);

    private static double Actual(ISession session, EMetric metric, AnalysisRequest request) =>
        Value(new AnalysisService(Settings).Analyze(session, request), metric);

    private static double Value(Contracts.Analysis.AnalysisDto analysis, EMetric metric) =>
        analysis.Metrics.Single(m => m.Key == MetricCatalog.Key(metric)).Value ?? double.NaN;

    private static double End(ISession session) =>
        session.Runs.SelectMany(run => run.CaptureData.TimeInSeconds).DefaultIfEmpty(0).Last();
}
