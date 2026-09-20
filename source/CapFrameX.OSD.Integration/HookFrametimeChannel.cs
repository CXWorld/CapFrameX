using System;
using System.Runtime.InteropServices;
using System.Threading;
using Serilog;

namespace CapFrameX.OSD.Integration
{
    /// <summary>
    /// Streams CapFrameX's PER-FRAME PresentMon frametime + display-time samples to the in-game hook
    /// through a named shared-memory RING, <c>Global\CfxOsdFrametimesV1</c>. This is the "PresentMon"
    /// graph source (opt-in via <see cref="Contracts.Configuration.IAppConfiguration.HookOverlayUsePresentMonFrametimes"/>),
    /// strictly separate from the hook's own local present ring: PresentMon data carries ETW-pipeline
    /// latency, so the renderer buffers + replays it via its playback clock rather than treating it as
    /// live in-game frametimes.
    ///
    /// SPSC ring: one writer (CapFrameX frame-data stream), one reader (the hook in the game process).
    /// 32-byte header + <see cref="Capacity"/> fixed 24-byte records {timeMs, frametimeMs, displayTimeMs}
    /// (all double). <c>writeIdx</c> is a monotonic sample counter; the next slot is <c>writeIdx % Capacity</c>.
    /// The reader tracks its own last-read index and drains new samples each present. A record is written
    /// BEFORE <c>writeIdx</c> is bumped (memory barrier), so the reader never observes a half-written
    /// sample below the index it read. Same permissive SD (Everyone DACL + LOW integrity label) as
    /// <see cref="HookMetricsChannel"/> so a medium/low-integrity game can open it.
    /// </summary>
    internal sealed class HookFrametimeChannel : IDisposable
    {
        internal const string MapName = @"Global\CfxOsdFrametimesV1";

        // ---- shared layout (MUST match the native reader in overlay_frametime_shm.cpp) ----
        internal const uint Magic = 0x32584643u; // 'C''F''X''2' (distinct from the entry SHM 'CFX1')
        internal const uint Version = 1;
        internal const int Capacity = 4096;      // ring slots (~seconds of history at any frame rate)
        // header
        private const int OffMagic = 0, OffVersion = 4, OffCapacity = 8, OffFlags = 12,
                          OffWriteIdx = 16, OffWriteQpc = 24;
        internal const int HeaderSize = 32;
        // record: three doubles
        private const int RTime = 0, RFrametime = 8, RDisplayTime = 16;
        internal const int RecordSize = 24;
        private const int MapSize = HeaderSize + Capacity * RecordSize; // 98336

        private const string Sddl = "D:(A;;GA;;;WD)S:(ML;;NW;;;LW)";
        private const uint SDDL_REVISION_1 = 1;
        private const uint PAGE_READWRITE = 0x04;
        private const uint FILE_MAP_WRITE = 0x0002;
        private static readonly IntPtr INVALID_HANDLE_VALUE = new IntPtr(-1);

        private readonly object _gate = new object();
        private IntPtr _mapHandle = IntPtr.Zero;
        private IntPtr _view = IntPtr.Zero;
        private long _writeIdx;

        [StructLayout(LayoutKind.Sequential)]
        private struct SECURITY_ATTRIBUTES
        {
            public int nLength;
            public IntPtr lpSecurityDescriptor;
            public int bInheritHandle;
        }

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr CreateFileMappingW(IntPtr hFile, ref SECURITY_ATTRIBUTES sa,
            uint protect, uint maxSizeHigh, uint maxSizeLow, string name);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr MapViewOfFile(IntPtr hMap, uint access, uint offHigh, uint offLow, UIntPtr bytes);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool UnmapViewOfFile(IntPtr view);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr handle);

        [DllImport("kernel32.dll")]
        private static extern bool QueryPerformanceCounter(out long count);

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool ConvertStringSecurityDescriptorToSecurityDescriptorW(
            string sddl, uint revision, out IntPtr securityDescriptor, out int size);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr LocalFree(IntPtr handle);

        public static HookFrametimeChannel Create()
        {
            var ch = new HookFrametimeChannel();
            try
            {
                if (!ConvertStringSecurityDescriptorToSecurityDescriptorW(Sddl, SDDL_REVISION_1, out IntPtr psd, out _))
                {
                    Log.Warning("HookOverlay: frametime SD build failed ({err}); PresentMon graph source unavailable",
                        Marshal.GetLastWin32Error());
                    return ch;
                }
                try
                {
                    var sa = new SECURITY_ATTRIBUTES
                    {
                        nLength = Marshal.SizeOf<SECURITY_ATTRIBUTES>(),
                        lpSecurityDescriptor = psd,
                        bInheritHandle = 0
                    };
                    ch._mapHandle = CreateFileMappingW(INVALID_HANDLE_VALUE, ref sa, PAGE_READWRITE, 0, MapSize, MapName);
                    int err = Marshal.GetLastWin32Error();
                    if (ch._mapHandle == IntPtr.Zero)
                    {
                        Log.Warning("HookOverlay: CreateFileMapping('{name}') failed ({err})", MapName, err);
                        return ch;
                    }
                    ch._view = MapViewOfFile(ch._mapHandle, FILE_MAP_WRITE, 0, 0, new UIntPtr(MapSize));
                    if (ch._view == IntPtr.Zero)
                    {
                        Log.Warning("HookOverlay: MapViewOfFile (frametimes) failed ({err})", Marshal.GetLastWin32Error());
                        CloseHandle(ch._mapHandle); ch._mapHandle = IntPtr.Zero;
                        return ch;
                    }
                    Marshal.WriteInt32(ch._view, OffMagic, unchecked((int)Magic));
                    Marshal.WriteInt32(ch._view, OffVersion, (int)Version);
                    Marshal.WriteInt32(ch._view, OffCapacity, Capacity);
                    Marshal.WriteInt32(ch._view, OffFlags, 0);
                    Marshal.WriteInt64(ch._view, OffWriteIdx, 0);
                    Marshal.WriteInt64(ch._view, OffWriteQpc, 0);
                    Log.Information("HookOverlay: frametime channel '{name}' created", MapName);
                }
                finally
                {
                    if (psd != IntPtr.Zero) LocalFree(psd);
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "HookOverlay: could not create the frametime channel");
            }
            return ch;
        }

        /// <summary>
        /// Append one per-frame sample. <paramref name="timeMs"/> is PresentMon StartTimeInMs (QPC ms) —
        /// the replay timeline. <paramref name="displayTimeMs"/> is 0 for frames with no display sample
        /// (dropped frames); the reader skips those for the display-time graph.
        /// </summary>
        public void PushSample(double timeMs, double frametimeMs, double displayTimeMs)
        {
            if (_view == IntPtr.Zero) return;
            lock (_gate)
            {
                if (_view == IntPtr.Zero) return;
                int slot = (int)(_writeIdx % Capacity);
                IntPtr rec = IntPtr.Add(_view, HeaderSize + slot * RecordSize);
                WriteDouble(rec, RTime, timeMs);
                WriteDouble(rec, RFrametime, frametimeMs);
                WriteDouble(rec, RDisplayTime, displayTimeMs);

                QueryPerformanceCounter(out long qpc);
                Marshal.WriteInt64(_view, OffWriteQpc, qpc);
                Thread.MemoryBarrier(); // the record is fully written before the index is published
                _writeIdx++;
                Marshal.WriteInt64(_view, OffWriteIdx, _writeIdx);
            }
        }

        private static void WriteDouble(IntPtr recBase, int off, double v)
            => Marshal.WriteInt64(recBase, off, BitConverter.DoubleToInt64Bits(v));

        public void Dispose()
        {
            lock (_gate)
            {
                if (_view != IntPtr.Zero) { UnmapViewOfFile(_view); _view = IntPtr.Zero; }
                if (_mapHandle != IntPtr.Zero) { CloseHandle(_mapHandle); _mapHandle = IntPtr.Zero; }
            }
        }
    }
}
