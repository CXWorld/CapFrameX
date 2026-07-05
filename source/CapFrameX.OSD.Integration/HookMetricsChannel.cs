using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using CapFrameX.OSD.Interop;
using Serilog;

namespace CapFrameX.OSD.Integration
{
    /// <summary>
    /// Publishes CapFrameX's processed overlay entries (the same <c>OsdEntry</c> set the hook-free
    /// OSD renders — fps/lows/sensors/static rows) to the in-game hook through a named shared-memory
    /// block, <c>Global\CfxOsdMetricsV1</c>. The hook (in the game process) opens it read-only and
    /// feeds the entries, so the in-game OSD shows the same values as RTSS / hook-free.
    ///
    /// Layout is a fixed 32-byte header + up to 64 fixed-size records (no offset math). Updates use a
    /// SEQLOCK: the sequence counter is odd while writing, even when a consistent snapshot is
    /// published; the reader retries/uses-cached on an odd or changed sequence. The block carries a
    /// QPC timestamp for staleness detection. Same permissive SD (Everyone DACL + LOW integrity
    /// label) as <see cref="HookVisibilityChannel"/> so a medium/low-integrity game can open it.
    /// </summary>
    internal sealed class HookMetricsChannel : IDisposable
    {
        internal const string MapName = @"Global\CfxOsdMetricsV1";

        // ---- shared layout (MUST match the native reader in overlay_metrics_shm.cpp) ----
        internal const uint Magic = 0x31584643u; // 'C''F''X''1'
        internal const uint Version = 1;
        internal const int MaxEntries = 64;
        // header Flags bits (OffFlags). bit0 tells the hook the graph source is PresentMon (the
        // CfxOsdFrametimesV1 ring), not the hook's own local present ring.
        internal const uint FlagPresentMonGraph = 1u;
        // header
        private const int OffMagic = 0, OffVersion = 4, OffSeq = 8, OffEntryCount = 12,
                          OffPayloadBytes = 16, OffFlags = 20, OffWriteQpc = 24;
        internal const int HeaderSize = 32;
        // record field offsets (relative to the record start) + sizes
        private const int RId = 0, SzId = 64, RGroup = 64, SzGroup = 64, RLabel = 128, SzLabel = 96,
                          RValueText = 224, SzValueText = 64, RUnit = 288, SzUnit = 16,
                          RValue = 304, RUpperLimit = 312, RLowerLimit = 320, RIsNumeric = 328,
                          RColor = 332, RShowGraph = 336, RDigits = 340, RHasUpper = 344,
                          RUpperColor = 348, RHasLower = 352, RLowerColor = 356, RSeparators = 360;
        internal const int RecordSize = 368;
        private const int MapSize = 32768; // > HeaderSize + MaxEntries*RecordSize (23584)

        private const string Sddl = "D:(A;;GA;;;WD)S:(ML;;NW;;;LW)";
        private const uint SDDL_REVISION_1 = 1;
        private const uint PAGE_READWRITE = 0x04;
        private const uint FILE_MAP_WRITE = 0x0002;
        private static readonly IntPtr INVALID_HANDLE_VALUE = new IntPtr(-1);

        private readonly object _gate = new object();
        private IntPtr _mapHandle = IntPtr.Zero;
        private IntPtr _view = IntPtr.Zero;
        private int _seq;

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

        public static HookMetricsChannel Create()
        {
            var ch = new HookMetricsChannel();
            try
            {
                if (!ConvertStringSecurityDescriptorToSecurityDescriptorW(Sddl, SDDL_REVISION_1, out IntPtr psd, out _))
                {
                    Log.Warning("HookOverlay: metrics SD build failed ({err}); in-game OSD uses local metrics only",
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
                        Log.Warning("HookOverlay: MapViewOfFile failed ({err})", Marshal.GetLastWin32Error());
                        CloseHandle(ch._mapHandle); ch._mapHandle = IntPtr.Zero;
                        return ch;
                    }
                    Marshal.WriteInt32(ch._view, OffMagic, unchecked((int)Magic));
                    Marshal.WriteInt32(ch._view, OffVersion, (int)Version);
                    Marshal.WriteInt32(ch._view, OffSeq, 0);
                    Marshal.WriteInt32(ch._view, OffEntryCount, 0);
                    Marshal.WriteInt64(ch._view, OffWriteQpc, 0);
                    Log.Information("HookOverlay: metrics channel '{name}' created", MapName);
                }
                finally
                {
                    if (psd != IntPtr.Zero) LocalFree(psd);
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "HookOverlay: could not create the metrics channel");
            }
            return ch;
        }

        /// <summary>
        /// Seqlock-publish the current entry set (truncated to <see cref="MaxEntries"/>).
        /// <paramref name="flags"/> is stamped into the header (e.g. <see cref="FlagPresentMonGraph"/>)
        /// so the hook knows which graph source to use.
        /// </summary>
        public void Publish(System.Collections.Generic.IReadOnlyList<OsdEntry> entries, uint flags)
        {
            if (_view == IntPtr.Zero || entries == null) return;
            lock (_gate)
            {
                if (_view == IntPtr.Zero) return;
                int count = Math.Min(entries.Count, MaxEntries);

                _seq++; // odd => write in progress
                Marshal.WriteInt32(_view, OffSeq, _seq);
                Thread.MemoryBarrier();

                QueryPerformanceCounter(out long qpc);
                Marshal.WriteInt64(_view, OffWriteQpc, qpc);
                Marshal.WriteInt32(_view, OffFlags, unchecked((int)flags));
                Marshal.WriteInt32(_view, OffEntryCount, count);
                Marshal.WriteInt32(_view, OffPayloadBytes, count * RecordSize);

                for (int i = 0; i < count; i++)
                {
                    IntPtr rec = IntPtr.Add(_view, HeaderSize + i * RecordSize);
                    OsdEntry e = entries[i];
                    WriteStr(rec, RId, SzId, e.Identifier);
                    WriteStr(rec, RGroup, SzGroup, e.Group);
                    WriteStr(rec, RLabel, SzLabel, e.Label);
                    WriteStr(rec, RValueText, SzValueText, e.ValueText);
                    WriteStr(rec, RUnit, SzUnit, e.Unit);
                    WriteDouble(rec, RValue, e.Value);
                    WriteDouble(rec, RUpperLimit, e.UpperLimit);
                    WriteDouble(rec, RLowerLimit, e.LowerLimit);
                    Marshal.WriteInt32(rec, RIsNumeric, e.IsNumeric ? 1 : 0);
                    Marshal.WriteInt32(rec, RColor, unchecked((int)e.Color));
                    Marshal.WriteInt32(rec, RShowGraph, e.ShowGraph ? 1 : 0);
                    Marshal.WriteInt32(rec, RDigits, e.Digits);
                    Marshal.WriteInt32(rec, RHasUpper, e.HasUpper ? 1 : 0);
                    Marshal.WriteInt32(rec, RUpperColor, unchecked((int)e.UpperColor));
                    Marshal.WriteInt32(rec, RHasLower, e.HasLower ? 1 : 0);
                    Marshal.WriteInt32(rec, RLowerColor, unchecked((int)e.LowerColor));
                    Marshal.WriteInt32(rec, RSeparators, e.Separators);
                }

                Thread.MemoryBarrier();
                _seq++; // even => consistent snapshot ready
                Marshal.WriteInt32(_view, OffSeq, _seq);
            }
        }

        private static void WriteStr(IntPtr recBase, int off, int size, string s)
        {
            var buf = new byte[size]; // zeroed => guaranteed null terminator + clean tail
            if (!string.IsNullOrEmpty(s))
            {
                var bytes = Encoding.UTF8.GetBytes(s);
                Array.Copy(bytes, buf, Math.Min(bytes.Length, size - 1));
            }
            Marshal.Copy(buf, 0, IntPtr.Add(recBase, off), size);
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
