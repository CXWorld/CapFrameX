using CapFrameX.Service.Contracts.Frames;

namespace CapFrameX.Service.Core.Platform;

/// <summary>
/// A running frame delivery for one process. Disposing it releases the source's demand for that
/// process; the last disposed subscription lets the source stand down.
/// </summary>
public interface IFrameSubscription : IAsyncDisposable
{
    /// <summary>Process this subscription observes.</summary>
    int ProcessId { get; }

    /// <summary>
    /// Frames in arrival order. The sequence completes when the process exits or the subscription
    /// is disposed.
    /// </summary>
    IAsyncEnumerable<FrameSample> Frames { get; }
}
