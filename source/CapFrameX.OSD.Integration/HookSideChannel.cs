using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace CapFrameX.OSD.Integration
{
    /// <summary>
    /// Copies a block the in-game hook publishes beside its status block: a named mapping whose
    /// publication counter is odd while the hook writes it.
    /// </summary>
    internal static class HookSideChannel
    {
        private const uint FileMapRead = 0x0004;

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr OpenFileMappingW(uint desiredAccess, bool inheritHandle,
            string name);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr MapViewOfFile(IntPtr mapping, uint desiredAccess,
            uint fileOffsetHigh, uint fileOffsetLow, UIntPtr numberOfBytesToMap);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool UnmapViewOfFile(IntPtr view);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr handle);

        /// <summary>
        /// A missing mapping is the normal state for every process without a current hook and is
        /// a plain false: the overlay refresh asks once per tick, so nothing may throw here.
        /// </summary>
        internal static bool TryCopy(string mappingName, int size, int publicationOffset,
            out byte[] block)
        {
            block = null;
            IntPtr mapping = OpenFileMappingW(FileMapRead, false, mappingName);
            if (mapping == IntPtr.Zero) return false;
            IntPtr view = IntPtr.Zero;
            try
            {
                view = MapViewOfFile(mapping, FileMapRead, 0, 0, (UIntPtr)size);
                if (view == IntPtr.Zero) return false;

                var copy = new byte[size];
                for (int attempt = 0; attempt < 3; attempt++)
                {
                    // Odd while the hook writes; a copy is only good between two equal reads.
                    int publication = Marshal.ReadInt32(view, publicationOffset);
                    if ((publication & 1) != 0) continue;
                    Thread.MemoryBarrier();
                    Marshal.Copy(view, copy, 0, size);
                    Thread.MemoryBarrier();
                    if (publication != Marshal.ReadInt32(view, publicationOffset)) continue;
                    block = copy;
                    return true;
                }
                return false;
            }
            finally
            {
                if (view != IntPtr.Zero) UnmapViewOfFile(view);
                CloseHandle(mapping);
            }
        }
    }
}
