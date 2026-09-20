using CapFrameX.Service.Records;

namespace CapFrameX.Service.Records.Tests;

/// <summary>
/// The index has to follow a folder the user edits behind its back - copying captures in, deleting
/// them, overwriting one with a longer run. These are the rules for noticing.
/// </summary>
public sealed class RecordIndexPlannerTests
{
    private static readonly DateTime Monday = new(2026, 9, 14, 10, 0, 0, DateTimeKind.Utc);

    private static RecordFile File(string path, long size = 1000, DateTime? modified = null) =>
        new(path, size, modified ?? Monday);

    private static IndexedRecord Indexed(
        string path,
        long size = 1000,
        DateTime? modified = null,
        int version = RecordIndexPlanner.CurrentIndexVersion) =>
        new(Guid.NewGuid(), path, size, modified ?? Monday, version);

    [Fact]
    public void Empty_folder_and_empty_index_mean_nothing_to_do()
    {
        var plan = RecordIndexPlanner.Plan([], []);

        Assert.True(plan.IsEmpty);
    }

    [Fact]
    public void Unchanged_file_is_left_alone()
    {
        // The common case: a scan over thousands of captures that have not moved must do no work.
        var plan = RecordIndexPlanner.Plan([File(@"C:\c\a.json")], [Indexed(@"C:\c\a.json")]);

        Assert.True(plan.IsEmpty);
    }

    [Fact]
    public void New_file_is_added()
    {
        var plan = RecordIndexPlanner.Plan([File(@"C:\c\new.json")], []);

        Assert.Equal(@"C:\c\new.json", Assert.Single(plan.Added).Path);
        Assert.Empty(plan.Updated);
        Assert.Empty(plan.Removed);
    }

    [Fact]
    public void File_that_grew_is_updated()
    {
        var plan = RecordIndexPlanner.Plan(
            [File(@"C:\c\a.json", size: 2000)],
            [Indexed(@"C:\c\a.json", size: 1000)]);

        Assert.Equal(@"C:\c\a.json", Assert.Single(plan.Updated).Path);
        Assert.Empty(plan.Added);
    }

    [Fact]
    public void File_written_again_is_updated_even_at_the_same_size()
    {
        var plan = RecordIndexPlanner.Plan(
            [File(@"C:\c\a.json", modified: Monday.AddHours(1))],
            [Indexed(@"C:\c\a.json", modified: Monday)]);

        Assert.Single(plan.Updated);
    }

    [Fact]
    public void Record_indexed_by_an_older_version_is_re_read()
    {
        // A summary that gained a field has to be rebuilt, and a version bump is cheaper than a
        // schema migration for that.
        var plan = RecordIndexPlanner.Plan(
            [File(@"C:\c\a.json")],
            [Indexed(@"C:\c\a.json", version: RecordIndexPlanner.CurrentIndexVersion - 1)]);

        Assert.Single(plan.Updated);
    }

    [Fact]
    public void Record_indexed_by_a_newer_version_is_left_alone()
    {
        // An older service must not undo the work of a newer one it shares a database with.
        var plan = RecordIndexPlanner.Plan(
            [File(@"C:\c\a.json")],
            [Indexed(@"C:\c\a.json", version: RecordIndexPlanner.CurrentIndexVersion + 1)]);

        Assert.True(plan.IsEmpty);
    }

    [Fact]
    public void Deleted_file_removes_its_record()
    {
        var indexed = Indexed(@"C:\c\gone.json");

        var plan = RecordIndexPlanner.Plan([], [indexed]);

        Assert.Equal(indexed.Id, Assert.Single(plan.Removed).Id);
    }

    [Fact]
    public void Sessions_the_service_recorded_itself_are_not_removed()
    {
        // A row without a source file is not backed by the folder, so a scan of the folder says
        // nothing about it.
        var plan = RecordIndexPlanner.Plan([], [Indexed(string.Empty)]);

        Assert.Empty(plan.Removed);
    }

    [Fact]
    public void Paths_are_matched_the_way_the_platform_compares_them()
    {
        // Windows hands back the same file under different casing depending on who asked.
        var plan = RecordIndexPlanner.Plan(
            [File(@"C:\Captures\A.json")],
            [Indexed(@"c:\captures\a.json")]);

        Assert.Equal(OperatingSystem.IsWindows(), plan.IsEmpty);
    }

    [Fact]
    public void A_mixed_folder_yields_all_three_lists()
    {
        var gone = Indexed(@"C:\c\gone.json");

        var plan = RecordIndexPlanner.Plan(
            [File(@"C:\c\same.json"), File(@"C:\c\changed.json", size: 9), File(@"C:\c\new.json")],
            [Indexed(@"C:\c\same.json"), Indexed(@"C:\c\changed.json"), gone]);

        Assert.Equal(@"C:\c\new.json", Assert.Single(plan.Added).Path);
        Assert.Equal(@"C:\c\changed.json", Assert.Single(plan.Updated).Path);
        Assert.Equal(gone.Id, Assert.Single(plan.Removed).Id);
    }
}
