namespace CapFrameX.Contracts.Overlay
{
    /// <summary>
    /// Immutable snapshot of an active Windows display as reported by the application's shared
    /// display detection. Width and height are the current desktop bounds in pixels.
    /// </summary>
    public sealed class DetectedDisplay
    {
        public DetectedDisplay(string deviceName, int width, int height, bool isPrimary)
        {
            DeviceName = deviceName ?? string.Empty;
            Width = width;
            Height = height;
            IsPrimary = isPrimary;
        }

        public string DeviceName { get; }

        public int Width { get; }

        public int Height { get; }

        public bool IsPrimary { get; }
    }
}
