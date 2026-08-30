using System;
using System.Runtime.InteropServices;
using System.Threading;
using Serilog;

namespace CapFrameX.OSD.Integration
{
    /// <summary>
    /// Publishes the overlay placement (anchor corner + margins in px) to the in-game hook and the
    /// Vulkan layer through a named shared-memory block, <c>Global\CfxOsdPlacementV1</c>.
    /// </summary>
    /// <remarks>
    /// Deliberately its OWN channel rather than more bits in the metrics header:
    /// <list type="bullet">
    /// <item>the metrics flags DWORD has 12 bits left, which is not enough for an anchor plus two
    /// pixel margins;</item>
    /// <item>growing the metrics header would move the record area, and already deployed readers
    /// accept version 3 — they would parse a new layout silently and wrongly rather than
    /// rejecting it.</item>
    /// </list>
    /// The payload is tiny and changes only when the user moves a control, so a seqlock over one
    /// page is all it needs. A reader that cannot open it keeps the renderer's built-in default.
    /// </remarks>
    internal sealed class HookPlacementChannel : IDisposable
    {
        internal const string MapName = @"Global\CfxOsdPlacementV1";

        // ---- shared layout (MUST match the native reader in overlay_placement_shm.cpp) ----
        internal const uint Magic = 0x314C5043u; // 'C''P''L''1'
        internal const uint Version = 1;
        private const int OffMagic = 0, OffVersion = 4, OffSeq = 8,
                          OffAnchor = 12, OffMarginX = 16, OffMarginY = 20;
        private const int MapSize = 4096; // one page; payload is 24 bytes

        private const string Sddl = "D:(A;;GA;;;WD)S:(ML;;NW;;;LW)";
        private const uint SDDL_REVISION_1 = 1;
        private const uint PAGE_READWRITE = 0x04;
        private const uint FILE_MAP_WRITE = 0x0002;
        private static readonly IntPtr INVALID_HANDLE_VALUE = new IntPtr(-1);

        private readonly object _gate = new object();
        private IntPtr _mapHandle = IntPtr.Zero;
        private IntPtr _view = IntPtr.Zero;
        private int _seq;
        private int _lastAnchor = -1, _lastMarginX = -1, _lastMarginY = -1;

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

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool ConvertStringSecurityDescriptorToSecurityDescriptorW(
            string sddl, uint revision, out IntPtr securityDescriptor, out int size);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr LocalFree(IntPtr handle);

        public static HookPlacementChannel Create()
            => Create(MapName);

        internal static HookPlacementChannel Create(string mapName)
        {
            if (string.IsNullOrWhiteSpace(mapName))
                throw new ArgumentException("A mapping name is required.", nameof(mapName));

            var ch = new HookPlacementChannel();
            try
            {
                if (!ConvertStringSecurityDescriptorToSecurityDescriptorW(Sddl, SDDL_REVISION_1, out IntPtr psd, out _))
                {
                    Log.Warning("HookOverlay: placement SD build failed ({err}); the in-game overlay keeps its default position",
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
                    ch._mapHandle = CreateFileMappingW(INVALID_HANDLE_VALUE, ref sa, PAGE_READWRITE,
                        0, MapSize, mapName);
                    if (ch._mapHandle == IntPtr.Zero)
                    {
                        Log.Warning("HookOverlay: CreateFileMapping('{name}') failed ({err})", mapName,
                            Marshal.GetLastWin32Error());
                        return ch;
                    }
                    ch._view = MapViewOfFile(ch._mapHandle, FILE_MAP_WRITE, 0, 0, new UIntPtr(MapSize));
                    if (ch._view == IntPtr.Zero)
                    {
                        Log.Warning("HookOverlay: MapViewOfFile failed ({err})", Marshal.GetLastWin32Error());
                        CloseHandle(ch._mapHandle); ch._mapHandle = IntPtr.Zero;
                        return ch;
                    }
                    ch._seq = (Marshal.ReadInt32(ch._view, OffSeq) & ~1) + 1; // odd => writing
                    Marshal.WriteInt32(ch._view, OffSeq, ch._seq);
                    Thread.MemoryBarrier();
                    Marshal.WriteInt32(ch._view, OffMagic, unchecked((int)Magic));
                    Marshal.WriteInt32(ch._view, OffVersion, (int)Version);
                    Thread.MemoryBarrier();
                    ch._seq++;
                    Marshal.WriteInt32(ch._view, OffSeq, ch._seq);
                    Log.Information("HookOverlay: placement channel '{name}' created", mapName);
                }
                finally
                {
                    if (psd != IntPtr.Zero) LocalFree(psd);
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "HookOverlay: could not create the placement channel");
            }
            return ch;
        }

        /// <summary>
        /// Seqlock-publish anchor + margins. Writing only on change keeps the readers' change
        /// detection cheap — a placement write makes the renderer re-place the panel. A forced
        /// write re-seeds a newly created native OSD instance after a visibility toggle.
        /// </summary>
        public void Publish(int anchor, int marginX, int marginY, bool force = false)
        {
            lock (_gate)
            {
                if (_view == IntPtr.Zero) return;
                if (!force && anchor == _lastAnchor && marginX == _lastMarginX &&
                    marginY == _lastMarginY) return;
                _lastAnchor = anchor; _lastMarginX = marginX; _lastMarginY = marginY;

                _seq++; // odd => write in progress
                Marshal.WriteInt32(_view, OffSeq, _seq);
                Thread.MemoryBarrier();

                Marshal.WriteInt32(_view, OffAnchor, anchor);
                Marshal.WriteInt32(_view, OffMarginX, marginX);
                Marshal.WriteInt32(_view, OffMarginY, marginY);

                Thread.MemoryBarrier();
                _seq++; // even => consistent snapshot
                Marshal.WriteInt32(_view, OffSeq, _seq);
            }
        }

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
