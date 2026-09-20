using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CapFrameX.Service.Api.Services;
using CapFrameX.Service.Application.Records;
using CapFrameX.Service.Application.Settings;
using CapFrameX.Service.Contracts.Analysis;
using CapFrameX.Service.Contracts.Bridge;
using CapFrameX.Service.Contracts.Settings;
using Microsoft.Extensions.DependencyInjection;

namespace CapFrameX.Service.Api.Tests;

/// <summary>
/// Reading and changing the settings.
/// </summary>
/// <remarks>
/// The interesting part is not that a value comes back, but that changing it does something: the
/// analysis has to answer differently on the next request, the indexer has to look somewhere else,
/// and everyone who did not make the change has to hear about it.
/// </remarks>
public sealed class SettingsEndpointTests(GuardedApiFactory factory)
    : IClassFixture<GuardedApiFactory>, IAsyncLifetime
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly string _root = Path.Combine(Path.GetTempPath(), "cfx-settings-" + Guid.NewGuid().ToString("N"));

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(_root);
        await RecordSeed.ClearAsync(factory);
        await ResetAsync();
    }

    public async Task DisposeAsync()
    {
        await ResetAsync();

        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
            // A temp folder that outlives the run is not a test failure.
        }
    }

    [Fact]
    public async Task The_settings_start_at_what_CapFrameX_has_always_used()
    {
        // So a record analysed here and in the desktop app gives the same numbers until the user
        // says otherwise.
        var settings = await GetAsync();

        Assert.Equal(2.5, settings.Analysis.StutteringFactor);
        Assert.Equal(25d, settings.Analysis.StutteringThreshold);
        Assert.Equal(1, settings.Analysis.FpsValuesRoundingDigits);
        Assert.Equal("None", settings.Analysis.OutlierMethod);
        Assert.Equal("system", settings.Appearance.Theme);
    }

    [Fact]
    public async Task The_tile_row_starts_as_the_one_the_analysis_view_opens_with()
    {
        var settings = await GetAsync();

        Assert.Equal(
            ["average", "p95", "onePercentLowAverage", "zerodotOnePercentLowAverage"],
            settings.Analysis.Metrics);
    }

    [Fact]
    public async Task A_setting_can_be_changed_and_comes_back_changed()
    {
        var settings = await PatchAsync(new AppSettingsPatch(
            Appearance: new AppearanceSettingsPatch("dark")));

        Assert.Equal("dark", settings.Appearance.Theme);
        Assert.Equal("dark", (await GetAsync()).Appearance.Theme);
    }

    [Fact]
    public async Task A_change_is_written_where_it_survives_a_restart()
    {
        await PatchAsync(new AppSettingsPatch(Appearance: new AppearanceSettingsPatch("light")));

        var path = factory.Services.GetRequiredService<SettingsStore>().Path;
        Assert.Contains("light", await File.ReadAllTextAsync(path), StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_patch_that_names_one_section_leaves_the_others_alone()
    {
        await PatchAsync(new AppSettingsPatch(Appearance: new AppearanceSettingsPatch("dark")));

        var settings = await PatchAsync(new AppSettingsPatch(
            Analysis: new AnalysisSettingsPatch(StutteringFactor: 3)));

        Assert.Equal(3, settings.Analysis.StutteringFactor);
        Assert.Equal("dark", settings.Appearance.Theme);
    }

    [Fact]
    public async Task A_value_the_service_cannot_use_is_refused_and_nothing_changes()
    {
        using var client = factory.CreateCaller();

        var response = await client.PatchAsJsonAsync(
            "/api/settings",
            new AppSettingsPatch(
                Appearance: new AppearanceSettingsPatch("dark"),
                Analysis: new AnalysisSettingsPatch(FpsValuesRoundingDigits: 99)),
            Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("system", (await GetAsync()).Appearance.Theme);
    }

    [Fact]
    public async Task Everyone_else_is_told_that_the_settings_changed()
    {
        // A second window has the settings open too, and nobody wants to press refresh.
        await using var subscription = factory.Services.GetRequiredService<BridgeEventStream>().Subscribe();

        await PatchAsync(new AppSettingsPatch(Appearance: new AppearanceSettingsPatch("dark")));

        var announced = await NextAsync(subscription, BridgeEventTypes.SettingsChanged);
        var settings = JsonSerializer.Deserialize<AppSettingsDto>(
            JsonSerializer.Serialize(announced.Payload, Json), Json);

        Assert.Equal("dark", settings!.Appearance.Theme);
    }

    [Fact]
    public async Task The_analysis_uses_the_tiles_that_were_configured()
    {
        // Without the frontend repeating them on every request.
        var seeded = await RecordSeed.WriteAsync(factory, _root);
        await PatchAsync(new AppSettingsPatch(Analysis: new AnalysisSettingsPatch(Metrics: ["p1", "max"])));

        using var client = factory.CreateCaller();
        var analysis = await client.GetFromJsonAsync<AnalysisDto>($"/api/records/{seeded.Id}/analysis", Json);

        Assert.Equal(["p1", "max"], analysis!.Metrics.Select(metric => metric.Key));
    }

    [Fact]
    public async Task A_request_that_names_its_own_tiles_still_gets_them()
    {
        var seeded = await RecordSeed.WriteAsync(factory, _root);
        await PatchAsync(new AppSettingsPatch(Analysis: new AnalysisSettingsPatch(Metrics: ["p1", "max"])));

        using var client = factory.CreateCaller();
        var analysis = await client.GetFromJsonAsync<AnalysisDto>(
            $"/api/records/{seeded.Id}/analysis?metrics=median", Json);

        Assert.Equal(["median"], analysis!.Metrics.Select(metric => metric.Key));
    }

    [Fact]
    public async Task Changing_the_thresholds_changes_the_next_analysis()
    {
        // The settings view promises that the open analysis recomputes, so the options cannot be
        // read once at start-up.
        var seeded = await RecordSeed.WriteAsync(factory, _root);
        using var client = factory.CreateCaller();

        var before = await client.GetFromJsonAsync<AnalysisDto>($"/api/records/{seeded.Id}/analysis", Json);

        await PatchAsync(new AppSettingsPatch(Analysis: new AnalysisSettingsPatch(
            StutteringFactor: 1.05,
            StutteringThreshold: 500)));

        var after = await client.GetFromJsonAsync<AnalysisDto>($"/api/records/{seeded.Id}/analysis", Json);

        Assert.Equal(2.5, before!.Thresholds.StutteringFactor);
        Assert.Equal(1.05, after!.Thresholds.StutteringFactor);
        Assert.NotEqual(before.FramePacing.SmoothPercent, after.FramePacing.SmoothPercent);
    }

    [Fact]
    public async Task Changing_the_rounding_changes_the_numbers()
    {
        var seeded = await RecordSeed.WriteAsync(factory, _root);
        using var client = factory.CreateCaller();

        var before = await client.GetFromJsonAsync<AnalysisDto>($"/api/records/{seeded.Id}/analysis", Json);
        await PatchAsync(new AppSettingsPatch(Analysis: new AnalysisSettingsPatch(FpsValuesRoundingDigits: 4)));
        var after = await client.GetFromJsonAsync<AnalysisDto>($"/api/records/{seeded.Id}/analysis", Json);

        var average = before!.Metrics.Single(metric => metric.Key == "average").Value;
        var precise = after!.Metrics.Single(metric => metric.Key == "average").Value;

        Assert.Equal(average, Math.Round(precise!.Value, 1));
        Assert.NotEqual(average, precise);
    }

    [Fact]
    public async Task Rounding_differently_marks_the_records_for_re_reading()
    {
        // The list shows numbers the index computed once; without this it would disagree with the
        // record it opens in the last decimal place.
        var seeded = await RecordSeed.WriteAsync(factory, _root);

        await PatchAsync(new AppSettingsPatch(Analysis: new AnalysisSettingsPatch(FpsValuesRoundingDigits: 3)));

        await using var scope = factory.Services.GetRequiredService<IServiceScopeFactory>().CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<Data.CapFrameXDbContext>();
        var record = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions
            .FirstAsync(context.Sessions, session => session.Id == seeded.Id);

        Assert.Equal(0, record.IndexVersion);
    }

    [Fact]
    public async Task Pointing_the_service_at_another_folder_moves_the_index_with_it()
    {
        var elsewhere = Path.Combine(_root, "Elsewhere");

        var settings = await PatchAsync(new AppSettingsPatch(Paths: new PathSettingsPatch(elsewhere)));

        Assert.Equal(elsewhere, settings.Paths.CaptureDirectory);
        Assert.Equal(elsewhere, factory.Services.GetRequiredService<RecordIndexOptions>().CaptureDirectory);
        Assert.True(Directory.Exists(elsewhere));
    }

    private static async Task<BridgeEventEnvelope> NextAsync(BridgeEventSubscription subscription, string type)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        while (await subscription.Reader.WaitToReadAsync(timeout.Token))
        {
            while (subscription.Reader.TryRead(out var envelope))
            {
                if (envelope.Type == type)
                {
                    return envelope;
                }
            }
        }

        throw new InvalidOperationException($"No '{type}' event arrived.");
    }

    private async Task<AppSettingsDto> GetAsync()
    {
        using var client = factory.CreateCaller();

        var settings = await client.GetFromJsonAsync<AppSettingsDto>("/api/settings", Json);

        Assert.NotNull(settings);

        return settings;
    }

    private async Task<AppSettingsDto> PatchAsync(AppSettingsPatch patch)
    {
        using var client = factory.CreateCaller();

        var response = await client.PatchAsJsonAsync("/api/settings", patch, Json);

        response.EnsureSuccessStatusCode();

        var settings = await response.Content.ReadFromJsonAsync<AppSettingsDto>(Json);
        Assert.NotNull(settings);

        return settings;
    }

    private async Task ResetAsync()
    {
        using var client = factory.CreateCaller();

        await client.PatchAsJsonAsync(
            "/api/settings",
            new AppSettingsPatch(
                Analysis: new AnalysisSettingsPatch(
                    StutteringFactor: 2.5,
                    StutteringThreshold: 25,
                    FpsValuesRoundingDigits: 1,
                    OutlierMethod: "None",
                    Metrics: [],
                    LShapeMetric: "Frametimes"),
                Paths: new PathSettingsPatch(string.Empty),
                Appearance: new AppearanceSettingsPatch("system")),
            Json);
    }
}
