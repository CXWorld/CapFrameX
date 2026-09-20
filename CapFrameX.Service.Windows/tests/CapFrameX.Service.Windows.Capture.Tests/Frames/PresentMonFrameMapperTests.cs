using CapFrameX.Service.Capture.Frames;
using CapFrameX.Service.Contracts.Frames;

namespace CapFrameX.Service.Capture.Tests.Frames;

/// <summary>
/// The mapper is the only place that knows PresentMon's output shape, so the distinction between
/// "measured zero" and "not measured" is settled here.
/// </summary>
public sealed class PresentMonFrameMapperTests
{
    private const string Row =
        "Cyberpunk2077.exe,15240,0x0000022D1B4B0BE0,DXGI,0,0,1,Hardware: Independent Flip,Application," +
        "12.5,16.61,16.62,16.60,0.42,7.81,9.12,11.73,1234.5,16.58,4.21,12.40,3.10,6.80,7.20,1.90," +
        "0.15,12.4,0.05,13.9";

    private static PresentMonFrameMapper Mapper(string header) =>
        new(PresentMonColumnLayout.FromHeader(header));

    private static string[] Split(string row) => row.Split(',');

    [Fact]
    public void Maps_a_full_row()
    {
        var mapper = Mapper(PresentMonColumnLayoutTests.HeaderWithPcLatency);

        Assert.True(mapper.TryMap(Split(Row), out var frame));

        Assert.Equal(15240, frame.ProcessId);
        Assert.Equal(12.5, frame.TimeInSeconds);
        Assert.Equal(16.62, frame.MsBetweenPresents);
        Assert.Equal(16.60, frame.MsBetweenDisplayChange);
        Assert.Equal(7.81, frame.MsUntilRenderComplete);
        Assert.Equal(9.12, frame.MsUntilDisplayed);
        Assert.Equal(11.73, frame.MsPcLatency);
        Assert.Equal(4.21, frame.MsCpuBusy);
        Assert.Equal(7.20, frame.MsGpuBusy);
        Assert.Equal(0.15, frame.MsAnimationError);
        Assert.Equal("Hardware: Independent Flip", frame.PresentMode);
        Assert.Equal("Application", frame.FrameType);
        Assert.Equal(0x0000022D1B4B0BE0UL, frame.SwapChainId);
    }

    [Fact]
    public void Metric_missing_from_the_layout_stays_null()
    {
        var mapper = Mapper(PresentMonColumnLayoutTests.HeaderWithoutPcLatency);
        var row = Split(Row);

        // The layout without PC latency has one column less; drop it from the row as PresentMon would.
        var withoutPcLatency = row.Take(16).Concat(row.Skip(17)).ToArray();

        Assert.True(mapper.TryMap(withoutPcLatency, out var frame));

        Assert.Null(frame.MsPcLatency);
        Assert.False(mapper.AvailableMetrics.HasFlag(FrameMetrics.PcLatency));
        Assert.Equal(16.62, frame.MsBetweenPresents);
    }

    [Theory]
    [InlineData("NA")]
    [InlineData("")]
    [InlineData("  ")]
    public void Value_PresentMon_could_not_determine_becomes_null_not_zero(string raw)
    {
        var mapper = Mapper(PresentMonColumnLayoutTests.HeaderWithPcLatency);
        var row = Split(Row);
        row[PresentMonColumnLayout.FromHeader(PresentMonColumnLayoutTests.HeaderWithPcLatency)
            .IndexOf(PresentMonColumns.MsGpuBusy)] = raw;

        Assert.True(mapper.TryMap(row, out var frame));

        Assert.Null(frame.MsGpuBusy);
    }

    [Fact]
    public void Measured_zero_is_kept()
    {
        var mapper = Mapper(PresentMonColumnLayoutTests.HeaderWithPcLatency);
        var row = Split(Row);
        row[23] = "0";

        Assert.True(mapper.TryMap(row, out var frame));

        Assert.Equal(0d, frame.MsGpuBusy);
    }

    [Fact]
    public void Error_line_is_not_a_frame()
    {
        var mapper = Mapper(PresentMonColumnLayoutTests.HeaderWithPcLatency);
        var row = Split(Row);
        row[0] = PresentMonColumns.ErrorMarker;

        Assert.False(mapper.TryMap(row, out _));
    }

    [Fact]
    public void Truncated_row_is_not_a_frame()
    {
        var mapper = Mapper(PresentMonColumnLayoutTests.HeaderWithPcLatency);

        Assert.False(mapper.TryMap(Split(Row).Take(5).ToArray(), out _));
    }

    [Theory]
    [InlineData(1, "not-a-number")]
    [InlineData(9, "NA")]
    [InlineData(11, "")]
    public void Row_without_a_usable_mandatory_value_is_not_a_frame(int index, string raw)
    {
        var mapper = Mapper(PresentMonColumnLayoutTests.HeaderWithPcLatency);
        var row = Split(Row);
        row[index] = raw;

        Assert.False(mapper.TryMap(row, out _));
    }

    [Fact]
    public void Swap_chain_address_without_hex_prefix_is_read()
    {
        var mapper = Mapper(PresentMonColumnLayoutTests.HeaderWithPcLatency);
        var row = Split(Row);
        row[2] = "22D1B4B0BE0";

        Assert.True(mapper.TryMap(row, out var frame));

        Assert.Equal(0x22D1B4B0BE0UL, frame.SwapChainId);
    }

    [Fact]
    public void Numbers_are_read_independently_of_the_current_culture()
    {
        var previous = Thread.CurrentThread.CurrentCulture;
        Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("de-DE");
        try
        {
            var mapper = Mapper(PresentMonColumnLayoutTests.HeaderWithPcLatency);

            Assert.True(mapper.TryMap(Split(Row), out var frame));

            Assert.Equal(16.62, frame.MsBetweenPresents);
            Assert.Equal(12.5, frame.TimeInSeconds);
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = previous;
        }
    }
}
