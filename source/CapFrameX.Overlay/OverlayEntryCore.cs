using CapFrameX.Contracts.Overlay;
using System.Collections.Generic;

namespace CapFrameX.Overlay
{
    public class OverlayEntryCore : IOverlayEntryCore
    {
        public Dictionary<string, IOverlayEntry> OverlayEntryDict { get; set; }

        public OverlayEntryCore()
        {
            OverlayEntryDict = new Dictionary<string, IOverlayEntry>();
        }
    }
}
