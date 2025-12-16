using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CapFrameX.Contracts.Overlay
{
    public interface IOverlayEntryCore
    {
        ConcurrentDictionary<string, IOverlayEntry> OverlayEntryDict { get; }

        ConcurrentDictionary<string, IOverlayEntry> RealtimeMetricEntryDict { get; }

        IOverlayEntry GetOverlayEntry(string key);

        TaskCompletionSource<bool> OverlayEntryCoreCompletionSource { get; }
    }
}
