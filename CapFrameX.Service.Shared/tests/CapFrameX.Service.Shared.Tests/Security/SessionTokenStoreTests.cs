using CapFrameX.Service.Core.Platform;
using CapFrameX.Service.Core.Security;

namespace CapFrameX.Service.Shared.Tests.Security;

/// <summary>
/// Covers the start order where the frontend did not launch the service and has to find its token.
/// </summary>
public sealed class SessionTokenStoreTests : IDisposable
{
    private readonly string _runtimeDirectory =
        Path.Combine(Path.GetTempPath(), "cfx-token-" + Guid.NewGuid().ToString("N"));

    private readonly RecordingWriter _writer = new();

    private SessionTokenStore Store() => new(new TestPaths(_runtimeDirectory), _writer);

    [Fact]
    public void Token_file_sits_in_the_runtime_directory()
    {
        Assert.Equal(Path.Combine(_runtimeDirectory, SessionTokenStore.FileName), Store().Path);
    }

    [Fact]
    public void Published_token_can_be_read_back()
    {
        var store = Store();
        var token = SessionToken.Generate();

        store.Publish(token);

        Assert.Equal(token.Value, store.Read()!.Value);
    }

    [Fact]
    public void Publishing_goes_through_the_restricted_writer()
    {
        // Plain File.WriteAllText would leave the token readable by every user on the machine.
        var store = Store();

        store.Publish(SessionToken.Generate());

        Assert.Equal(store.Path, Assert.Single(_writer.Written).Path);
    }

    [Fact]
    public void Missing_runtime_directory_is_created()
    {
        Assert.False(Directory.Exists(_runtimeDirectory));

        Store().Publish(SessionToken.Generate());

        Assert.True(Directory.Exists(_runtimeDirectory));
    }

    [Fact]
    public void Publishing_again_replaces_the_previous_token()
    {
        var store = Store();
        store.Publish(new SessionToken("first-token-value"));

        var second = SessionToken.Generate();
        store.Publish(second);

        Assert.Equal(second.Value, store.Read()!.Value);
    }

    [Fact]
    public void Reading_without_a_published_token_yields_nothing()
    {
        Assert.Null(Store().Read());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   \r\n")]
    public void Blank_token_file_is_treated_as_absent(string content)
    {
        var store = Store();
        Directory.CreateDirectory(_runtimeDirectory);
        File.WriteAllText(store.Path, content);

        Assert.Null(store.Read());
    }

    [Fact]
    public void Surrounding_whitespace_is_not_part_of_the_token()
    {
        var store = Store();
        Directory.CreateDirectory(_runtimeDirectory);
        File.WriteAllText(store.Path, "  padded-token-value\r\n");

        Assert.Equal("padded-token-value", store.Read()!.Value);
    }

    [Fact]
    public void Revoking_removes_the_token()
    {
        var store = Store();
        store.Publish(SessionToken.Generate());

        store.Revoke();

        Assert.Null(store.Read());
        Assert.False(File.Exists(store.Path));
    }

    [Fact]
    public void Revoking_without_a_token_is_harmless()
    {
        var store = Store();

        store.Revoke();
        store.Revoke();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (Directory.Exists(_runtimeDirectory))
        {
            Directory.Delete(_runtimeDirectory, recursive: true);
        }
    }

    private sealed class RecordingWriter : ISecretFileWriter
    {
        public List<(string Path, string Content)> Written { get; } = [];

        public void Write(string path, string content)
        {
            Written.Add((path, content));
            File.WriteAllText(path, content);
        }
    }

    private sealed class TestPaths(string runtimeDirectory) : IAppPaths
    {
        public string ConfigurationDirectory => runtimeDirectory;

        public string DataDirectory => runtimeDirectory;

        public string CaptureDirectory => runtimeDirectory;

        public string LogDirectory => runtimeDirectory;

        public string RuntimeDirectory => runtimeDirectory;

        public bool IsPortable => false;
    }
}
