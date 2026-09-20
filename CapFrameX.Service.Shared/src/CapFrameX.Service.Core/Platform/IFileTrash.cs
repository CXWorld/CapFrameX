namespace CapFrameX.Service.Core.Platform;

/// <summary>What became of a file the service was asked to remove.</summary>
public enum TrashOutcome
{
    /// <summary>It is in the platform's trash, where the user can get it back.</summary>
    MovedToTrash,

    /// <summary>It was not there to begin with.</summary>
    NotFound,

    /// <summary>This platform has no trash the service can reach.</summary>
    NotSupported,

    /// <summary>It is still there; the reason says why.</summary>
    Failed,
}

/// <summary>The outcome of one removal.</summary>
/// <param name="Outcome">What became of the file.</param>
/// <param name="Error">Why it failed, phrased for a log; <c>null</c> otherwise.</param>
public readonly record struct TrashResult(TrashOutcome Outcome, string? Error = null)
{
    /// <summary>Whether the file is gone from where it was.</summary>
    public bool IsRemoved => Outcome is TrashOutcome.MovedToTrash or TrashOutcome.NotFound;
}

/// <summary>
/// Moves a file to the platform's trash.
/// </summary>
/// <remarks>
/// Deleting a record deletes hours of benchmarking that cannot be recaptured, so the service never
/// unlinks a capture file. Windows has the recycle bin and Linux the freedesktop trash; both let
/// the user undo it with the tools they already know, which is the point.
/// </remarks>
public interface IFileTrash
{
    /// <summary>Whether this platform offers a trash at all.</summary>
    bool IsSupported { get; }

    /// <summary>Moves one file there.</summary>
    /// <param name="path">Full path of the file.</param>
    /// <param name="cancellationToken">Cancels the move.</param>
    Task<TrashResult> MoveAsync(string path, CancellationToken cancellationToken = default);
}
