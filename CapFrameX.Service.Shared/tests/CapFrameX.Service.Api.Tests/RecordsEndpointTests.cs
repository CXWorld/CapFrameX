using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CapFrameX.Service.Contracts.Records;
using CapFrameX.Service.Data;
using CapFrameX.Service.Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CapFrameX.Service.Api.Tests;

/// <summary>
/// What the record list gets over the wire.
/// </summary>
/// <remarks>
/// Against the hosted API rather than the controller as a class: the shape the frontend binds to
/// is produced by the serializer and the route, not by the return statement, and a renamed field
/// compiles either way.
/// </remarks>
public sealed class RecordsEndpointTests(GuardedApiFactory factory) : IClassFixture<GuardedApiFactory>, IAsyncLifetime
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task InitializeAsync() => await ClearAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task An_empty_index_is_an_empty_list_rather_than_an_error()
    {
        using var client = factory.CreateCaller();

        var response = await client.GetFromJsonAsync<RecordsListResponse>("/api/records", Json);

        Assert.NotNull(response);
        Assert.Empty(response.Records);
        Assert.Equal(0, response.Total);
    }

    [Fact]
    public async Task An_indexed_capture_arrives_with_what_the_list_shows()
    {
        await SeedAsync(Record("Cyberpunk 2077", @"C:\c\cyberpunk-ultra.json"));
        using var client = factory.CreateCaller();

        var response = await client.GetFromJsonAsync<RecordsListResponse>("/api/records", Json);

        var record = Assert.Single(response!.Records);
        Assert.Equal("cyberpunk-ultra", record.Name);
        Assert.Equal("Cyberpunk 2077", record.GameName);
        Assert.Equal(120, record.DurationSeconds);
        Assert.Equal(2, record.RunCount);
        Assert.Equal([16.6, 16.7], record.Sparkline);
        Assert.True(record.HasDisplayChange);
    }

    [Fact]
    public async Task The_newest_capture_comes_first()
    {
        await SeedAsync(
            Record("Older", @"C:\c\older.json", DateTime.UtcNow.AddDays(-1)),
            Record("Newer", @"C:\c\newer.json", DateTime.UtcNow));
        using var client = factory.CreateCaller();

        var response = await client.GetFromJsonAsync<RecordsListResponse>("/api/records", Json);

        Assert.Equal(["Newer", "Older"], response!.Records.Select(record => record.GameName));
    }

    [Fact]
    public async Task A_page_says_how_many_captures_there_are_in_total()
    {
        await SeedAsync(
            Record("One", @"C:\c\one.json"),
            Record("Two", @"C:\c\two.json"),
            Record("Three", @"C:\c\three.json"));
        using var client = factory.CreateCaller();

        var response = await client.GetFromJsonAsync<RecordsListResponse>("/api/records?skip=1&take=1", Json);

        Assert.Single(response!.Records);
        Assert.Equal(3, response.Total);
    }

    [Fact]
    public async Task Searching_matches_the_game_and_the_process_whatever_the_case()
    {
        await SeedAsync(
            Record("Cyberpunk 2077", @"C:\c\one.json", process: "Cyberpunk2077"),
            Record("Baldurs Gate 3", @"C:\c\two.json", process: "bg3"));
        using var client = factory.CreateCaller();

        var byGame = await client.GetFromJsonAsync<RecordsListResponse>("/api/records?search=CYBERPUNK", Json);
        var byProcess = await client.GetFromJsonAsync<RecordsListResponse>("/api/records?search=bg3", Json);

        Assert.Equal("Cyberpunk 2077", Assert.Single(byGame!.Records).GameName);
        Assert.Equal("Baldurs Gate 3", Assert.Single(byProcess!.Records).GameName);
    }

    [Fact]
    public async Task A_search_box_character_is_not_a_wildcard()
    {
        // Typing '%' has to find nothing, not everything.
        await SeedAsync(Record("Cyberpunk 2077", @"C:\c\one.json"));
        using var client = factory.CreateCaller();

        var response = await client.GetFromJsonAsync<RecordsListResponse>("/api/records?search=%25", Json);

        Assert.Empty(response!.Records);
    }

    [Fact]
    public async Task One_capture_can_be_asked_for_by_id()
    {
        var record = Record("Cyberpunk 2077", @"C:\c\one.json");
        await SeedAsync(record);
        using var client = factory.CreateCaller();

        var summary = await client.GetFromJsonAsync<RecordSummaryDto>($"/api/records/{record.Id}", Json);

        Assert.Equal(record.Id, summary!.Id);
    }

    [Fact]
    public async Task An_unknown_id_is_a_not_found_problem()
    {
        using var client = factory.CreateCaller();

        var response = await client.GetAsync($"/api/records/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    private static Session Record(
        string game,
        string path,
        DateTime? createdAt = null,
        string process = "game") =>
        new()
        {
            Id = Guid.NewGuid(),
            GameName = game,
            ProcessName = process,
            Processor = "Ryzen 9 9950X",
            Gpu = "RTX 5090",
            Os = "Windows 11",
            CreatedAt = createdAt ?? DateTime.UtcNow,
            SourceFilePath = path,
            SourceFileSize = 1024,
            SourceModifiedUtc = DateTime.UtcNow,
            IndexVersion = 1,
            DurationSeconds = 120,
            RunCount = 2,
            FrameCount = 7200,
            SparklineJson = "[16.6,16.7]",
            HasDisplayChange = true,
        };

    private async Task SeedAsync(params Session[] sessions)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<CapFrameXDbContext>();

        var suite = new Suite
        {
            Id = Guid.NewGuid(),
            Name = "Captures",
            Type = SuiteType.Miscellaneous,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        context.Suites.Add(suite);

        foreach (var session in sessions)
        {
            session.SuiteId = suite.Id;
            context.Sessions.Add(session);
        }

        await context.SaveChangesAsync();
    }

    private async Task ClearAsync()
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<CapFrameXDbContext>();

        await context.Sessions.ExecuteDeleteAsync();
        await context.Suites.ExecuteDeleteAsync();
    }
}
