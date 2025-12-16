using CapFrameX.Contracts.Overlay;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace CapFrameX.Overlay
{
    public class OverlayEntryCore : IOverlayEntryCore
    {
        public ConcurrentDictionary<string, IOverlayEntry> OverlayEntryDict { get; }

        public TaskCompletionSource<bool> OverlayEntryCoreCompletionSource { get; }

        public ConcurrentDictionary<string, IOverlayEntry> RealtimeMetricEntryDict { get; }

        public OverlayEntryCore()
        {
            OverlayEntryDict = new ConcurrentDictionary<string, IOverlayEntry>();
            RealtimeMetricEntryDict = new ConcurrentDictionary<string, IOverlayEntry>();
            OverlayEntryCoreCompletionSource = new TaskCompletionSource<bool>();
        }

        public IOverlayEntry GetOverlayEntry(string key)
        {
            OverlayEntryDict.TryGetValue(key, out var entry);
            return entry;
        }
    }
}