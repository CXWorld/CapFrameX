using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CapFrameX.Service.Contracts.Analysis;

namespace CapFrameX.Service.Api.Tests;

/// <summary>
/// The analysis and series endpoints over the wire.
/// </summary>
/// <remarks>
/// Written against a capture file the test wrote itself and a record that points at it, because
/// that is the whole path a chart takes: index row, file on disk, parser, statistics, JSON.
/// </remarks>
public sealed class AnalysisEndpointTests(GuardedApiFactory factory) : IClassFixture<GuardedApiFactory>, IAsyncLifetime
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly string _root = Path.Combine(Path.GetTempPath(), "cfx-analysis-" + Guid.NewGuid().ToString("N"));

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
    public async Task A_record_analyses_into_the_default_tiles()
    {
        var id = await SeedAsync();
        using var client = factory.CreateCaller();

        var analysis = await client.GetFromJsonAsync<AnalysisDto>($"/api/records/{id}/analysis", Json);

        Assert.NotNull(analysis);
        Assert.Equal(["average", "p95", "onePercentLowAverage", "zerodotOnePercentLowAverage"],
            analysis.Metrics.Select(metric => metric.Key));
        Assert.All(analysis.Metrics, metric => Assert.NotNull(metric.Value));
        Assert.Equal(RecordSeed.FrameCount, analysis.Window.FrameCount);
        Assert.NotEmpty(analysis.LShape);
        Assert.NotEmpty(analysis.Distribution);
    }

    [Fact]
    public async Task The_tiles_can_be_chosen_and_keep_their_order()
    {
        var id = await SeedAsync();
        using var client = factory.CreateCaller();

        var analysis = await client.GetFromJsonAsync<AnalysisDto>(
            $"/api/records/{id}/analysis?metrics=p1,max,average", Json);

        Assert.Equal(["p1", "max", "average"], analysis!.Metrics.Select(metric => metric.Key));
    }

    [Fact]
    public async Task A_metric_the_service_does_not_know_is_refused_with_the_list_of_those_it_does()
    {
        var id = await SeedAsync();
        using var client = factory.CreateCaller();

        var response = await client.GetAsync($"/api/records/{id}/analysis?metrics=p1,nonsense");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("p95", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task An_outlier_method_that_removes_nothing_is_refused_rather_than_accepted_silently()
    {
        // 1.x declares three more and implements none of them; offering them would put a setting
        // in the UI that does nothing.
        var id = await SeedAsync();
        using var client = factory.CreateCaller();

        var response = await client.GetAsync($"/api/records/{id}/analysis?outliers=ThreeSigma");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("DeciPercentile", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task A_run_the_capture_does_not_have_is_a_bad_request()
    {
        var id = await SeedAsync();
        using var client = factory.CreateCaller();

        var response = await client.GetAsync($"/api/records/{id}/analysis?run=7");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task An_unknown_record_is_not_found()
    {
        using var client = factory.CreateCaller();

        var response = await client.GetAsync($"/api/records/{Guid.NewGuid()}/analysis");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task A_record_whose_file_is_gone_says_so_rather_than_answering_with_nothing()
    {
        // The folder changed underneath the index; the indexer will catch up, and until it does
        // the caller gets the reason instead of an empty chart.
        var id = await SeedAsync(deleteFile: true);
        using var client = factory.CreateCaller();

        var response = await client.GetAsync($"/api/records/{id}/analysis");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task The_series_returns_the_frame_times_against_time()
    {
        var id = await SeedAsync();
        using var client = factory.CreateCaller();

        var series = await client.GetFromJsonAsync<SeriesResponse>($"/api/records/{id}/series", Json);

        Assert.NotNull(series);
        Assert.Equal(RecordSeed.FrameCount, series.Time.Length);
        Assert.Equal(RecordSeed.FrameCount, series.Frametimes!.Length);
        Assert.Null(series.Fps);
    }

    [Fact]
    public async Task The_series_returns_the_curves_it_was_asked_for()
    {
        var id = await SeedAsync();
        using var client = factory.CreateCaller();

        var series = await client.GetFromJsonAsync<SeriesResponse>(
            $"/api/records/{id}/series?kinds=fps,pclatency&start=0&end=1", Json);

        Assert.NotNull(series);
        Assert.NotNull(series.Fps);
        Assert.NotNull(series.PcLatency);
        Assert.Null(series.Frametimes);
        Assert.All(series.Time, time => Assert.InRange(time, 0, 1));
    }

    [Fact]
    public async Task A_curve_the_service_does_not_have_is_refused()
    {
        var id = await SeedAsync();
        using var client = factory.CreateCaller();

        var response = await client.GetAsync($"/api/records/{id}/series?kinds=frametimes,voltage");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task A_capture_read_once_is_not_read_again()
    {
        // Deleting the file after the first call is the only way to see the cache from outside,
        // and it is exactly what the cache is for: a range drag must not go back to the disk.
        var seeded = await RecordSeed.WriteAsync(factory, _root);
        var id = seeded.Id;
        using var client = factory.CreateCaller();

        Assert.True((await client.GetAsync($"/api/records/{id}/analysis")).IsSuccessStatusCode);
        File.Delete(seeded.Path);

        var second = await client.GetAsync($"/api/records/{id}/analysis?start=0.1&end=0.5");

        Assert.True(second.IsSuccessStatusCode);
    }

    private async Task<Guid> SeedAsync(bool deleteFile = false) =>
        (await RecordSeed.WriteAsync(factory, _root, deleteFile: deleteFile)).Id;

}
