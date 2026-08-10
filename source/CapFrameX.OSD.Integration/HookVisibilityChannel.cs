using System;
using System.Runtime.InteropServices;
using Serilog;

namespace CapFrameX.OSD.Integration
{
    /// <summary>
    /// Publishes the overlay show/hide state (the ALT+O toggle = <c>IAppConfiguration.IsOverlayActive</c>)
    /// to the in-game hook via a named manual-reset event, <c>Global\CfxOsdOverlayVisible</c>.
    /// CapFrameX (elevated) owns the event and sets/resets it; the injected hook — running in the
    /// game at medium/low integrity — merely opens it for SYNCHRONIZE and skips drawing while it is
    /// reset. Signaled == visible. If the channel can't be created the hook defaults to visible, so
    /// the toggle degrades gracefully rather than hiding the overlay.
    ///
    /// The event gets a permissive security descriptor: a DACL granting Everyone access plus a LOW
    /// mandatory-integrity label (no-write-up), so a sandboxed/low-integrity game can still open it.
    /// Raw P/Invoke (not <c>EventWaitHandleSecurity</c>) keeps this identical across the project's
    /// net472 and net9.0-windows targets, whose ACL APIs differ.
    /// </summary>
    internal sealed class HookVisibilityChannel : IDisposable
    {
        internal const string EventName = @"Global\CfxOsdOverlayVisible";

        // D: Everyone (WD) = GENERIC_ALL (the flag is not sensitive — worst case a local process
        // toggles the overlay). S: Low mandatory label, NO_WRITE_UP, so low/AppContainer games open it.
        private const string Sddl = "D:(A;;GA;;;WD)S:(ML;;NW;;;LW)";
        private const uint SDDL_REVISION_1 = 1;

        private IntPtr _handle = IntPtr.Zero;

        [StructLayout(LayoutKind.Sequential)]
        private struct SECURITY_ATTRIBUTES
        {
            public int nLength;
            public IntPtr lpSecurityDescriptor;
            public int bInheritHandle;
        }

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr CreateEventW(ref SECURITY_ATTRIBUTES sa, bool manualReset, bool initialState, string name);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetEvent(IntPtr handle);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool ResetEvent(IntPtr handle);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr handle);

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool ConvertStringSecurityDescriptorToSecurityDescriptorW(
            string sddl, uint revision, out IntPtr securityDescriptor, out int size);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr LocalFree(IntPtr handle);

        /// <summary>Creates the toggle event with <paramref name="initiallyVisible"/> as its initial state.</summary>
        public static HookVisibilityChannel Create(bool initiallyVisible)
        {
            var channel = new HookVisibilityChannel();
            try
            {
                if (!ConvertStringSecurityDescriptorToSecurityDescriptorW(Sddl, SDDL_REVISION_1, out IntPtr psd, out _))
                {
                    Log.Warning("HookOverlay: visibility SD build failed ({err}); ALT+O won't reach the hook", Marshal.GetLastWin32Error());
                    return channel; // _handle stays null -> SetVisible is a no-op; the hook defaults to visible
                }

                try
                {
                    var sa = new SECURITY_ATTRIBUTES
                    {
                        nLength = Marshal.SizeOf<SECURITY_ATTRIBUTES>(),
                        lpSecurityDescriptor = psd,
                        bInheritHandle = 0
                    };
                    channel._handle = CreateEventW(ref sa, true, initiallyVisible, EventName);
                    int err = Marshal.GetLastWin32Error();
                    if (channel._handle == IntPtr.Zero)
                        Log.Warning("HookOverlay: CreateEvent('{name}') failed ({err}); ALT+O won't reach the hook", EventName, err);
                }
                finally
                {
                    if (psd != IntPtr.Zero) LocalFree(psd);
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "HookOverlay: could not create the overlay-visibility channel");
            }
            return channel;
        }

        /// <summary>Mirrors the ALT+O state into the event: signaled = visible, reset = hidden.</summary>
        public void SetVisible(bool visible)
        {
            if (_handle == IntPtr.Zero) return;
            if (visible) SetEvent(_handle); else ResetEvent(_handle);
        }

        public void Dispose()
        {
            if (_handle != IntPtr.Zero)
            {
                CloseHandle(_handle);
                _handle = IntPtr.Zero;
            }
        }
    }
}
