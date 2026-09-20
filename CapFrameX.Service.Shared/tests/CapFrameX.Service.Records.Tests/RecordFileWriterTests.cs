using CapFrameX.Data.Session.Contracts;
using CapFrameX.Service.Contracts.Records;

namespace CapFrameX.Service.Records.Tests;

/// <summary>
/// Editing a capture.
/// </summary>
/// <remarks>
/// The file is the record, so this overwrites something the user cannot get back. What is under
/// test is as much what it leaves alone as what it changes.
/// </remarks>
public sealed class RecordFileWriterTests : IDisposable
{
    private readonly RecordFileReader _reader = new();
    private readonly RecordFileWriter _writer = new();
    private readonly string _root = Path.Combine(Path.GetTempPath(), "cfx-write-" + Guid.NewGuid().ToString("N"));

    public RecordFileWriterTests() => Directory.CreateDirectory(_root);

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
    public void A_field_that_was_not_asked_about_is_left_alone()
    {
        // That is what makes it a patch: a client that only edits the comment must not blank the
        // hardware it never sent.
        var session = Parse(RecordFixtures.Capture);

        Assert.True(RecordFileWriter.Apply(session, new RecordEdit(Comment: "rerun")));

        Assert.Equal("rerun", session.Info.Comment);
        Assert.Equal("Ryzen 9 9950X", session.Info.Processor);
        Assert.Equal("Cyberpunk 2077", session.Info.GameName);
    }

    [Fact]
    public void An_empty_string_clears_a_field()
    {
        // Distinct from leaving it out, and the only way to remove a comment.
        var session = Parse(RecordFixtures.Capture);

        Assert.True(RecordFileWriter.Apply(session, new RecordEdit(Comment: string.Empty)));

        Assert.Equal(string.Empty, session.Info.Comment);
    }

    [Fact]
    public void An_edit_that_changes_nothing_reports_that_it_changed_nothing()
    {
        // The caller uses this to skip the write, so the file keeps its timestamp and the indexer
        // is not woken for nothing.
        var session = Parse(RecordFixtures.Capture);

        Assert.False(RecordFileWriter.Apply(session, new RecordEdit(Comment: "ultra settings")));
        Assert.False(RecordFileWriter.Apply(session, new RecordEdit()));
    }

    [Fact]
    public void Every_editable_field_can_be_changed()
    {
        var session = Parse(RecordFixtures.Capture);

        RecordFileWriter.Apply(session, new RecordEdit(
            GameName: "Cyberpunk 2077 (patched)",
            Comment: "second run",
            Processor: "Ryzen 7 9800X3D",
            Gpu: "RX 9070 XT",
            SystemRam: "64 GB",
            Motherboard: "X870E",
            ResolutionInfo: "3840x2160"));

        Assert.Equal("Cyberpunk 2077 (patched)", session.Info.GameName);
        Assert.Equal("second run", session.Info.Comment);
        Assert.Equal("Ryzen 7 9800X3D", session.Info.Processor);
        Assert.Equal("RX 9070 XT", session.Info.GPU);
        Assert.Equal("64 GB", session.Info.SystemRam);
        Assert.Equal("X870E", session.Info.Motherboard);
        Assert.Equal("3840x2160", session.Info.ResolutionInfo);
    }

    [Fact]
    public async Task A_written_capture_reads_back_as_what_it_was()
    {
        // The round trip is the point: 1.x has to keep reading the file after we touch it.
        var path = Write(RecordFixtures.CaptureWithTwoRuns);
        var session = Parse(await File.ReadAllTextAsync(path));
        RecordFileWriter.Apply(session, new RecordEdit(Comment: "edited"));

        await _writer.SaveAsync(path, session);

        var reloaded = Parse(await File.ReadAllTextAsync(path));
        Assert.Equal("edited", reloaded.Info.Comment);
        Assert.Equal(2, reloaded.Runs.Count);
        Assert.Equal(
            session.Runs[0].CaptureData.MsBetweenPresents,
            reloaded.Runs[0].CaptureData.MsBetweenPresents);
    }

    [Fact]
    public async Task Nothing_is_left_behind_beside_the_capture()
    {
        // A leftover file in the capture folder is a record the user did not make.
        var path = Write(RecordFixtures.Capture);

        await _writer.SaveAsync(path, Parse(RecordFixtures.Capture));

        Assert.Equal([path], Directory.GetFiles(_root));
    }

    [Fact]
    public async Task The_scratch_file_is_not_mistaken_for_a_capture()
    {
        // The indexer scans for *.json while this runs; a partially written capture must not look
        // like one.
        var path = Write(RecordFixtures.Capture);
        var temporary = path + RecordFileWriter.TemporaryExtension;

        await File.WriteAllTextAsync(temporary, "{ half written");

        Assert.DoesNotContain(
            temporary,
            Directory.GetFiles(_root, "*" + RecordFileReader.Extension, SearchOption.AllDirectories));
    }

    [Fact]
    public async Task A_write_to_a_place_that_does_not_exist_leaves_the_capture_alone()
    {
        var missing = Path.Combine(_root, "no-such-folder", "capture.json");

        await Assert.ThrowsAnyAsync<IOException>(() => _writer.SaveAsync(missing, Parse(RecordFixtures.Capture)));

        Assert.Empty(Directory.GetFiles(_root));
    }

    private string Write(string content)
    {
        var path = Path.Combine(_root, "capture" + RecordFileReader.Extension);
        File.WriteAllText(path, content);

        return path;
    }

    private ISession Parse(string content)
    {
        var read = _reader.Parse(content);

        Assert.True(read.IsSuccess, read.Error);

        return read.Session!;
    }
}
