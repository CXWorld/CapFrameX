using CapFrameX.Service.Records;

namespace CapFrameX.Service.Records.Tests;

/// <summary>
/// The indexer walks a directory the user controls, so the reader meets half-written files,
/// leftovers from a crashed capture and anything else that ends in <c>.json</c>. None of that may
/// stop a scan, and none of it may enter the index as an empty record.
/// </summary>
public sealed class RecordFileReaderTests : IDisposable
{
    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), "cfx-records-" + Guid.NewGuid().ToString("N"));

    private readonly RecordFileReader _reader = new();

    public RecordFileReaderTests() => Directory.CreateDirectory(_directory);

    private string Write(string name, string content)
    {
        var path = Path.Combine(_directory, name);
        File.WriteAllText(path, content);
        return path;
    }

    [Fact]
    public void Capture_with_its_parts_is_read()
    {
        var result = _reader.Parse(RecordFixtures.Capture);

        Assert.True(result.IsSuccess, result.Error);
        Assert.Null(result.Error);
    }

    [Fact]
    public void Session_info_survives_the_read()
    {
        var session = _reader.Parse(RecordFixtures.Capture).Session!;

        Assert.Equal("Cyberpunk 2077", session.Info.GameName);
        Assert.Equal("Cyberpunk2077", session.Info.ProcessName);
        Assert.Equal("RTX 5090", session.Info.GPU);
        Assert.Equal("Ryzen 9 9950X", session.Info.Processor);
    }

    [Fact]
    public void Frame_times_survive_the_read()
    {
        var run = _reader.Parse(RecordFixtures.Capture).Session!.Runs[0];

        Assert.Equal([16.6, 16.7, 16.5], run.CaptureData!.MsBetweenPresents);
        Assert.Equal([0.0, 0.0166, 0.0333], run.CaptureData.TimeInSeconds);
    }

    [Fact]
    public async Task Missing_file_is_a_failure_not_an_exception()
    {
        var result = await _reader.ReadAsync(Path.Combine(_directory, "not-there.json"));

        Assert.False(result.IsSuccess);
        Assert.Contains("not-there.json", result.Error);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   \r\n")]
    public async Task Empty_file_is_a_failure(string content)
    {
        var result = await _reader.ReadAsync(Write("empty.json", content));

        Assert.False(result.IsSuccess);
        Assert.Contains("empty", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Half_written_file_is_a_failure()
    {
        // What a capture interrupted by a crash leaves behind.
        var truncated = RecordFixtures.Capture[..(RecordFixtures.Capture.Length / 2)];

        var result = await _reader.ReadAsync(Write("truncated.json", truncated));

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("""{"Info":{"GameName":"X"}}""")]
    [InlineData("""{"Info":{"GameName":"X"},"Runs":[]}""")]
    public async Task Json_without_runs_is_not_a_capture(string content)
    {
        // Otherwise any stray .json in the capture folder enters the index as an empty record.
        var result = await _reader.ReadAsync(Write("stray.json", content));

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
    }

    [Fact]
    public async Task Unknown_fields_do_not_make_a_capture_unreadable()
    {
        // A record written by a later CapFrameX still has to open here.
        var withExtra = RecordFixtures.Capture.Replace(
            """"Hash":"""",
            """"SomethingFromTheFuture":{"a":1},"Hash":"""",
            StringComparison.Ordinal);

        var result = await _reader.ReadAsync(Write("future.json", withExtra));

        Assert.True(result.IsSuccess, result.Error);
    }

    [Fact]
    public async Task File_is_read_from_disk_as_well_as_from_memory()
    {
        var result = await _reader.ReadAsync(Write("capture.json", RecordFixtures.Capture));

        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal("Cyberpunk 2077", result.Session!.Info.GameName);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }
}
