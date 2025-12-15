using CapFrameX.Contracts.Overlay;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CapFrameX.Overlay
{
    public class OverlayEntryCore : IOverlayEntryCore
    {
        public Dictionary<string, IOverlayEntry> OverlayEntryDict { get; }

        public TaskCompletionSource<bool> OverlayEntryCoreCompletionSource { get; }

        public Dictionary<string, IOverlayEntry> RealtimeMetricEntryDict { get; }

        public OverlayEntryCore()
        {
            OverlayEntryDict = new Dictionary<string, IOverlayEntry>();
            RealtimeMetricEntryDict = new Dictionary<string, IOverlayEntry>();
            OverlayEntryCoreCompletionSource = new TaskCompletionSource<bool>();
        }

        public IOverlayEntry GetOverlayEntry(string key)
        {
            OverlayEntryDict.TryGetValue(key, out var entry);
            return entry;
        }
    }
}