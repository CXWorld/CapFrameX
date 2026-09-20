using CapFrameX.Service.Records;

namespace CapFrameX.Service.Records.Tests;

/// <summary>
/// The sparkline is the only thing in the record list that says whether a capture was smooth, so
/// what it must never do is lose the spike that made it interesting.
/// </summary>
public sealed class FrametimeSparklineTests
{
    private static double[] Steady(int count, double value = 16.6) =>
        [.. Enumerable.Repeat(value, count)];

    [Fact]
    public void Series_shorter_than_the_budget_is_kept_as_it_is()
    {
        double[] values = [16.6, 33.2, 16.5];

        Assert.Equal(values, FrametimeSparkline.Decimate(values, 64));
    }

    [Fact]
    public void Long_series_is_reduced_to_the_budget()
    {
        var result = FrametimeSparkline.Decimate(Steady(10_000), 64);

        Assert.InRange(result.Length, 2, 64);
    }

    [Fact]
    public void A_single_spike_survives_decimation()
    {
        // The whole point: one 200 ms frame among ten thousand smooth ones is what the user is
        // looking for, and it is exactly what averaging or every-nth sampling throws away.
        var values = Steady(10_000);
        values[4_321] = 200.0;

        var result = FrametimeSparkline.Decimate(values, 64);

        Assert.Contains(200.0, result);
    }

    [Fact]
    public void The_lowest_frame_time_survives_too()
    {
        var values = Steady(5_000);
        values[1_234] = 1.5;

        Assert.Contains(1.5, FrametimeSparkline.Decimate(values, 32));
    }

    [Fact]
    public void Points_stay_in_time_order()
    {
        // A rising series must come out rising; a sparkline that reorders its points is a lie.
        var values = Enumerable.Range(0, 1_000).Select(i => (double)i).ToArray();

        var result = FrametimeSparkline.Decimate(values, 64);

        Assert.Equal(result.OrderBy(v => v), result);
    }

    [Fact]
    public void Empty_series_yields_an_empty_sparkline()
    {
        Assert.Empty(FrametimeSparkline.Decimate([], 64));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(0)]
    [InlineData(-1)]
    public void Asking_for_fewer_than_two_points_is_rejected(int maxPoints)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => FrametimeSparkline.Decimate(Steady(100), maxPoints));
    }

    [Fact]
    public void Budget_of_two_yields_the_extremes()
    {
        var values = Steady(500);
        values[100] = 90.0;
        values[400] = 2.0;

        var result = FrametimeSparkline.Decimate(values, 2);

        Assert.Equal(2, result.Length);
        Assert.Contains(90.0, result);
        Assert.Contains(2.0, result);
    }

    [Fact]
    public void Series_of_one_is_kept()
    {
        Assert.Equal([16.6], FrametimeSparkline.Decimate([16.6], 64));
    }
}
