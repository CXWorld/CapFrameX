using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace CapFrameX
{
    public static class IconHelper
    {
        [DllImport("user32.dll")]
        static extern int GetWindowLong(IntPtr hwnd, int index);

        [DllImport("user32.dll")]
        static extern int SetWindowLong(IntPtr hwnd, int index, int newStyle);

        [DllImport("user32.dll")]
        static extern bool SetWindowPos(IntPtr hwnd, IntPtr hwndInsertAfter, int x, int y, int width, int height, uint flags);

        [DllImport("user32.dll")]
        static extern IntPtr SendMessage(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);

        const int GWL_EXSTYLE = -20;
        const int WS_EX_DLGMODALFRAME = 0x0001;
        const int SWP_NOSIZE = 0x0001;
        const int SWP_NOMOVE = 0x0002;
        const int SWP_NOZORDER = 0x0004;
        const int SWP_FRAMECHANGED = 0x0020;
        const uint WM_GETICON = 0x007F;
        const uint WM_SETICON = 0x0080;
        const int ICON_SMALL = 0;
        const int ICON_BIG = 1;

        public static void RemoveIcon(Window window)
        {
            IntPtr hwnd = new WindowInteropHelper(window).Handle;
            int extendedStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
            SetWindowLong(hwnd, GWL_EXSTYLE, extendedStyle | WS_EX_DLGMODALFRAME);
            SetWindowPos(hwnd, IntPtr.Zero, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_FRAMECHANGED);
        }

        public static void RefreshTaskbarIcon(Window window)
        {
            IntPtr hwnd = new WindowInteropHelper(window).Handle;
            if (hwnd == IntPtr.Zero)
                return;

            IntPtr smallIcon = SendMessage(hwnd, WM_GETICON, new IntPtr(ICON_SMALL), IntPtr.Zero);
            IntPtr bigIcon = SendMessage(hwnd, WM_GETICON, new IntPtr(ICON_BIG), IntPtr.Zero);

            // Re-publish the existing handles after the taskbar button has been created.
            // WS_EX_DLGMODALFRAME keeps the title bar clean but can make Explorer miss
            // WPF's initial WM_SETICON messages until the window is minimized/restored.
            if (smallIcon != IntPtr.Zero)
            {
                SendMessage(hwnd, WM_SETICON, new IntPtr(ICON_SMALL), IntPtr.Zero);
                SendMessage(hwnd, WM_SETICON, new IntPtr(ICON_SMALL), smallIcon);
            }

            if (bigIcon != IntPtr.Zero)
            {
                SendMessage(hwnd, WM_SETICON, new IntPtr(ICON_BIG), IntPtr.Zero);
                SendMessage(hwnd, WM_SETICON, new IntPtr(ICON_BIG), bigIcon);
            }
        }
    }
}
