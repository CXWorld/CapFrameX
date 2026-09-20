using CapFrameX.Service.Records;

namespace CapFrameX.Service.Records.Tests;

/// <summary>
/// The summary is what the list shows for thousands of captures, so it has to be right about the
/// two things a user scans for - how long the capture is and what it looked like - and honest about
/// the metrics a capture does not carry.
/// </summary>
public sealed class RecordSummaryFactoryTests
{
    private static readonly RecordFileReader Reader = new();

    private static RecordSummary Summarise(string json, string path = @"C:\captures\CapFrameX-Game.exe-2026.json") =>
        RecordSummaryFactory.Create(Reader.Parse(json).Session!, path);

    [Fact]
    public void Carries_the_identity_from_the_capture_and_the_file()
    {
        var summary = Summarise(RecordFixtures.Capture);

        Assert.Equal("Cyberpunk 2077", summary.GameName);
        Assert.Equal("Cyberpunk2077", summary.ProcessName);
        Assert.Equal("CapFrameX-Game.exe-2026", summary.Name);
        Assert.Equal(@"C:\captures\CapFrameX-Game.exe-2026.json", summary.FilePath);
    }

    [Fact]
    public void Carries_the_hardware_the_capture_was_taken_on()
    {
        var summary = Summarise(RecordFixtures.Capture);

        Assert.Equal("Ryzen 9 9950X", summary.Processor);
        Assert.Equal("RTX 5090", summary.Gpu);
    }

    [Fact]
    public void Duration_is_the_span_the_frames_cover()
    {
        // The fixture's three frames run from 0 to 0.0333 s.
        var summary = Summarise(RecordFixtures.Capture);

        Assert.Equal(0.0333, summary.DurationSeconds, 4);
    }

    [Fact]
    public void Counts_runs_and_frames()
    {
        var summary = Summarise(RecordFixtures.Capture);

        Assert.Equal(1, summary.RunCount);
        Assert.Equal(3, summary.FrameCount);
    }

    [Fact]
    public void Sparkline_comes_from_the_frame_times()
    {
        var summary = Summarise(RecordFixtures.Capture);

        Assert.Equal([16.6, 16.7, 16.5], summary.Sparkline);
    }

    [Fact]
    public void Metrics_the_capture_does_not_carry_are_reported_absent()
    {
        // The list has to be able to say "this capture has no latency" rather than show a zero.
        var summary = Summarise(RecordFixtures.Capture);

        Assert.False(summary.HasPcLatency);
        Assert.True(summary.HasDisplayChange);
    }

    [Fact]
    public void Latency_is_reported_when_the_capture_carries_it()
    {
        Assert.True(Summarise(RecordFixtures.CaptureWithLatency).HasPcLatency);
    }

    [Fact]
    public void Several_runs_are_summed_not_taken_from_the_first()
    {
        var summary = Summarise(RecordFixtures.CaptureWithTwoRuns);

        Assert.Equal(2, summary.RunCount);
        Assert.Equal(6, summary.FrameCount);
        Assert.Equal(0.0666, summary.DurationSeconds, 4);
    }

    [Fact]
    public void Capture_without_frame_data_still_yields_a_summary()
    {
        // A run whose CaptureData is missing must not take the whole list down.
        var summary = Summarise(RecordFixtures.CaptureWithoutFrameData);

        Assert.Equal(0, summary.FrameCount);
        Assert.Equal(0d, summary.DurationSeconds);
        Assert.Empty(summary.Sparkline);
    }
}
