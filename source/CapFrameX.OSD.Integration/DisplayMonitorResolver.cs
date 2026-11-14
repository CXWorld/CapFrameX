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

        [DllImport("user32.dll")]
        private static extern int GetSystemMetrics(int index);

        private const int SM_XVIRTUALSCREEN = 76, SM_YVIRTUALSCREEN = 77, SM_CXVIRTUALSCREEN = 78,
            SM_CYVIRTUALSCREEN = 79, SM_CMONITORS = 80;

        /// <summary>How long <see cref="GetMonitorIndexCached"/> trusts an index whose display
        /// layout signature did not change.</summary>
        internal const long CacheLifetimeMs = 10_000;

        private static readonly object CacheGate = new object();
        private static readonly MonitorIndexCache Cache = new MonitorIndexCache(GetMonitorIndex,
            GetLayoutSignature, () => Environment.TickCount64);

        /// <summary>
        /// <see cref="GetMonitorIndex"/> for every OSD refresh of the hook-free window, which
        /// enumerated the displays once per refresh. The index is kept while the display count and
        /// the virtual screen stay the same, for at most <see cref="CacheLifetimeMs"/>: a display
        /// that is added, removed or moved changes them at once, a new primary display without
        /// another change shows within the lifetime.
        /// </summary>
        internal static int GetMonitorIndexCached(string selectedDeviceName)
        {
            lock (CacheGate) return Cache.Get(selectedDeviceName);
        }

        private static long GetLayoutSignature()
        {
            unchecked
            {
                long signature = GetSystemMetrics(SM_CMONITORS);
                signature = signature * 31 + GetSystemMetrics(SM_XVIRTUALSCREEN);
                signature = signature * 31 + GetSystemMetrics(SM_YVIRTUALSCREEN);
                signature = signature * 31 + GetSystemMetrics(SM_CXVIRTUALSCREEN);
                signature = signature * 31 + GetSystemMetrics(SM_CYVIRTUALSCREEN);
                return signature;
            }
        }

        /// <summary>The cache of <see cref="GetMonitorIndexCached"/>; not thread-safe.</summary>
        internal sealed class MonitorIndexCache
        {
            private readonly Func<string, int> _resolve;
            private readonly Func<long> _layoutSignature;
            private readonly Func<long> _clockMs;
            private bool _valid;
            private string _deviceName;
            private int _index;
            private long _signature;
            private long _resolvedAtMs;

            internal MonitorIndexCache(Func<string, int> resolve, Func<long> layoutSignature, Func<long> clockMs)
            {
                _resolve = resolve;
                _layoutSignature = layoutSignature;
                _clockMs = clockMs;
            }

            internal int Get(string deviceName)
            {
                long now = _clockMs();
                long signature = _layoutSignature();
                if (_valid && signature == _signature && now - _resolvedAtMs < CacheLifetimeMs &&
                    string.Equals(deviceName, _deviceName, StringComparison.OrdinalIgnoreCase))
                    return _index;
                _index = _resolve(deviceName);
                _deviceName = deviceName;
                _signature = signature;
                _resolvedAtMs = now;
                _valid = true;
                return _index;
            }
        }

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
