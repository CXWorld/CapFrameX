using System.Collections.Generic;

namespace CapFrameX.Contracts.Overlay
{
    public interface IOverlayEntryCore
    {
        Dictionary<string, IOverlayEntry> OverlayEntryDict { get; set; }
    }
}
