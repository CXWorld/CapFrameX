using CapFrameX.Service.Core.Platform;

namespace CapFrameX.Service.Api.Tests;

/// <summary>
/// A trash the tests can look inside.
/// </summary>
/// <remarks>
/// It moves the file rather than deleting it, which is what the real ones do and what the endpoint
/// promises. The platform implementations are tested against the actual recycle bin and the actual
/// freedesktop layout; what matters here is that the endpoint reaches one at all, and never
/// unlinks a capture itself.
/// </remarks>
public sealed class RecordingTrash : IFileTrash
{
    private readonly List<string> _moved = [];
    private readonly Lock _gate = new();

    /// <summary>Where trashed captures end up.</summary>
    public string Directory { get; } = Path.Combine(
        Path.GetTempPath(),
        "cfx-api-trash-" + Guid.NewGuid().ToString("N"));

    /// <summary>The captures that were trashed, in order.</summary>
    public IReadOnlyList<string> Moved
    {
        get
        {
            lock (_gate)
            {
                return [.. _moved];
            }
        }
    }

    /// <summary>Makes the next move fail, as a locked or read-only file would.</summary>
    public bool Refuse { get; set; }

    /// <inheritdoc />
    public bool IsSupported => true;

    /// <inheritdoc />
    public Task<TrashResult> MoveAsync(string path, CancellationToken cancellationToken = default)
    {
        if (Refuse)
        {
            return Task.FromResult(new TrashResult(TrashOutcome.Failed, $"'{path}' is in use."));
        }

        if (!File.Exists(path))
        {
            return Task.FromResult(new TrashResult(TrashOutcome.NotFound));
        }

        System.IO.Directory.CreateDirectory(Directory);
        File.Move(path, Path.Combine(Directory, Path.GetFileName(path)), overwrite: true);

        lock (_gate)
        {
            _moved.Add(path);
        }

        return Task.FromResult(new TrashResult(TrashOutcome.MovedToTrash));
    }
}
