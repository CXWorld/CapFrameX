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
/// Narrowing and ordering the record list.
/// </summary>
/// <remarks>
/// Over the wire and against real SQLite, because that is where the interesting answers come from:
/// how the database collates a string, where it puts a null, and whether a page boundary holds.
/// </remarks>
public sealed class RecordListFilterTests(GuardedApiFactory factory)
    : IClassFixture<GuardedApiFactory>, IAsyncLifetime
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static readonly DateTime Monday = new(2026, 9, 14, 12, 0, 0, DateTimeKind.Utc);

    public Task InitializeAsync() => RecordSeed.ClearAsync(factory);

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task One_game_can_be_picked_out()
    {
        await SeedAsync(
            Row("Cyberpunk 2077", Monday),
            Row("Cyberpunk 2077", Monday.AddDays(1)),
            Row("Baldurs Gate 3", Monday.AddDays(2)));

        var page = await ListAsync("?game=Cyberpunk 2077");

        Assert.Equal(2, page.Total);
        Assert.All(page.Records, record => Assert.Equal("Cyberpunk 2077", record.GameName));
    }

    [Fact]
    public async Task Picking_a_game_matches_it_whole_rather_than_partly()
    {
        // The chip comes from the list of games that exist, so "Portal" must not drag in
        // "Portal 2" - that is what the search box is for.
        await SeedAsync(Row("Portal", Monday), Row("Portal 2", Monday.AddDays(1)));

        var page = await ListAsync("?game=Portal");

        Assert.Equal("Portal", Assert.Single(page.Records).GameName);
    }

    [Fact]
    public async Task Picking_a_game_ignores_the_casing_the_capture_recorded()
    {
        await SeedAsync(Row("Cyberpunk 2077", Monday));

        Assert.Single((await ListAsync("?game=cyberpunk 2077")).Records);
    }

    [Fact]
    public async Task A_date_range_keeps_what_falls_inside_it()
    {
        await SeedAsync(
            Row("Before", Monday.AddDays(-1)),
            Row("Inside", Monday),
            Row("After", Monday.AddDays(1)));

        var page = await ListAsync($"?from={Iso(Monday.AddHours(-1))}&to={Iso(Monday.AddHours(1))}");

        Assert.Equal("Inside", Assert.Single(page.Records).GameName);
    }

    [Fact]
    public async Task The_end_of_a_date_range_is_included()
    {
        // A capture taken at the exact second the user asked up to is inside the range they asked
        // for.
        await SeedAsync(Row("Exactly", Monday));

        Assert.Single((await ListAsync($"?to={Iso(Monday)}")).Records);
    }

    [Fact]
    public async Task Half_a_range_is_a_range()
    {
        await SeedAsync(Row("Older", Monday), Row("Newer", Monday.AddDays(2)));

        Assert.Equal("Newer", Assert.Single((await ListAsync($"?from={Iso(Monday.AddDays(1))}")).Records).GameName);
        Assert.Equal("Older", Assert.Single((await ListAsync($"?to={Iso(Monday.AddDays(1))}")).Records).GameName);
    }

    [Fact]
    public async Task Filters_narrow_each_other()
    {
        await SeedAsync(
            Row("Cyberpunk 2077", Monday),
            Row("Cyberpunk 2077", Monday.AddDays(5)),
            Row("Baldurs Gate 3", Monday));

        var page = await ListAsync($"?game=Cyberpunk 2077&to={Iso(Monday.AddDays(1))}");

        Assert.Equal(1, page.Total);
    }

    [Fact]
    public async Task The_total_counts_what_matched_rather_than_what_fits_on_the_page()
    {
        await SeedAsync(
            Row("Cyberpunk 2077", Monday),
            Row("Cyberpunk 2077", Monday.AddDays(1)),
            Row("Baldurs Gate 3", Monday));

        var page = await ListAsync("?game=Cyberpunk 2077&take=1");

        Assert.Single(page.Records);
        Assert.Equal(2, page.Total);
    }

    [Fact]
    public async Task Newest_first_is_what_the_list_opens_with()
    {
        await SeedAsync(Row("Older", Monday), Row("Newer", Monday.AddDays(1)));

        Assert.Equal(["Newer", "Older"], (await ListAsync(string.Empty)).Records.Select(r => r.GameName));
    }

    [Theory]
    [InlineData("created", "Older")]
    [InlineData("-created", "Newer")]
    [InlineData("game", "Newer")]
    [InlineData("-game", "Older")]
    public async Task A_sort_orders_the_list_and_a_minus_turns_it_round(string sort, string first)
    {
        // "Newer" sorts before "Older" alphabetically and after it by date, so one seeding covers
        // both directions of both fields.
        await SeedAsync(Row("Older", Monday), Row("Newer", Monday.AddDays(1)));

        var page = await ListAsync($"?sort={sort}");

        Assert.Equal(first, page.Records[0].GameName);
    }

    [Fact]
    public async Task Sorting_by_a_metric_puts_the_fastest_first()
    {
        await SeedAsync(
            Row("Slow", Monday, averageFps: 45),
            Row("Fast", Monday.AddDays(1), averageFps: 144));

        Assert.Equal("Fast", (await ListAsync("?sort=-averagefps")).Records[0].GameName);
        Assert.Equal("Slow", (await ListAsync("?sort=averagefps")).Records[0].GameName);
    }

    [Fact]
    public async Task A_record_with_no_metric_sorts_last_whichever_way_round()
    {
        // "Not measured" is not "slowest": a record the index has not caught up with must not head
        // a list sorted by frame rate.
        await SeedAsync(
            Row("Measured", Monday, averageFps: 90),
            Row("Unmeasured", Monday.AddDays(1), averageFps: null));

        Assert.Equal("Unmeasured", (await ListAsync("?sort=averagefps")).Records[^1].GameName);
        Assert.Equal("Unmeasured", (await ListAsync("?sort=-averagefps")).Records[^1].GameName);
    }

    [Fact]
    public async Task Records_taken_at_the_same_moment_are_ordered_by_identity()
    {
        // Captures from one benchmark run share a timestamp, and a sort on that alone leaves their
        // order to the database. It may answer differently for two pages of the same list, which
        // shows up as one record appearing twice and another never - so the order ends in
        // something no two records share.
        //
        // This pins that identity is the last key, not that the list would be wrong without it:
        // SQLite happens to return these rows in the same order either way. Reversing the
        // tiebreaker does fail here, so the clause is reaching the database.
        await SeedAsync([.. Enumerable.Range(0, 10).Select(_ => Row("Same", Monday))]);

        var page = await ListAsync(string.Empty);
        var identities = page.Records.Select(record => record.Id).ToArray();

        Assert.Equal(identities.OrderByDescending(id => id), identities);
    }

    [Fact]
    public async Task Two_pages_of_one_list_cover_it_once()
    {
        await SeedAsync([.. Enumerable.Range(0, 10).Select(_ => Row("Same", Monday))]);

        var first = await ListAsync("?take=5");
        var second = await ListAsync("?skip=5&take=5");

        var seen = first.Records.Concat(second.Records).Select(record => record.Id).ToArray();
        Assert.Equal(10, seen.Distinct().Count());
    }

    [Fact]
    public async Task A_sort_the_service_does_not_know_is_refused_with_the_ones_it_does()
    {
        using var client = factory.CreateCaller();

        var response = await client.GetAsync("/api/records?sort=loudness");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("averagefps", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_games_that_exist_can_be_asked_for()
    {
        // The filter chips are built from this, so it has to be the games the index actually holds.
        await SeedAsync(
            Row("Cyberpunk 2077", Monday),
            Row("Cyberpunk 2077", Monday.AddDays(1)),
            Row("Baldurs Gate 3", Monday));

        using var client = factory.CreateCaller();

        var games = await client.GetFromJsonAsync<string[]>("/api/records/games", Json);

        Assert.Equal(["Baldurs Gate 3", "Cyberpunk 2077"], games);
    }

    private static string Iso(DateTime moment) =>
        Uri.EscapeDataString(new DateTimeOffset(moment).ToString("O"));

    private static Session Row(string game, DateTime createdAt, double? averageFps = 83.4) =>
        new()
        {
            Id = Guid.NewGuid(),
            GameName = game,
            ProcessName = "game.exe",
            Processor = "Ryzen 9 9950X",
            Gpu = "RTX 5090",
            Os = "Windows 11",
            CreatedAt = createdAt,
            SourceFilePath = Path.Combine(@"C:\c", Guid.NewGuid().ToString("N") + ".json"),
            SourceFileSize = 1024,
            SourceModifiedUtc = createdAt,
            IndexVersion = 2,
            DurationSeconds = 20,
            RunCount = 1,
            FrameCount = 1200,
            AverageFps = averageFps,
        };

    private async Task<RecordsListResponse> ListAsync(string query)
    {
        using var client = factory.CreateCaller();

        var page = await client.GetFromJsonAsync<RecordsListResponse>("/api/records" + query, Json);

        Assert.NotNull(page);

        return page;
    }

    private async Task SeedAsync(params Session[] records)
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

        foreach (var record in records)
        {
            record.SuiteId = suite.Id;
            context.Sessions.Add(record);
        }

        await context.SaveChangesAsync();
    }
}
