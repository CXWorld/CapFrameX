using CapFrameX.Service.Contracts.Frames;

namespace CapFrameX.Service.Core.Platform;

/// <summary>
/// Platform port for present data: PresentMon on Windows, the CapFrameX.OSD Vulkan layer on Linux.
/// Implementations do the platform work and nothing else; the capture lifecycle, buffering and
/// record writing live in the shared application layer.
/// </summary>
public interface IFrameSource : IAsyncDisposable
{
    /// <summary>Identity and the optional metrics this source can deliver.</summary>
    FrameSourceInfo Info { get; }

    /// <summary>Whether the source can be used on this machine, with a reason when it cannot.</summary>
    PlatformAvailability Availability { get; }

    /// <summary>Processes currently presenting, already filtered by the ignore list.</summary>
    /// <param name="cancellationToken">Cancels the enumeration.</param>
    ValueTask<IReadOnlyList<PresentingProcess>> GetPresentingProcessesAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Starts delivering frames of one process. The source does its expensive work - starting
    /// PresentMon, asking the layer for frames - only while at least one subscription is alive.
    /// </summary>
    /// <param name="processId">Process to observe.</param>
    /// <param name="cancellationToken">Cancels the subscription attempt.</param>
    ValueTask<IFrameSubscription> SubscribeAsync(int processId, CancellationToken cancellationToken = default);
}
