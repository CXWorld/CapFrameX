using CapFrameX.Data.Session.Contracts;
using CapFrameX.Service.Contracts.Analysis;
using CapFrameX.Statistics.NetStandard.Contracts;

namespace CapFrameX.Service.Analysis.Tests;

/// <summary>
/// The columns behind the chart.
/// </summary>
/// <remarks>
/// A chart draws these as they arrive, so the shape matters as much as the values: one time axis,
/// every other column indexed by it, and a gap where a frame has no value rather than a zero that
/// would draw a line to the floor.
/// </remarks>
public sealed class SeriesTests
{
    private static readonly AnalysisSettings Settings = new();

    [Fact]
    public void Only_the_frame_times_come_back_unless_more_is_asked_for()
    {
        // Every further column is another array the size of the capture.
        var series = Series(Captures.Of(Captures.Run(Captures.Steady())), new SeriesRequest());

        Assert.NotNull(series.Frametimes);
        Assert.Null(series.Fps);
        Assert.Null(series.DisplayChange);
        Assert.Null(series.PcLatency);
    }

    [Fact]
    public void Every_column_is_as_long_as_the_time_axis()
    {
        var session = Captures.Of(Captures.Run(
            Captures.Steady(),
            pcLatency: Enumerable.Repeat(30d, 60).ToArray(),
            displayChange: Captures.Steady()));

        var series = Series(session, new SeriesRequest { Kinds = SeriesKinds.All });

        Assert.Equal(60, series.Time.Length);
        Assert.Equal(60, series.Frametimes!.Length);
        Assert.Equal(60, series.Fps!.Length);
        Assert.Equal(60, series.DisplayChange!.Length);
        Assert.Equal(60, series.PcLatency!.Length);
    }

    [Fact]
    public void The_frame_rate_column_is_the_reciprocal_of_the_frame_times()
    {
        var series = Series(
            Captures.Of(Captures.Run(Captures.Steady(10, 20))),
            new SeriesRequest { Kinds = [SeriesKinds.Fps] });

        Assert.All(series.Fps!, value => Assert.Equal(50, value));
    }

    [Fact]
    public void A_frame_without_a_value_leaves_a_gap_rather_than_a_zero()
    {
        // Half the frames have a display change recorded; a zero there would draw a line to the
        // floor, which is a different claim from "not measured".
        var displayChange = Enumerable.Repeat(16.6, 5).Concat(Enumerable.Repeat(0d, 5)).ToArray();
        var session = Captures.Of(Captures.Run(Captures.Steady(10), displayChange: displayChange));

        var series = Series(session, new SeriesRequest { Kinds = [SeriesKinds.DisplayChange] });

        Assert.Equal(5, series.DisplayChange!.Count(value => value is not null));
        Assert.Equal(5, series.DisplayChange!.Count(value => value is null));
    }

    [Fact]
    public void A_column_lines_up_with_the_frame_it_belongs_to()
    {
        var latency = Enumerable.Range(0, 10).Select(i => 30d + i).ToArray();
        var session = Captures.Of(Captures.Run(Captures.Steady(10), pcLatency: latency));

        var series = Series(session, new SeriesRequest { Kinds = [SeriesKinds.PcLatency] });

        Assert.Equal(latency.Select(value => (double?)value), series.PcLatency!);
    }

    [Fact]
    public void The_window_cuts_the_columns_down_with_it()
    {
        var session = Captures.Of(Captures.Run(Captures.Steady()));

        var series = Series(session, new SeriesRequest { EndSeconds = 0.5 });

        Assert.InRange(series.Time.Length, 25, 35);
        Assert.Equal(series.Time.Length, series.Window.FrameCount);
        Assert.All(series.Time, time => Assert.InRange(time, 0, 0.5));
    }

    [Fact]
    public void One_run_can_be_charted_on_its_own()
    {
        var session = Captures.Of(Captures.Run(Captures.Steady(30)), Captures.Run(Captures.Steady(45)));

        Assert.Equal(45, Series(session, new SeriesRequest { Run = 1 }).Time.Length);
    }

    [Fact]
    public void The_chart_keeps_every_frame_the_capture_recorded()
    {
        // The analysis may remove outliers; the chart must not, or it would silently redraw the
        // capture as smoother than it was.
        var frametimes = Captures.Steady(1000).ToList();
        frametimes[500] = 500;
        var session = Captures.Of(Captures.Run(frametimes));

        var series = Series(session, new SeriesRequest());

        Assert.Equal(1000, series.Time.Length);
        Assert.Contains(500d, series.Frametimes!);
    }

    [Fact]
    public void An_unknown_kind_is_ignored_rather_than_fatal()
    {
        var series = Series(
            Captures.Of(Captures.Run(Captures.Steady())),
            new SeriesRequest { Kinds = ["frametimes", "something-else"] });

        Assert.NotNull(series.Frametimes);
    }

    [Fact]
    public void Kinds_are_matched_whatever_the_casing()
    {
        var series = Series(
            Captures.Of(Captures.Run(Captures.Steady())),
            new SeriesRequest { Kinds = ["FrameTimes"] });

        Assert.NotNull(series.Frametimes);
    }

    private static SeriesResponse Series(ISession session, SeriesRequest request) =>
        new AnalysisService(Settings).Series(session, request);
}
