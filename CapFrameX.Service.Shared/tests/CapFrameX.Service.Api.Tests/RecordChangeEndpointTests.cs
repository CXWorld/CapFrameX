using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CapFrameX.Service.Contracts.Records;
using CapFrameX.Service.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CapFrameX.Service.Api.Tests;

/// <summary>
/// Editing and removing a record.
/// </summary>
/// <remarks>
/// Both touch a file the user cannot get back, so the assertions are as much about what stays as
/// about what changes: the fields nobody sent, the capture still being readable afterwards, and
/// nothing ever being unlinked outright.
/// </remarks>
public sealed class RecordChangeEndpointTests(GuardedApiFactory factory)
    : IClassFixture<GuardedApiFactory>, IAsyncLifetime
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly string _root = Path.Combine(Path.GetTempPath(), "cfx-change-" + Guid.NewGuid().ToString("N"));

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(_root);
        factory.Trash.Refuse = false;
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
    public async Task A_comment_can_be_corrected()
    {
        var seeded = await RecordSeed.WriteAsync(factory, _root);
        using var client = factory.CreateCaller();

        var response = await client.PatchAsJsonAsync(
            $"/api/records/{seeded.Id}",
            new RecordEdit(Comment: "second run, driver 566.36"),
            Json);

        response.EnsureSuccessStatusCode();
        var detail = await response.Content.ReadFromJsonAsync<RecordDetailDto>(Json);
        Assert.Equal("second run, driver 566.36", detail!.Info.Comment);
    }

    [Fact]
    public async Task The_correction_lands_in_the_capture_file()
    {
        // Not beside it: the change has to survive being copied to another machine, and 1.x has to
        // read it.
        var seeded = await RecordSeed.WriteAsync(factory, _root);
        using var client = factory.CreateCaller();

        await client.PatchAsJsonAsync($"/api/records/{seeded.Id}", new RecordEdit(GameName: "Renamed"), Json);

        Assert.Contains("Renamed", await File.ReadAllTextAsync(seeded.Path), StringComparison.Ordinal);
    }

    [Fact]
    public async Task What_was_not_sent_is_left_alone()
    {
        var seeded = await RecordSeed.WriteAsync(factory, _root);
        using var client = factory.CreateCaller();

        var response = await client.PatchAsJsonAsync(
            $"/api/records/{seeded.Id}",
            new RecordEdit(Comment: "only the comment"),
            Json);

        var detail = await response.Content.ReadFromJsonAsync<RecordDetailDto>(Json);
        Assert.Equal("Ryzen 9 9950X", detail!.Info.Processor);
        Assert.Equal("Cyberpunk 2077", detail.Info.GameName);
        Assert.Equal("X870E", detail.Info.Motherboard);
    }

    [Fact]
    public async Task The_index_answers_with_the_correction_at_once()
    {
        // Without waiting for the folder watcher, which settles seconds later.
        var seeded = await RecordSeed.WriteAsync(factory, _root);
        using var client = factory.CreateCaller();

        await client.PatchAsJsonAsync($"/api/records/{seeded.Id}", new RecordEdit(GameName: "Renamed"), Json);

        var list = await client.GetFromJsonAsync<RecordsListResponse>("/api/records", Json);
        Assert.Equal("Renamed", Assert.Single(list!.Records).GameName);
    }

    [Fact]
    public async Task The_capture_still_reads_after_being_edited()
    {
        // The whole file is rewritten, so the frames have to come through it unharmed.
        var seeded = await RecordSeed.WriteAsync(factory, _root);
        using var client = factory.CreateCaller();

        await client.PatchAsJsonAsync($"/api/records/{seeded.Id}", new RecordEdit(Comment: "edited"), Json);

        var series = await client.GetFromJsonAsync<SeriesResponseShape>(
            $"/api/records/{seeded.Id}/series", Json);
        Assert.Equal(RecordSeed.FrameCount, series!.Time.Length);
    }

    [Fact]
    public async Task A_patch_that_asks_for_nothing_changes_nothing()
    {
        var seeded = await RecordSeed.WriteAsync(factory, _root);
        var before = File.GetLastWriteTimeUtc(seeded.Path);
        using var client = factory.CreateCaller();

        var response = await client.PatchAsJsonAsync($"/api/records/{seeded.Id}", new RecordEdit(), Json);

        response.EnsureSuccessStatusCode();
        Assert.Equal(before, File.GetLastWriteTimeUtc(seeded.Path));
    }

    [Fact]
    public async Task Editing_a_record_nobody_indexed_is_not_found()
    {
        using var client = factory.CreateCaller();

        var response = await client.PatchAsJsonAsync(
            $"/api/records/{Guid.NewGuid()}",
            new RecordEdit(Comment: "x"),
            Json);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Editing_a_record_whose_file_is_gone_says_so()
    {
        var seeded = await RecordSeed.WriteAsync(factory, _root, deleteFile: true);
        using var client = factory.CreateCaller();

        var response = await client.PatchAsJsonAsync($"/api/records/{seeded.Id}", new RecordEdit(Comment: "x"), Json);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Deleting_moves_the_capture_to_the_trash()
    {
        // Never an unlink: a record is hours of benchmarking that cannot be recaptured.
        var seeded = await RecordSeed.WriteAsync(factory, _root);
        using var client = factory.CreateCaller();

        var response = await client.DeleteAsync($"/api/records/{seeded.Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Contains(seeded.Path, factory.Trash.Moved);
        Assert.False(File.Exists(seeded.Path));
        Assert.True(File.Exists(Path.Combine(factory.Trash.Directory, Path.GetFileName(seeded.Path))));
    }

    [Fact]
    public async Task A_deleted_record_leaves_the_list()
    {
        var seeded = await RecordSeed.WriteAsync(factory, _root);
        using var client = factory.CreateCaller();

        await client.DeleteAsync($"/api/records/{seeded.Id}");

        var list = await client.GetFromJsonAsync<RecordsListResponse>("/api/records", Json);
        Assert.Empty(list!.Records);
    }

    [Fact]
    public async Task A_capture_that_refuses_to_move_keeps_its_record()
    {
        // A row removed while its file is still in the folder would come straight back on the next
        // scan, under a new identity and without its history.
        var seeded = await RecordSeed.WriteAsync(factory, _root);
        factory.Trash.Refuse = true;
        using var client = factory.CreateCaller();

        var response = await client.DeleteAsync($"/api/records/{seeded.Id}");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.True(File.Exists(seeded.Path));
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<CapFrameXDbContext>();
        Assert.True(await context.Sessions.AnyAsync(session => session.Id == seeded.Id));
    }

    [Fact]
    public async Task Deleting_a_record_nobody_indexed_is_not_found()
    {
        using var client = factory.CreateCaller();

        var response = await client.DeleteAsync($"/api/records/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task A_record_whose_file_already_vanished_can_still_be_removed()
    {
        // The row is all that is left of it, and the user asked for it to go.
        var seeded = await RecordSeed.WriteAsync(factory, _root, deleteFile: true);
        using var client = factory.CreateCaller();

        var response = await client.DeleteAsync($"/api/records/{seeded.Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    /// <summary>Only the part of the series response these tests look at.</summary>
    /// <param name="Time">Seconds from the start of the capture.</param>
    private sealed record SeriesResponseShape(double[] Time);
}
