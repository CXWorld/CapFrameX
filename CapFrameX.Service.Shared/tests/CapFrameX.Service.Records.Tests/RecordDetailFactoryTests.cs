using CapFrameX.Service.Contracts.Records;

namespace CapFrameX.Service.Records.Tests;

/// <summary>
/// What the analysis view is told about a capture before it draws anything.
/// </summary>
public sealed class RecordDetailFactoryTests
{
    private readonly RecordFileReader _reader = new();

    [Fact]
    public void The_machine_is_read_out_of_the_capture()
    {
        var info = RecordDetailFactory.Info(Parse(RecordFixtures.Capture));

        Assert.Equal("Cyberpunk 2077", info.GameName);
        Assert.Equal("Cyberpunk2077", info.ProcessName);
        Assert.Equal("Ryzen 9 9950X", info.Processor);
        Assert.Equal("RTX 5090", info.Gpu);
        Assert.Equal("32 GB", info.SystemRam);
        Assert.Equal("Windows 11", info.Os);
        Assert.Equal("ultra settings", info.Comment);
    }

    [Fact]
    public void A_field_the_capture_left_blank_comes_back_as_no_answer()
    {
        // The frontend can then leave the row out instead of showing an empty label.
        var info = RecordDetailFactory.Info(Parse(RecordFixtures.Capture));

        Assert.Null(info.Motherboard);
        Assert.Null(info.ResolutionInfo);
    }

    [Theory]
    [InlineData("Enabled", true)]
    [InlineData("enabled", true)]
    [InlineData("Disabled", false)]
    [InlineData("", null)]
    [InlineData("Error", null)]
    public void A_platform_switch_has_three_states_rather_than_two(string written, bool? expected)
    {
        // CapFrameX writes an empty string where it could not find out, and "not known" is not the
        // same claim as "off".
        var capture = RecordFixtures.Capture.Replace(
            "\"OS\": \"Windows 11\"",
            $"\"OS\": \"Windows 11\", \"ResizableBar\": \"{written}\"",
            StringComparison.Ordinal);

        Assert.Equal(expected, RecordDetailFactory.Info(Parse(capture)).ResizableBar);
    }

    [Fact]
    public void Every_run_is_described_with_its_index()
    {
        // The index is what the analysis endpoint takes, so it has to come from here.
        var runs = RecordDetailFactory.Runs(Parse(RecordFixtures.CaptureWithTwoRuns));

        Assert.Equal([0, 1], runs.Select(run => run.Index));
        Assert.All(runs, run =>
        {
            Assert.Equal(3, run.FrameCount);
            Assert.Equal("DXGI", run.PresentMonRuntime);
            Assert.True(run.DurationSeconds > 0);
        });
    }

    [Fact]
    public void A_run_says_which_columns_it_carries()
    {
        var plain = RecordDetailFactory.Runs(Parse(RecordFixtures.Capture))[0];
        var withLatency = RecordDetailFactory.Runs(Parse(RecordFixtures.CaptureWithLatency))[0];

        Assert.False(plain.HasPcLatency);
        Assert.True(plain.HasDisplayChange);
        Assert.False(plain.HasGpuActive);
        Assert.True(withLatency.HasPcLatency);
    }

    [Fact]
    public void A_run_without_frame_data_is_still_a_run()
    {
        // Dropping it would renumber the runs after it, and the analysis asks by index.
        var runs = RecordDetailFactory.Runs(Parse(RecordFixtures.CaptureWithoutFrameData));

        var run = Assert.Single(runs);
        Assert.Equal(0, run.FrameCount);
        Assert.Equal(0, run.DurationSeconds);
    }

    [Fact]
    public void The_chips_are_the_facts_the_capture_knows()
    {
        var chips = RecordDetailFactory.Chips(Parse(RecordFixtures.Capture));

        Assert.Equal("Ryzen 9 9950X", Label(chips, ChipKeys.Processor));
        Assert.Equal("RTX 5090", Label(chips, ChipKeys.Gpu));
        Assert.Equal("32 GB", Label(chips, ChipKeys.Memory));
    }

    [Fact]
    public void A_fact_the_capture_does_not_know_gets_no_chip()
    {
        var chips = RecordDetailFactory.Chips(Parse(RecordFixtures.Capture));

        Assert.DoesNotContain(chips, chip => chip.Key == ChipKeys.Resolution);
        Assert.DoesNotContain(chips, chip => chip.Key == ChipKeys.Driver);
    }

    [Fact]
    public void The_driver_chip_says_what_the_number_is()
    {
        // A bare "566.36" pill means nothing next to a resolution and an API.
        var capture = RecordFixtures.Capture.Replace(
            "\"OS\": \"Windows 11\"",
            "\"OS\": \"Windows 11\", \"GPUDriverVersion\": \"566.36\"",
            StringComparison.Ordinal);

        Assert.Equal("Driver 566.36", Label(RecordDetailFactory.Chips(Parse(capture)), ChipKeys.Driver));
    }

    [Fact]
    public void The_switches_that_are_on_share_one_chip()
    {
        // One pill rather than three: they are only interesting together, and a row of "Disabled"
        // pills says nothing a reader wants.
        var capture = RecordFixtures.Capture.Replace(
            "\"OS\": \"Windows 11\"",
            "\"OS\": \"Windows 11\", \"ResizableBar\": \"Enabled\", \"HAGS\": \"Enabled\", \"WinGameMode\": \"Disabled\"",
            StringComparison.Ordinal);

        Assert.Equal("ReBAR · HAGS", Label(RecordDetailFactory.Chips(Parse(capture)), ChipKeys.Features));
    }

    [Fact]
    public void A_capture_with_no_switch_on_gets_no_feature_chip()
    {
        var capture = RecordFixtures.Capture.Replace(
            "\"OS\": \"Windows 11\"",
            "\"OS\": \"Windows 11\", \"ResizableBar\": \"Disabled\", \"HAGS\": \"Disabled\"",
            StringComparison.Ordinal);

        Assert.DoesNotContain(RecordDetailFactory.Chips(Parse(capture)), chip => chip.Key == ChipKeys.Features);
    }

    [RequiresRealCapturesFact]
    public void Every_capture_in_the_folder_describes_itself()
    {
        // Real captures carry fields the hand-written ones do not, and leave out fields they do.
        var described = 0;

        foreach (var path in CaptureFolder.Records())
        {
            var read = _reader.Parse(File.ReadAllText(path), path);

            if (read.Session is not { } session)
            {
                continue;
            }

            var info = RecordDetailFactory.Info(session);
            var runs = RecordDetailFactory.Runs(session);

            Assert.Equal(session.Runs.Count, runs.Count);
            Assert.Equal(Enumerable.Range(0, runs.Count), runs.Select(run => run.Index));
            Assert.NotNull(info.ProcessName);
            Assert.All(RecordDetailFactory.Chips(session), chip => Assert.False(string.IsNullOrWhiteSpace(chip.Label)));

            described++;
        }

        Assert.True(described > 0, $"No usable capture under '{CaptureFolder.Path}'.");
    }

    private static string? Label(IReadOnlyList<ChipDto> chips, string key) =>
        chips.FirstOrDefault(chip => chip.Key == key)?.Label;

    private CapFrameX.Data.Session.Contracts.ISession Parse(string content)
    {
        var read = _reader.Parse(content);

        Assert.True(read.IsSuccess, read.Error);

        return read.Session!;
    }
}
