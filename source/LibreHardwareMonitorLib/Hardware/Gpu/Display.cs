using Serilog;
using System;
using System.Runtime.InteropServices;

namespace LibreHardwareMonitor.Hardware.Gpu
{
    /// <summary>
    /// Represents a display associated with a window handle.
    /// </summary>
    public class Display
    {
        // MonitorFromWindow
        private const uint MONITOR_DEFAULTTONEAREST = 2;

        IntPtr _hwnd;

        /// <summary>
        /// Initializes a new instance of the <see cref="Display"/> class.
        /// </summary>
        /// <param name="hwnd"></param>
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

        /// <summary>
        /// Gets the display refresh rate in Hz.
        /// </summary>
        /// <returns></returns>
        public int GetDisplayRefreshRate()
        {
            int refreshRate = 0;

            try
            {
                // Get a monitor handle ("HMONITOR") for the window. 
                // If the window is straddling more than one monitor, Windows will pick the "best" one.
                if (!TryGetMonitorInfo(out MONITORINFOEXW monitorInfo))
                {
                    return 0;
                }

                // Get display settings from Windows API
                bool bResult = EnumDisplaySettingsW(monitorInfo.szDevice, ENUM_CURRENT_SETTINGS, out DEVMODEW devMode);
                if (!bResult)
                {
                    return 0;
                }

                refreshRate = (int)devMode.dmDisplayFrequency;
            }
            catch (Exception ex)
            {
                Log.Logger.Error(ex, $"Error while getting monitor refresh rate.");
            }

            return refreshRate;
        }

        public string GetDisplayDeviceName()
        {
            try
            {
                if (TryGetMonitorInfo(out MONITORINFOEXW monitorInfo))
                {
                    return monitorInfo.szDevice;
                }
            }
            catch (Exception ex)
            {
                Log.Logger.Error(ex, $"Error while getting monitor device name.");
            }

            return null;
        }

        private bool TryGetMonitorInfo(out MONITORINFOEXW monitorInfo)
        {
            monitorInfo = new MONITORINFOEXW
            {
                cbSize = (uint)Marshal.SizeOf<MONITORINFOEXW>()
            };

            IntPtr hmonitor = MonitorFromWindow(_hwnd, MONITOR_DEFAULTTONEAREST);
            if (hmonitor == IntPtr.Zero)
            {
                return false;
            }

            return GetMonitorInfoW(hmonitor, ref monitorInfo);
        }
    }
}
