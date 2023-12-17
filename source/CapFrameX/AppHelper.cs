using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

namespace CapFrameX
{
    public static class AppHelper
    {
        [DllImport("User32.dll")]
        static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("User32.dll")]
        static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [StructLayout(LayoutKind.Sequential)]
        struct POINT
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct RECT
        {
            public int left;
            public int top;
            public int right;
            public int bottom;
        }

        [Serializable]
        [StructLayout(LayoutKind.Sequential)]
        struct WINDOWPLACEMENT
        {
            public int length;
            public int flags;
            public int showCmd;
            public POINT ptMinPosition;
            public POINT ptMaxPosition;
            public RECT rcNormalPosition;
        }

        [DllImport("user32", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool GetWindowPlacement(IntPtr hWnd, ref WINDOWPLACEMENT lpwndpl);

        //https://learn.microsoft.com/de-de/windows/win32/api/winuser/nf-winuser-showwindow
        enum CMDSHOW
        {
            SW_HIDE = 0,
            SW_SHOWNORMAL = 1,
            SW_SHOWMINIMIZED = 2,
            SW_SHOWMAXIMIZED = 3,
            SW_SHOWNOACTIVATE = 4,
            SW_SHOW = 5,
            SW_MINIMIZE = 6,
            SW_SHOWMINNOACITVE = 7,
            SW_SHOWNA = 8,
            SW_RESTORE = 9,
            SW_SHOWDEFAULT = 10,
            SW_FORCEMINIMIZE = 11
        }

        public static void ShowWindowInCorrectState(Process process)
        {
            //The purpose of this function is to show the main window
            //of the process given, according to its current window state.
            //That means that if the main window is minimized, it should be restored.
            //And if the main window is not minimized, it should be shown/brought
            //to the foreground.

            //Some resources:
            //https://stackoverflow.com/questions/10511619/how-do-i-get-the-position-of-a-form-and-the-screen-it-is-displayed-on-and-resto
            //https://learn.microsoft.com/en-us/windows/win32/api/winuser/ns-winuser-windowplacement

            WINDOWPLACEMENT wp = new WINDOWPLACEMENT();
            wp.length = Marshal.SizeOf(wp);

            for (var attempt = 0; attempt < 20; ++attempt)
            {
                process?.Refresh();
                if (process?.MainWindowHandle != IntPtr.Zero)
                {
                    break;
                }

                Thread.Sleep(100);
                process = Process.GetProcessById(process.Id);
            }

            GetWindowPlacement(process.MainWindowHandle, ref wp);

            ShowWindow(process.MainWindowHandle, wp.showCmd == (int)CMDSHOW.SW_SHOWMINIMIZED ? (int)CMDSHOW.SW_RESTORE : (int)CMDSHOW.SW_SHOW);
            SetForegroundWindow(process.MainWindowHandle);
        }
    }
}