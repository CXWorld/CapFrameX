namespace CapFrameX.Contracts.Configuration
{
    /// <summary>
    /// Ties <see cref="IAppConfiguration.OverlayRenderer"/> to the two older renderer flags,
    /// <see cref="IAppConfiguration.EnableHookOverlay"/> and
    /// <see cref="IAppConfiguration.EnableHookFreeOverlay"/>. The configuration stores the renderer
    /// and keeps both flags as its mirror, so a build that only knows the flags still reads a
    /// usable selection: <see cref="EOverlayRenderer.Off"/> mirrors to the same flags as RTSS.
    /// </summary>
    public static class OverlayRendererSelection
    {
        /// <summary>
        /// The renderer a pair of stored flags selects. A missing flag takes the default it had
        /// before the renderer setting existed: the in-game overlay is the default renderer, and
        /// the hook-free one is the default once the in-game overlay was switched off.
        /// </summary>
        public static EOverlayRenderer FromFlags(bool? enableHookOverlay, bool? enableHookFreeOverlay)
        {
            bool hook = enableHookOverlay ?? true;
            if (hook)
                return EOverlayRenderer.InGame;

            return enableHookFreeOverlay ?? true ? EOverlayRenderer.HookFree : EOverlayRenderer.Rtss;
        }

        /// <summary>
        /// The renderer to load from storage. The stored renderer wins unless a stored flag
        /// contradicts it: then a build that only knows the flags changed the selection since,
        /// and the flags describe what the user chose last.
        /// </summary>
        public static EOverlayRenderer Reconcile(EOverlayRenderer? storedRenderer,
            bool? enableHookOverlay, bool? enableHookFreeOverlay)
        {
            if (storedRenderer.HasValue
                && (!enableHookOverlay.HasValue || enableHookOverlay.Value == EnablesHookOverlay(storedRenderer.Value))
                && (!enableHookFreeOverlay.HasValue || enableHookFreeOverlay.Value == EnablesHookFreeOverlay(storedRenderer.Value)))
                return storedRenderer.Value;

            return FromFlags(enableHookOverlay, enableHookFreeOverlay);
        }

        /// <summary>The <see cref="IAppConfiguration.EnableHookOverlay"/> mirror of a renderer.</summary>
        public static bool EnablesHookOverlay(EOverlayRenderer renderer)
            => renderer == EOverlayRenderer.InGame;

        /// <summary>The <see cref="IAppConfiguration.EnableHookFreeOverlay"/> mirror of a renderer.</summary>
        public static bool EnablesHookFreeOverlay(EOverlayRenderer renderer)
            => renderer == EOverlayRenderer.HookFree;

        /// <summary>
        /// The renderer after <see cref="IAppConfiguration.EnableHookOverlay"/> was written:
        /// switching it on selects the in-game overlay, switching it off while the in-game
        /// overlay is selected falls back to RTSS, as two cleared flags always meant.
        /// </summary>
        public static EOverlayRenderer WithHookOverlay(EOverlayRenderer current, bool enabled)
            => enabled ? EOverlayRenderer.InGame
                : current == EOverlayRenderer.InGame ? EOverlayRenderer.Rtss : current;

        /// <summary>
        /// The renderer after <see cref="IAppConfiguration.EnableHookFreeOverlay"/> was written;
        /// the hook-free counterpart of <see cref="WithHookOverlay"/>.
        /// </summary>
        public static EOverlayRenderer WithHookFreeOverlay(EOverlayRenderer current, bool enabled)
            => enabled ? EOverlayRenderer.HookFree
                : current == EOverlayRenderer.HookFree ? EOverlayRenderer.Rtss : current;

        /// <summary>
        /// Whether the renderer can show an overlay at all. RTSS needs an installed RTSS;
        /// <see cref="EOverlayRenderer.Off"/> never shows one.
        /// </summary>
        public static bool CanShowOverlay(EOverlayRenderer renderer, bool isRTSSInstalled)
        {
            switch (renderer)
            {
                case EOverlayRenderer.Rtss: return isRTSSInstalled;
                case EOverlayRenderer.InGame:
                case EOverlayRenderer.HookFree: return true;
                default: return false;
            }
        }

        /// <summary>
        /// Whether RTSS is the selected renderer. The flags are checked as well: an
        /// <see cref="IAppConfiguration"/> that only maintains the flags reports the default
        /// renderer, <see cref="EOverlayRenderer.Rtss"/>, and must not read as RTSS while one of
        /// the CapFrameX renderers is selected.
        /// </summary>
        public static bool UsesRtss(this IAppConfiguration configuration)
            => configuration.OverlayRenderer == EOverlayRenderer.Rtss
                && !configuration.EnableHookOverlay && !configuration.EnableHookFreeOverlay;

        /// <summary>
        /// <see cref="CanShowOverlay(EOverlayRenderer, bool)"/> for the configured renderer, with
        /// the flags taking precedence for the reason given at <see cref="UsesRtss"/>.
        /// </summary>
        public static bool CanShowOverlay(this IAppConfiguration configuration, bool isRTSSInstalled)
            => configuration.EnableHookOverlay || configuration.EnableHookFreeOverlay
                || (isRTSSInstalled && configuration.UsesRtss());
    }
}
