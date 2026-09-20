using CapFrameX.Data.Session.Contracts;
using CapFrameX.Service.Contracts.Analysis;
using CapFrameX.Statistics.NetStandard;
using CapFrameX.Statistics.NetStandard.Contracts;

namespace CapFrameX.Service.Analysis.Tests;

/// <summary>
/// What the analysis makes of a capture.
/// </summary>
/// <remarks>
/// The numbers themselves are not under test - they come from the same provider the desktop app
/// uses, and <see cref="MetricParityTests"/> pins that they still do. These tests are about the
/// mapping around it: which frames the window admits, which run is analysed, which sequence a
/// metric is fed, and what happens when the capture does not carry what was asked for.
/// </remarks>
public sealed class AnalysisServiceTests
{
    private static readonly AnalysisSettings Settings = new();

    [Fact]
    public void A_capture_yields_the_default_tiles_in_order()
    {
        var analysis = Analyze(Captures.Of(Captures.Run(Captures.Steady())), new AnalysisRequest());

        Assert.Equal(
            MetricCatalog.Default.Select(MetricCatalog.Key),
            analysis.Metrics.Select(metric => metric.Key));
        Assert.All(analysis.Metrics, metric => Assert.NotNull(metric.Value));
    }

    [Fact]
    public void The_metrics_come_back_in_the_order_they_were_asked_for()
    {
        // The tile layout is the caller's, not the catalogue's.
        var request = new AnalysisRequest { Metrics = [EMetric.P1, EMetric.Max, EMetric.Average] };

        var analysis = Analyze(Captures.Of(Captures.Run(Captures.Steady())), request);

        Assert.Equal(["p1", "max", "average"], analysis.Metrics.Select(metric => metric.Key));
    }

    [Fact]
    public void A_window_cuts_the_capture_down_to_what_it_covers()
    {
        // 60 frames of 16.6 ms is just under a second; half of it is about half the frames.
        var session = Captures.Of(Captures.Run(Captures.Steady()));

        var whole = Analyze(session, new AnalysisRequest());
        var half = Analyze(session, new AnalysisRequest { EndSeconds = 0.5 });

        Assert.Equal(60, whole.Window.FrameCount);
        Assert.InRange(half.Window.FrameCount, 25, 35);
        Assert.Equal(0.5, half.Window.EndSeconds);
    }

    [Fact]
    public void A_window_with_nothing_in_it_is_an_answer_rather_than_an_error()
    {
        var session = Captures.Of(Captures.Run(Captures.Steady()));

        var analysis = Analyze(session, new AnalysisRequest { StartSeconds = 100, EndSeconds = 200 });

        Assert.Equal(0, analysis.Window.FrameCount);
        Assert.All(analysis.Metrics, metric => Assert.Null(metric.Value));
        Assert.Equal(0, analysis.FramePacing.SpikeCount);
        Assert.Empty(analysis.LShape);
    }

    [Fact]
    public void An_end_before_the_start_covers_nothing_rather_than_turning_around()
    {
        var session = Captures.Of(Captures.Run(Captures.Steady()));

        var analysis = Analyze(session, new AnalysisRequest { StartSeconds = 0.6, EndSeconds = 0.2 });

        Assert.Equal(0.6, analysis.Window.StartSeconds);
        Assert.Equal(0.6, analysis.Window.EndSeconds);
    }

    [Fact]
    public void One_run_can_be_analysed_on_its_own()
    {
        var session = Captures.Of(
            Captures.Run(Captures.Steady(30)),
            Captures.Run(Captures.Steady(45)));

        var second = Analyze(session, new AnalysisRequest { Run = 1 });

        Assert.Equal(45, second.Window.FrameCount);
        Assert.Equal(1, second.Window.Run);
        Assert.Equal(75, Analyze(session, new AnalysisRequest()).Window.FrameCount);
    }

    [Fact]
    public void A_run_the_capture_does_not_have_is_refused()
    {
        var session = Captures.Of(Captures.Run(Captures.Steady()));

        var error = Assert.Throws<ArgumentOutOfRangeException>(
            () => Analyze(session, new AnalysisRequest { Run = 3 }));

        Assert.Contains("1 run", error.Message);
    }

    [Fact]
    public void The_frame_pacing_split_accounts_for_the_whole_capture()
    {
        var analysis = Analyze(Captures.Of(Captures.Run(Uneven())), new AnalysisRequest());
        var pacing = analysis.FramePacing;

        Assert.Equal(100d, pacing.SmoothPercent + pacing.StutterPercent + pacing.LowFpsPercent, 9);
    }

    [Fact]
    public void A_single_long_frame_is_counted_and_located()
    {
        var frametimes = Captures.Steady().ToList();
        frametimes.Insert(40, 200);

        var pacing = Analyze(Captures.Of(Captures.Run(frametimes)), new AnalysisRequest()).FramePacing;

        Assert.Equal(1, pacing.SpikeCount);
        Assert.NotNull(pacing.WorstSpike);
        Assert.Equal(200, pacing.WorstSpike.Milliseconds);
        // Forty frames of 16.6 ms in, so about two thirds of a second.
        Assert.Equal(40 * 0.0166, pacing.WorstSpike.TimeSeconds, 3);
    }

    [Fact]
    public void A_capture_that_never_hitches_reports_no_spikes()
    {
        var pacing = Analyze(Captures.Of(Captures.Run(Captures.Steady())), new AnalysisRequest()).FramePacing;

        Assert.Equal(0, pacing.SpikeCount);
        Assert.Null(pacing.WorstSpike);
        Assert.Equal(100d, pacing.SmoothPercent, 9);
    }

    [Fact]
    public void The_spike_count_and_the_stutter_share_tell_the_same_story()
    {
        // Two numbers from one rule: a capture cannot report stutter without a spike behind it.
        var frametimes = Captures.Steady().ToList();
        frametimes.Insert(20, 150);
        frametimes.Insert(45, 180);

        var pacing = Analyze(Captures.Of(Captures.Run(frametimes)), new AnalysisRequest()).FramePacing;

        Assert.Equal(2, pacing.SpikeCount);
        Assert.True(pacing.StutterPercent > 0);
    }

    [Fact]
    public void The_l_shape_has_one_point_per_quantile_of_the_metric_it_was_asked_for()
    {
        var session = Captures.Of(Captures.Run(Uneven()));
        var analyzer = new FrametimeAnalyzer();

        var overFrametimes = Analyze(session, new AnalysisRequest());
        var overFps = Analyze(session, new AnalysisRequest { LShapeMetric = ELShapeMetrics.FPS });

        Assert.Equal(
            analyzer.GetLShapeQuantiles(ELShapeMetrics.Frametimes),
            overFrametimes.LShape.Select(point => point.X));
        Assert.Equal(
            analyzer.GetLShapeQuantiles(ELShapeMetrics.FPS),
            overFps.LShape.Select(point => point.X));
    }

    [Fact]
    public void The_l_shape_over_frame_times_rises_towards_the_slow_end()
    {
        // The quantiles run 90 to 99.95, so a later point is a slower frame.
        var curve = Analyze(Captures.Of(Captures.Run(Uneven())), new AnalysisRequest()).LShape;

        Assert.Equal(curve.OrderBy(point => point.Y).Select(point => point.Y), curve.Select(point => point.Y));
    }

    [Fact]
    public void The_distribution_is_the_one_the_desktop_app_draws()
    {
        var session = Captures.Of(Captures.Run(Uneven()));
        var end = session.Runs[0].CaptureData.TimeInSeconds[^1];

        var analysis = Analyze(session, new AnalysisRequest());

        var expected = session.GetFrametimeDistributionPoints(0, end, Settings);
        Assert.Equal(expected.Count, analysis.Distribution.Count);
        Assert.Equal(expected.Select(point => point.Y), analysis.Distribution.Select(point => point.Y));
    }

    [Fact]
    public void A_capture_without_latency_reports_none()
    {
        var analysis = Analyze(Captures.Of(Captures.Run(Captures.Steady())), new AnalysisRequest());

        Assert.Null(analysis.PcLatency);
    }

    [Fact]
    public void Latency_is_read_even_from_a_capture_without_sensor_data()
    {
        // The desktop app hides it in that case, which would make the record list promise a value
        // the analysis never delivers.
        var frametimes = Captures.Steady(10);
        var latency = Enumerable.Range(0, 10).Select(i => 30d + i).ToArray();

        var analysis = Analyze(
            Captures.Of(Captures.Run(frametimes, pcLatency: latency, sensorData: false)),
            new AnalysisRequest());

        Assert.NotNull(analysis.PcLatency);
        Assert.Equal(34.5, analysis.PcLatency.AverageMs, 6);
    }

    [Fact]
    public void Latency_follows_the_window()
    {
        var frametimes = Captures.Steady(60);
        var latency = Enumerable.Repeat(30d, 30).Concat(Enumerable.Repeat(60d, 30)).ToArray();

        var first = Analyze(
            Captures.Of(Captures.Run(frametimes, pcLatency: latency)),
            new AnalysisRequest { EndSeconds = 0.4 });

        Assert.NotNull(first.PcLatency);
        Assert.Equal(30, first.PcLatency.AverageMs);
    }

    [Fact]
    public void A_gpu_metric_has_no_value_where_the_capture_has_no_gpu_column()
    {
        var request = new AnalysisRequest { Metrics = [EMetric.GpuActiveAverage, EMetric.Average] };

        var analysis = Analyze(Captures.Of(Captures.Run(Captures.Steady())), request);

        Assert.Null(analysis.Metrics[0].Value);
        Assert.NotNull(analysis.Metrics[1].Value);
    }

    [Fact]
    public void A_gpu_metric_is_computed_from_the_gpu_column_rather_than_the_frame_times()
    {
        // Half the frame time busy on the GPU means twice the frame rate; reading the wrong column
        // would return the plain average under a GPU label.
        var frametimes = Captures.Steady(60, 20);
        var gpuActive = Enumerable.Repeat(10d, 60).ToArray();
        var request = new AnalysisRequest { Metrics = [EMetric.GpuActiveAverage, EMetric.Average] };

        var analysis = Analyze(
            Captures.Of(Captures.Run(frametimes, gpuActive: gpuActive)),
            request);

        Assert.Equal(100, analysis.Metrics[0].Value);
        Assert.Equal(50, analysis.Metrics[1].Value);
    }

    [Fact]
    public void The_answer_says_what_it_was_told_to_assume()
    {
        var request = new AnalysisRequest { OutlierMethod = ERemoveOutlierMethod.DeciPercentile };

        var thresholds = Analyze(Captures.Of(Captures.Run(Captures.Steady())), request).Thresholds;

        Assert.Equal(Settings.StutteringFactor, thresholds.StutteringFactor);
        Assert.Equal(Settings.StutteringThreshold, thresholds.StutteringThreshold);
        Assert.Equal("DeciPercentile", thresholds.OutlierMethod);
    }

    [Fact]
    public void Removing_outliers_drops_the_slowest_frames()
    {
        var frametimes = Captures.Steady(1000).ToList();
        frametimes[500] = 500;

        var session = Captures.Of(Captures.Run(frametimes));
        var kept = Analyze(session, new AnalysisRequest());
        var trimmed = Analyze(session, new AnalysisRequest { OutlierMethod = ERemoveOutlierMethod.DeciPercentile });

        Assert.Equal(1000, kept.Window.FrameCount);
        Assert.Equal(999, trimmed.Window.FrameCount);
        Assert.Equal(0, trimmed.FramePacing.SpikeCount);
    }

    internal static AnalysisDto Analyze(ISession session, AnalysisRequest request) =>
        new AnalysisService(Settings).Analyze(session, request);

    /// <summary>Frame times that are not all the same, so percentiles differ from the average.</summary>
    internal static double[] Uneven(int count = 600)
    {
        var random = new Random(20260920);

        return [.. Enumerable.Range(0, count).Select(_ => 10d + random.NextDouble() * 15d)];
    }
}
