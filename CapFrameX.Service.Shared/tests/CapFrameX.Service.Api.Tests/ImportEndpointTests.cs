using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CapFrameX.Data.Session.Classes;
using CapFrameX.Service.Contracts.Analysis;
using CapFrameX.Service.Contracts.Records;
using CapFrameX.Service.Contracts.Settings;
using Newtonsoft.Json;
using LegacyRun = CapFrameX.Data.Session.Classes.SessionRun;
using LegacySession = CapFrameX.Data.Session.Classes.Session;

namespace CapFrameX.Service.Api.Tests;

/// <summary>
/// Importing captures over the wire.
/// </summary>
/// <remarks>
/// The claim an import makes is that the record no longer needs the file, so the tests delete it
/// afterwards and open the record anyway. That is the whole difference from indexing a folder.
/// </remarks>
public sealed class ImportEndpointTests(GuardedApiFactory factory)
    : IClassFixture<GuardedApiFactory>, IAsyncLifetime
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly string _source = Path.Combine(Path.GetTempPath(), "cfx-imp-" + Guid.NewGuid().ToString("N"));

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(_source);
        await RecordSeed.ClearAsync(factory);

        // The settings outlive a test - the host is shared by the whole class - and whether the
        // first import was offered is exactly what one of these tests is about.
        using var client = factory.CreateCaller();
        await client.PatchAsJsonAsync(
            "/api/settings",
            new AppSettingsPatch(Import: new ImportSettingsPatch(Offered: false)),
            Json);
    }

    public Task DisposeAsync()
    {
        try
        {
            Directory.Delete(_source, recursive: true);
        }
        catch (IOException)
        {
            // A temp folder that outlives the run is not a test failure.
        }

        return Task.CompletedTask;
    }

    [Fact]
    public async Task A_folder_of_captures_becomes_records()
    {
        Write("one", "Cyberpunk 2077");
        Write("two", "Baldurs Gate 3");

        var result = await ImportAsync(_source);

        Assert.Equal(2, result.Imported);
        Assert.Equal(2, result.Total);

        using var client = factory.CreateCaller();
        var list = await client.GetFromJsonAsync<RecordsListResponse>("/api/records", Json);
        Assert.Equal(2, list!.Total);
    }

    [Fact]
    public async Task A_single_file_can_be_imported()
    {
        var path = Write("one", "Cyberpunk 2077");

        Assert.Equal(1, (await ImportAsync(path)).Imported);
    }

    [Fact]
    public async Task An_imported_record_opens_after_its_file_is_deleted()
    {
        // The point of importing: the record carries the capture rather than pointing at it.
        var path = Write("one", "Cyberpunk 2077");
        await ImportAsync(_source);
        File.Delete(path);

        using var client = factory.CreateCaller();
        var list = await client.GetFromJsonAsync<RecordsListResponse>("/api/records", Json);
        var id = Assert.Single(list!.Records).Id;

        var analysis = await client.GetFromJsonAsync<AnalysisDto>($"/api/records/{id}/analysis", Json);

        Assert.Equal(RecordSeed.FrameCount, analysis!.Window.FrameCount);
        Assert.All(analysis.Metrics, metric => Assert.NotNull(metric.Value));
    }

    [Fact]
    public async Task An_imported_record_charts_after_its_file_is_deleted()
    {
        var path = Write("one", "Cyberpunk 2077");
        await ImportAsync(_source);
        File.Delete(path);

        using var client = factory.CreateCaller();
        var list = await client.GetFromJsonAsync<RecordsListResponse>("/api/records", Json);
        var id = Assert.Single(list!.Records).Id;

        var series = await client.GetFromJsonAsync<SeriesResponse>(
            $"/api/records/{id}/series?kinds=frametimes,pclatency", Json);

        Assert.Equal(RecordSeed.FrameCount, series!.Time.Length);
        Assert.Equal(RecordSeed.FrameCount, series.PcLatency!.Length);
    }

    [Fact]
    public async Task An_imported_record_describes_itself_without_its_file()
    {
        var path = Write("one", "Cyberpunk 2077");
        await ImportAsync(_source);
        File.Delete(path);

        using var client = factory.CreateCaller();
        var list = await client.GetFromJsonAsync<RecordsListResponse>("/api/records", Json);
        var id = Assert.Single(list!.Records).Id;

        var detail = await client.GetFromJsonAsync<RecordDetailDto>($"/api/records/{id}", Json);

        Assert.Equal("Cyberpunk 2077", detail!.Info.GameName);
        Assert.Equal("Ryzen 9 9950X", detail.Info.Processor);
        Assert.Equal(path, detail.Source.FilePath);
        Assert.Single(detail.Runs);
    }

    [Fact]
    public async Task The_runs_of_an_imported_capture_keep_the_order_they_were_recorded_in()
    {
        // The database returns rows in whatever order suits it, and the statistics read the runs
        // as one sequence - several metrics give a different answer when the frames arrive in a
        // different order. The two runs here are different lengths, so a swap is visible.
        Write("two-runs", "Cyberpunk 2077", [120, 240]);
        await ImportAsync(_source);

        using var client = factory.CreateCaller();
        var list = await client.GetFromJsonAsync<RecordsListResponse>("/api/records", Json);
        var id = Assert.Single(list!.Records).Id;

        var first = await client.GetFromJsonAsync<AnalysisDto>($"/api/records/{id}/analysis?run=0", Json);
        var second = await client.GetFromJsonAsync<AnalysisDto>($"/api/records/{id}/analysis?run=1", Json);

        Assert.Equal(120, first!.Window.FrameCount);
        Assert.Equal(240, second!.Window.FrameCount);
    }

    [Fact]
    public async Task Importing_the_same_folder_again_brings_nothing_new()
    {
        Write("one", "Cyberpunk 2077");
        await ImportAsync(_source);

        var second = await ImportAsync(_source);

        Assert.Equal(0, second.Imported);
        Assert.Equal(1, second.AlreadyKnown);
    }

    [Fact]
    public async Task An_unreadable_file_costs_one_file_rather_than_the_import()
    {
        Write("one", "Cyberpunk 2077");
        await File.WriteAllTextAsync(Path.Combine(_source, "notes.json"), "{ \"not\": \"a capture\" }");

        var result = await ImportAsync(_source);

        Assert.Equal(1, result.Imported);
        Assert.Equal(1, result.Failed);
        Assert.NotEmpty(result.Errors);
    }

    [Fact]
    public async Task A_relative_path_is_refused()
    {
        // It would resolve against the service's working directory, which is not a place the user
        // can see or reason about.
        using var client = factory.CreateCaller();

        var response = await client.PostAsJsonAsync("/api/records/import", new ImportRequest("Captures"), Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task A_folder_that_is_not_there_says_so()
    {
        using var client = factory.CreateCaller();

        var response = await client.PostAsJsonAsync(
            "/api/records/import",
            new ImportRequest(Path.Combine(_source, "no-such-folder")),
            Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task The_first_import_is_offered_once()
    {
        // Somebody who imported has answered the question; asking again every start would be rude.
        using var client = factory.CreateCaller();

        var before = await client.GetFromJsonAsync<ImportSourcesResponse>("/api/records/import/sources", Json);
        Write("one", "Cyberpunk 2077");
        await ImportAsync(_source);
        var after = await client.GetFromJsonAsync<ImportSourcesResponse>("/api/records/import/sources", Json);

        Assert.False(before!.Offered);
        Assert.True(after!.Offered);
        Assert.True((await client.GetFromJsonAsync<AppSettingsDto>("/api/settings", Json))!.Import.Offered);
    }

    [Fact]
    public async Task Saying_no_to_the_first_import_is_remembered_too()
    {
        using var client = factory.CreateCaller();

        await client.PatchAsJsonAsync(
            "/api/settings",
            new AppSettingsPatch(Import: new ImportSettingsPatch(Offered: true)),
            Json);

        var sources = await client.GetFromJsonAsync<ImportSourcesResponse>("/api/records/import/sources", Json);

        Assert.True(sources!.Offered);
    }

    [Fact]
    public async Task A_suggested_folder_is_never_the_one_the_service_already_watches()
    {
        // Importing it would bring nothing and only raise the question of why.
        using var client = factory.CreateCaller();

        var sources = await client.GetFromJsonAsync<ImportSourcesResponse>("/api/records/import/sources", Json);

        Assert.DoesNotContain(sources!.Sources, source => source.Path.Contains("Captures", StringComparison.Ordinal)
            && source.CaptureCount == 0);
    }

    private async Task<ImportResultDto> ImportAsync(string path)
    {
        using var client = factory.CreateCaller();

        var response = await client.PostAsJsonAsync("/api/records/import", new ImportRequest(path), Json);

        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<ImportResultDto>(Json);
        Assert.NotNull(result);

        return result;
    }

    private string Write(string name, string game, int[]? runFrames = null)
    {
        var session = new LegacySession
        {
            Hash = Guid.NewGuid().ToString("N"),
            Info = new SessionInfo
            {
                Id = Guid.NewGuid(),
                GameName = game,
                ProcessName = "game.exe",
                Processor = "Ryzen 9 9950X",
                GPU = "RTX 5090",
                OS = "Windows 11",
                CreationDate = new DateTime(2026, 9, 20, 18, 10, 0, DateTimeKind.Utc),
            },
            Runs = [.. (runFrames ?? [RecordSeed.FrameCount]).Select(Run)],
        };

        var path = Path.Combine(_source, name + ".json");
        File.WriteAllText(path, JsonConvert.SerializeObject(session));

        return path;
    }

    private static LegacyRun Run(int frames)
    {
        var random = new Random(frames);
        var frametimes = new double[frames];
        var times = new double[frames];
        var latency = new double[frames];
        var elapsed = 0d;

        for (var i = 0; i < frames; i++)
        {
            frametimes[i] = 10d + random.NextDouble() * 15d;
            times[i] = elapsed;
            latency[i] = 25d + random.NextDouble() * 10d;
            elapsed += frametimes[i] / 1000d;
        }

        return new LegacyRun
        {
            PresentMonRuntime = "DXGI",
            SampleTime = (int)Math.Ceiling(elapsed),
            CaptureData = new SessionCaptureData(frames)
            {
                TimeInSeconds = times,
                MsBetweenPresents = frametimes,
                PcLatency = latency,
            },
        };
    }
}
