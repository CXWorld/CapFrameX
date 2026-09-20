using CapFrameX.Data.Session.Contracts;

namespace CapFrameX.Service.Records;

/// <summary>
/// The outcome of reading one capture file.
/// </summary>
/// <remarks>
/// Reading a record is not exceptional when it fails: the indexer walks a directory the user
/// controls, where a half-written file, a leftover from a crashed capture or something that merely
/// ends in <c>.json</c> are all normal. Those must skip one file, not stop the scan, so the failure
/// is a value rather than an exception.
/// </remarks>
/// <param name="Session">The parsed capture, or <c>null</c> when reading failed.</param>
/// <param name="Error">Why it failed, phrased for a log; <c>null</c> on success.</param>
public readonly record struct RecordReadResult(ISession? Session, string? Error)
{
    /// <summary>Whether the file yielded a usable capture.</summary>
    public bool IsSuccess => Session is not null;

    /// <summary>The capture was read.</summary>
    /// <param name="session">The parsed capture.</param>
    public static RecordReadResult Success(ISession session) => new(session, null);

    /// <summary>The file could not be used.</summary>
    /// <param name="error">Why.</param>
    public static RecordReadResult Failure(string error) => new(null, error);
}
