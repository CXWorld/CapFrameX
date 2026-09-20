using CapFrameX.Service.Core.Platform;
using CapFrameX.Service.Linux.Platform;

namespace CapFrameX.Service.Linux.Tests.Platform;

/// <summary>
/// The freedesktop trash.
/// </summary>
/// <remarks>
/// It is a convention about two directories and a small text file, so all of it can be checked
/// without Linux: what fails on a real system is the file manager reading the record back, and
/// that is decided by the contents asserted here.
/// </remarks>
public sealed class XdgFileTrashTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "cfx-trash-" + Guid.NewGuid().ToString("N"));
    private readonly string _data;
    private readonly string _captures;

    public XdgFileTrashTests()
    {
        _data = Path.Combine(_root, "data");
        _captures = Path.Combine(_root, "Captures");
        Directory.CreateDirectory(_data);
        Directory.CreateDirectory(_captures);
    }

    public void Dispose()
    {
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
    public void The_trash_follows_the_xdg_data_directory()
    {
        Assert.Equal(Path.Combine(_data, "Trash"), Trash().TrashHome);
    }

    [Fact]
    public void Without_the_variable_it_falls_back_to_the_place_the_specification_names()
    {
        var trash = new XdgFileTrash(_ => null, homeDirectory: _root);

        Assert.Equal(Path.Combine(_root, ".local", "share", "Trash"), trash.TrashHome);
    }

    [Fact]
    public async Task A_capture_moves_out_of_the_folder_and_into_the_trash()
    {
        var path = Capture("run.json");

        var result = await Trash().MoveAsync(path);

        Assert.Equal(TrashOutcome.MovedToTrash, result.Outcome);
        Assert.False(File.Exists(path));
        Assert.True(File.Exists(Path.Combine(_data, "Trash", "files", "run.json")));
    }

    [Fact]
    public async Task The_capture_arrives_whole()
    {
        // Moved, not copied and truncated: this is the user's only copy.
        var path = Capture("run.json", "frame times");

        await Trash().MoveAsync(path);

        Assert.Equal("frame times", await File.ReadAllTextAsync(Path.Combine(_data, "Trash", "files", "run.json")));
    }

    [Fact]
    public async Task A_record_says_where_the_capture_came_from()
    {
        // Without it a file manager cannot offer to restore, which is the whole reason for not
        // deleting outright.
        var path = Capture("run.json");

        await Trash().MoveAsync(path);

        var record = await File.ReadAllTextAsync(
            Path.Combine(_data, "Trash", "info", "run.json" + XdgFileTrash.InfoExtension));

        Assert.StartsWith("[Trash Info]", record, StringComparison.Ordinal);
        Assert.Contains("Path=", record, StringComparison.Ordinal);
        Assert.Contains("DeletionDate=", record, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_path_with_spaces_is_encoded_rather_than_written_raw()
    {
        // The record is read back by other programs, and an unescaped space breaks the line.
        var folder = Path.Combine(_captures, "Cyberpunk 2077");
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, "run one.json");
        await File.WriteAllTextAsync(path, "{}");

        await Trash().MoveAsync(path);

        var record = await File.ReadAllTextAsync(
            Path.Combine(_data, "Trash", "info", "run one.json" + XdgFileTrash.InfoExtension));

        Assert.Contains("%20", record, StringComparison.Ordinal);
        Assert.DoesNotContain("Path=" + path, record, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Two_captures_of_the_same_name_both_survive()
    {
        // Benchmark folders are full of files called the same thing in different directories.
        var first = Capture("run.json", "first");
        var second = Path.Combine(_captures, "other");
        Directory.CreateDirectory(second);
        var duplicate = Path.Combine(second, "run.json");
        await File.WriteAllTextAsync(duplicate, "second");

        var trash = Trash();
        await trash.MoveAsync(first);
        await trash.MoveAsync(duplicate);

        var files = Path.Combine(_data, "Trash", "files");
        Assert.Equal("first", await File.ReadAllTextAsync(Path.Combine(files, "run.json")));
        Assert.Equal("second", await File.ReadAllTextAsync(Path.Combine(files, "run.1.json")));
    }

    [Fact]
    public async Task A_capture_that_is_already_gone_is_not_a_failure()
    {
        // The folder is the user's; the file may have been removed between the list and the click.
        var result = await Trash().MoveAsync(Path.Combine(_captures, "never-existed.json"));

        Assert.Equal(TrashOutcome.NotFound, result.Outcome);
        Assert.True(result.IsRemoved);
    }

    [Fact]
    public async Task A_failed_move_leaves_no_record_behind()
    {
        // A record without its file shows up in every file manager as an entry that cannot be
        // restored.
        var path = Capture("locked.json");
        var trash = Trash();

        using (File.Open(path, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            var result = await trash.MoveAsync(path);

            Assert.Equal(TrashOutcome.Failed, result.Outcome);
            Assert.False(result.IsRemoved);
        }

        Assert.True(File.Exists(path));
        Assert.Empty(Directory.GetFiles(Path.Combine(_data, "Trash", "info")));
    }

    private XdgFileTrash Trash() =>
        new(name => name == "XDG_DATA_HOME" ? _data : null, homeDirectory: _root);

    private string Capture(string name, string content = "{}")
    {
        var path = Path.Combine(_captures, name);
        File.WriteAllText(path, content);

        return path;
    }
}
