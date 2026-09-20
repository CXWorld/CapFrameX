using System.Net.Http.Json;
using System.Text.Json;
using CapFrameX.Service.Contracts.Records;

namespace CapFrameX.Service.Api.Tests;

/// <summary>
/// What a deep link into one record gets.
/// </summary>
/// <remarks>
/// The detail is the index row plus the capture file: the summary comes from the row, the machine
/// and the runs from the file. Both halves are asserted here, because either one going missing
/// leaves a view that renders but says nothing.
/// </remarks>
public sealed class RecordDetailEndpointTests(GuardedApiFactory factory)
    : IClassFixture<GuardedApiFactory>, IAsyncLifetime
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly string _root = Path.Combine(Path.GetTempPath(), "cfx-detail-" + Guid.NewGuid().ToString("N"));

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(_root);
        await RecordSeed.ClearAsync(factory);
    }

    public Task DisposeAsync()
    {
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
            // A temp folder that outlives the run is not a test failure.
        }

        return Task.CompletedTask;
    }

    [Fact]
    public async Task The_detail_carries_the_summary_so_a_deep_link_needs_no_list()
    {
        var detail = await GetAsync();

        Assert.Equal("Cyberpunk 2077", detail.Summary.GameName);
        Assert.Equal(83.4, detail.Summary.AverageFps);
        Assert.NotEmpty(detail.Summary.Sparkline);
    }

    [Fact]
    public async Task The_machine_comes_from_the_capture_file()
    {
        var detail = await GetAsync();

        Assert.Equal("Ryzen 9 9950X", detail.Info.Processor);
        Assert.Equal("X870E", detail.Info.Motherboard);
        Assert.Equal("DX12", detail.Info.ApiInfo);
        Assert.Equal("ultra settings", detail.Info.Comment);
        Assert.True(detail.Info.ResizableBar);
        Assert.False(detail.Info.WinGameMode);
    }

    [Fact]
    public async Task Every_run_is_listed_with_the_index_the_analysis_takes()
    {
        var seeded = await RecordSeed.WriteAsync(factory, _root, runs: 3);

        var detail = await GetAsync(seeded.Id);

        Assert.Equal([0, 1, 2], detail.Runs.Select(run => run.Index));
        Assert.All(detail.Runs, run => Assert.Equal(RecordSeed.FrameCount, run.FrameCount));
        Assert.All(detail.Runs, run => Assert.True(run.HasPcLatency));
    }

    [Fact]
    public async Task The_chips_describe_the_machine_in_one_line_each()
    {
        var detail = await GetAsync();

        Assert.Equal("Ryzen 9 9950X", Chip(detail, ChipKeys.Processor));
        Assert.Equal("RTX 5090", Chip(detail, ChipKeys.Gpu));
        Assert.Equal("Driver 566.36", Chip(detail, ChipKeys.Driver));
        Assert.Equal("ReBAR · HAGS", Chip(detail, ChipKeys.Features));
    }

    [Fact]
    public async Task The_detail_says_where_the_capture_lives()
    {
        // The view offers to open the containing folder, so it has to know the path.
        var seeded = await RecordSeed.WriteAsync(factory, _root);

        var detail = await GetAsync(seeded.Id);

        Assert.Equal(seeded.Path, detail.Source.FilePath);
        Assert.True(detail.Source.FileSize > 0);
        Assert.NotNull(detail.Source.ModifiedUtc);
    }

    private async Task<RecordDetailDto> GetAsync(Guid? id = null)
    {
        var record = id ?? (await RecordSeed.WriteAsync(factory, _root)).Id;
        using var client = factory.CreateCaller();

        var detail = await client.GetFromJsonAsync<RecordDetailDto>($"/api/records/{record}", Json);

        Assert.NotNull(detail);

        return detail;
    }

    private static string? Chip(RecordDetailDto detail, string key) =>
        detail.Chips.FirstOrDefault(chip => chip.Key == key)?.Label;
}
