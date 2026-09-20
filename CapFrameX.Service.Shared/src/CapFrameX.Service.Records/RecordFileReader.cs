using CapFrameX.Data.Session.Classes;
using CapFrameX.Data.Session.Contracts;
using Newtonsoft.Json;

namespace CapFrameX.Service.Records;

/// <summary>
/// Reads a CapFrameX capture file.
/// </summary>
/// <remarks>
/// The format is the one CapFrameX 1.x writes, and the model comes from
/// <c>CapFrameX.Data.Session</c> rather than a new one: records are shared between machines and
/// between both generations, so a second parser would only find new ways to disagree with the
/// files users already have.
/// </remarks>
public sealed class RecordFileReader
{
    /// <summary>Extension of a capture file.</summary>
    public const string Extension = ".json";

    private static readonly JsonSerializerSettings Settings = new()
    {
        // A capture the reader does not fully understand is still a capture; a new field added by a
        // later CapFrameX must not make the file unreadable.
        MissingMemberHandling = MissingMemberHandling.Ignore,
        NullValueHandling = NullValueHandling.Ignore,
    };

    /// <summary>Reads one capture file.</summary>
    /// <param name="path">Path of the file.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    public async Task<RecordReadResult> ReadAsync(string path, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        string content;

        try
        {
            content = await File.ReadAllTextAsync(path, cancellationToken);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Being written right now, or not ours to read: the scan moves on.
            return RecordReadResult.Failure($"'{path}' could not be read: {exception.Message}");
        }

        return Parse(content, path);
    }

    /// <summary>Parses capture file content that has already been loaded.</summary>
    /// <param name="content">The file's text.</param>
    /// <param name="origin">Where it came from, for the error message.</param>
    public RecordReadResult Parse(string content, string origin = "<memory>")
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return RecordReadResult.Failure($"'{origin}' is empty.");
        }

        Session? session;

        try
        {
            session = JsonConvert.DeserializeObject<Session>(content, Settings);
        }
        catch (Exception exception)
        {
            // Deliberately broad. The input is a file in a folder the user controls, and the
            // legacy model does not fail uniformly on nonsense: a JSON object without a "Runs"
            // array reaches its constructor with null and throws ArgumentNullException, not
            // JsonException. Any of that has to skip one file, not stop the scan.
            return RecordReadResult.Failure($"'{origin}' is not a capture file: {exception.Message}");
        }

        if (session is null)
        {
            return RecordReadResult.Failure($"'{origin}' contains no capture.");
        }

        // Valid JSON that happens to have none of a capture's parts is not a capture. Without this
        // any stray .json in the capture folder would enter the index as an empty record.
        if (session.Runs is null || session.Runs.Count == 0)
        {
            return RecordReadResult.Failure($"'{origin}' has no runs.");
        }

        return RecordReadResult.Success(session);
    }
}
