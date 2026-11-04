using System;
using System.Runtime.InteropServices;

namespace CapFrameX.CaptureHelper
{
    internal static class NativeMethods
    {
        public const int WM_HOTKEY = 0x0312;

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

        public static int GetForegroundProcessId()
        {
            var window = GetForegroundWindow();
            if (window == IntPtr.Zero) return 0;
            GetWindowThreadProcessId(window, out uint processId);
            return (int)processId;
        }
    }
}
