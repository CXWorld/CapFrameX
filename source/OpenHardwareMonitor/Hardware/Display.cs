using System;
using System.Runtime.InteropServices;

namespace OpenHardwareMonitor.Hardware
{
    public class Display
    {
        // MonitorFromWindow
        private const uint MONITOR_DEFAULTTONEAREST = 2;

        IntPtr _hwnd;

        public Display(IntPtr hwnd)
        {
            _hwnd = hwnd;
        }

        [DllImport("user32.dll", SetLastError = false)]
        private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

        // RECT
        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int left;
            public int top;
            public int right;
            public int bottom;
        }

        // MONITORINFOEX
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private unsafe struct MONITORINFOEXW
        {
            public uint cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public uint dwFlags;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string szDevice;
        }

        // GetMonitorInfo
        [DllImport("user32.dll", SetLastError = false, CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetMonitorInfoW(
            IntPtr hMonitor,
            ref MONITORINFOEXW lpmi);

        // EnumDisplaySettings
        private const uint ENUM_CURRENT_SETTINGS = unchecked((uint)-1);

        [DllImport("user32.dll", SetLastError = false, CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool EnumDisplaySettingsW(
            [MarshalAs(UnmanagedType.LPWStr)] string lpszDeviceName,
            uint iModeNum,
            out DEVMODEW lpDevMode);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct DEVMODEW
        {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string dmDeviceName;

            public ushort dmSpecVersion;
            public ushort dmDriverVersion;
            public ushort dmSize;
            public ushort dmDriverExtra;
            public uint dmFields;

            /*public short dmOrientation;
            public short dmPaperSize;
            public short dmPaperLength;
            public short dmPaperWidth;
            public short dmScale;
            public short dmCopies;
            public short dmDefaultSource;
            public short dmPrintQuality;*/
            // These next 4 int fields are a union with the above 8 shorts, but we don't need them right now
            public int dmPositionX;
            public int dmPositionY;
            public uint dmDisplayOrientation;
            public uint dmDisplayFixedOutput;

            public short dmColor;
            public short dmDuplex;
            public short dmYResolution;
            public short dmTTOption;
            public short dmCollate;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string dmFormName;

            public short dmLogPixels;
            public uint dmBitsPerPel;
            public uint dmPelsWidth;
            public uint dmPelsHeight;

            public uint dmNupOrDisplayFlags;
            public uint dmDisplayFrequency;

            public uint dmICMMethod;
            public uint dmICMIntent;
            public uint dmMediaType;
            public uint dmDitherType;
            public uint dmReserved1;
            public uint dmReserved2;
            public uint dmPanningWidth;
            public uint dmPanningHeight;
        }

        public int GetDisplayRefreshRate()
        {
            // 1. Get a monitor handle ("HMONITOR") for the window. 
            //    If the window is straddling more than one monitor, Windows will pick the "best" one.
            IntPtr hmonitor = MonitorFromWindow(_hwnd, MONITOR_DEFAULTTONEAREST);
            if (hmonitor == IntPtr.Zero)
            {
                return 0;
            }

            // 2. Get more information about the monitor.
            MONITORINFOEXW monitorInfo = new MONITORINFOEXW
            {
                cbSize = (uint)Marshal.SizeOf<MONITORINFOEXW>()
            };

            bool bResult = GetMonitorInfoW(hmonitor, ref monitorInfo);
            if (!bResult)
            {
                return 0;
            }

            bResult = EnumDisplaySettingsW(monitorInfo.szDevice, ENUM_CURRENT_SETTINGS, out DEVMODEW devMode);
            if (!bResult)
            {
                return 0;
            }

            return (int)devMode.dmDisplayFrequency;
        }
    }
}
