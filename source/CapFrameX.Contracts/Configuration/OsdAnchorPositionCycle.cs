namespace CapFrameX.Contracts.Configuration
{
    /// <summary>
    /// Defines the ordered set of CapFrameX OSD anchors used by the renderer and options UI.
    /// </summary>
    public static class OsdAnchorPositionCycle
    {
        public const int PositionCount = 5;

        /// <summary>
        /// Returns the following anchor, wrapping the last or an invalid value to top left.
        /// </summary>
        public static int GetNext(int currentPosition)
        {
            return currentPosition >= 0 && currentPosition < PositionCount - 1
                ? currentPosition + 1
                : 0;
        }
    }
}
