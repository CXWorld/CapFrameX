using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace CapFrameX.OSD.Integration
{
    internal static class DisplayMonitorResolver
    {
        private const uint MONITORINFOF_PRIMARY = 0x00000001;
        private const int CCHDEVICENAME = 32;

        internal readonly struct MonitorDescriptor
        {
            public MonitorDescriptor(string deviceName, bool isPrimary)
            {
                DeviceName = deviceName;
                IsPrimary = isPrimary;
            }

            public string DeviceName { get; }
            public bool IsPrimary { get; }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct MONITORINFOEX
        {
            public uint Size;
            public RECT Monitor;
            public RECT WorkArea;
            public uint Flags;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = CCHDEVICENAME)]
            public string DeviceName;
        }

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private delegate bool MonitorEnumProc(IntPtr monitor, IntPtr hdc, IntPtr monitorRect,
            IntPtr data);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr clipRect,
            MonitorEnumProc callback, IntPtr data);

        [DllImport("user32.dll", EntryPoint = "GetMonitorInfoW", ExactSpelling = true,
            CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetMonitorInfo(IntPtr monitor, ref MONITORINFOEX monitorInfo);

        /// <summary>
        /// Returns the selected display's index in the same <see cref="EnumDisplayMonitors"/>
        /// ordering consumed by the native hook-free renderer. Missing selections fall back to
        /// the primary display and ultimately to index zero.
        /// </summary>
        internal static int GetMonitorIndex(string selectedDeviceName)
        {
            var monitors = new List<MonitorDescriptor>();
            MonitorEnumProc callback = (monitor, _, _, _) =>
            {
                var monitorInfo = new MONITORINFOEX
                {
                    Size = (uint)Marshal.SizeOf<MONITORINFOEX>()
                };

                bool hasInfo = GetMonitorInfo(monitor, ref monitorInfo);
                monitors.Add(new MonitorDescriptor(
                    hasInfo ? monitorInfo.DeviceName : string.Empty,
                    hasInfo && (monitorInfo.Flags & MONITORINFOF_PRIMARY) != 0));
                return true;
            };

            EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, callback, IntPtr.Zero);
            return FindMonitorIndex(monitors, selectedDeviceName);
        }

        internal static int FindMonitorIndex(IReadOnlyList<MonitorDescriptor> monitors,
            string selectedDeviceName)
        {
            if (monitors == null || monitors.Count == 0)
            {
                return 0;
            }

            if (!string.IsNullOrWhiteSpace(selectedDeviceName))
            {
                for (int i = 0; i < monitors.Count; i++)
                {
                    if (string.Equals(monitors[i].DeviceName, selectedDeviceName,
                        StringComparison.OrdinalIgnoreCase))
                    {
                        return i;
                    }
                }
            }

            for (int i = 0; i < monitors.Count; i++)
            {
                if (monitors[i].IsPrimary)
                {
                    return i;
                }
            }

            return 0;
        }
    }
}
