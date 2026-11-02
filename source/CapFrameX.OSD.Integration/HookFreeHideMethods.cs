using System;
using System.Runtime.InteropServices;

namespace CapFrameX.OSD.Integration
{
    /// <summary>
    /// How the hook-free window hides; the values of the native core's cfx_osd_set_hide_mode.
    /// </summary>
    internal enum HookFreeHideMethod
    {
        Cloak = 0,
        ShowWindow = 1,
        Transparent = 2
    }

    /// <summary>
    /// Chooses how the hook-free window hides from the target's present mode. A window that leaves
    /// the composition makes DWM reassign its planes: in Mafia: The Old Country (RX 9070 XT, VRR,
    /// FSR FG, 2026-10-31) every capture began with 1-2 s of lost displays after the auto-hide, with
    /// the cloak and with ShowWindow alike, although the game stayed on its plane. A game on a plane
    /// of its own (PresentMon "Hardware: ...") keeps it while the emptied window stays composed, so
    /// the transparent frame hides the overlay without that transition (three captures began
    /// clean). A composed game, or one whose mode is unknown, is cloaked: a window that stayed would
    /// keep it composed for the whole capture, which is the overlay's cost the hide removes.
    /// </summary>
    internal static class HookFreeHideMethods
    {
        internal static HookFreeHideMethod Choose(string presentMode)
            => presentMode != null && presentMode.StartsWith("Hardware", StringComparison.Ordinal)
                ? HookFreeHideMethod.Transparent
                : HookFreeHideMethod.Cloak;

        [DllImport("cfx_osd_core", CallingConvention = CallingConvention.Cdecl)]
        private static extern void cfx_osd_set_hide_mode(int mode);

        private static volatile bool _apiAvailable = true;

        /// <summary>
        /// Sets the method of the next hides. False when the native core predates the API; it then
        /// cloaks, as before.
        /// </summary>
        internal static bool Apply(HookFreeHideMethod method)
        {
            if (!_apiAvailable) return false;
            try
            {
                cfx_osd_set_hide_mode((int)method);
                return true;
            }
            catch (EntryPointNotFoundException) { _apiAvailable = false; }
            catch (DllNotFoundException) { _apiAvailable = false; }
            return false;
        }
    }
}
