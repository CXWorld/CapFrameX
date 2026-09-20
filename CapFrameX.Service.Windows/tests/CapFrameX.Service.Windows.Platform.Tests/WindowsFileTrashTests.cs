using CapFrameX.Service.Core.Platform;
using CapFrameX.Service.Windows.Platform;

namespace CapFrameX.Service.Windows.Platform.Tests;

/// <summary>
/// The Windows recycle bin.
/// </summary>
/// <remarks>
/// Against the real shell, because there is nothing else to test: the whole point of the class is
/// the one call into <c>shell32</c>, and a fake would only assert that we wrote the call we wrote.
/// It recycles one tiny file per run, which the user can empty like any other.
/// </remarks>
public sealed class WindowsFileTrashTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "cfx-recycle-test-" + Guid.NewGuid().ToString("N"));

    public WindowsFileTrashTests() => Directory.CreateDirectory(_root);

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
    public async Task A_capture_leaves_the_folder()
    {
        var path = Path.Combine(_root, "cfx-recycle-test.json");
        await File.WriteAllTextAsync(path, "{}");

        var result = await new WindowsFileTrash().MoveAsync(path);

        Assert.Equal(TrashOutcome.MovedToTrash, result.Outcome);
        Assert.False(File.Exists(path));
    }

    [Fact]
    public async Task A_capture_that_is_already_gone_is_not_a_failure()
    {
        // The folder is the user's; the file may have been removed between the list and the click.
        var result = await new WindowsFileTrash().MoveAsync(Path.Combine(_root, "never-existed.json"));

        Assert.Equal(TrashOutcome.NotFound, result.Outcome);
        Assert.True(result.IsRemoved);
    }

    [Fact]
    public async Task A_file_that_is_held_open_stays_where_it_is()
    {
        var path = Path.Combine(_root, "cfx-recycle-locked.json");
        await File.WriteAllTextAsync(path, "{}");

        using var held = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.None);

        var result = await new WindowsFileTrash().MoveAsync(path);

        Assert.Equal(TrashOutcome.Failed, result.Outcome);
        Assert.False(result.IsRemoved);
        Assert.True(File.Exists(path));
    }
}
