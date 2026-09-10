namespace CapFrameX.Contracts.Overlay
{
    public static class OverlayAvailability
    {
        public static bool IsInGameAvailable
        {
            get
            {
#if CFX_INGAME_OVERLAY
                return true;
#else
                return false;
#endif
            }
        }

        public const string InGameUnavailableMessage =
            "Coming in a later update once our code-signing certificate is available.";
    }
}
