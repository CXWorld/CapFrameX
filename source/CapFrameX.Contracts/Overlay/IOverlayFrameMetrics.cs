using System;

namespace CapFrameX.Contracts.Overlay
{
    /// <summary>
    /// Supplies PresentMon frame values to the common overlay snapshot, including remote readers
    /// while the renderer is hidden. Profile entries remain editable and retain their placeholders.
    /// </summary>
    public interface IOverlayFrameMetrics : IDisposable
    {
        void SetEnabled(bool enabled);

        /// <summary>
        /// Closes the current frame interval and fills copies of frame entries and runtime labels.
        /// Returns the original entries unchanged when this source is disabled.
        /// </summary>
        IOverlayEntry[] ApplySnapshot(IOverlayEntry[] entries);
    }
}
