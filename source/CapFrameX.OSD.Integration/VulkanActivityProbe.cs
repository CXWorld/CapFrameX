using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace CapFrameX.OSD.Integration
{
    internal struct VulkanActivitySnapshot
    {
        public bool IsLayerLoaded;
        public long LastVulkanPresentTickMs;
        public int PreferredBackend;
        // Extent of the presenting Vulkan swapchain; 0 until a present established one.
        public int ResolutionX;
        public int ResolutionY;
    }

    /// <summary>
    /// Reads the per-process renderer arbitration state published by the Vulkan layer.
    /// A recent Vulkan present must suppress DXGI hook injection, not merely DXGI drawing:
    /// installing a dormant Present hook is enough to destabilize some interop swapchains.
    /// </summary>
    internal static class VulkanActivityProbe
    {
        internal const ulong PriorityWindowMs = 2000;

        // renderer_arbiter.cpp::PreferDxgi() writes this once the Vulkan compositor has failed
        // and permanently yielded presentation to DXGI.
        internal const int PreferDxgiBackend = 1;

        // Keep these values synchronized with renderer_arbiter.cpp::RendererState.
        private const int StateVersion = 2;
        private const int StateSize = 24;
        private const int VersionOffset = 0;
        private const int LastVulkanPresentOffset = 8;
        private const int PreferredBackendOffset = 16;
        // Packed as width | height << 16 into the padding the struct already had, so the block
        // stays 24 bytes — the hook and the layer share it and a size change would break the
        // module that maps it second. See renderer_arbiter.cpp::AnnounceVulkanResolution.
        private const int ResolutionPackedOffset = 20;
        private const int ErrorFileNotFound = 2;
        private const uint FileMapRead = 0x0004;

        [DllImport("kernel32.dll")]
        private static extern ulong GetTickCount64();

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
        /// Reads the arbitration signals the DXGI injection gate needs from a single mapping
        /// open: whether Vulkan presented within <see cref="PriorityWindowMs"/>, whether it has
        /// ever presented, and whether the layer has permanently yielded presentation to DXGI.
        /// </summary>
        internal static bool TryHasRecentPresent(int pid, out bool recent,
            out bool hasEverPresented, out bool yieldedToDxgi, out string error)
        {
            recent = false;
            hasEverPresented = false;
            yieldedToDxgi = false;
            if (!TryRead(pid, out VulkanActivitySnapshot snapshot, out error))
                return false;

            if (!snapshot.IsLayerLoaded)
                return true;

            yieldedToDxgi = snapshot.PreferredBackend == PreferDxgiBackend;
            hasEverPresented = snapshot.LastVulkanPresentTickMs > 0;
            if (!hasEverPresented)
                return true;

            recent = IsRecent(unchecked((ulong)snapshot.LastVulkanPresentTickMs),
                GetTickCount64(), PriorityWindowMs);
            return true;
        }

        internal static bool TryRead(int pid, out VulkanActivitySnapshot snapshot,
            out string error)
        {
            snapshot = default;
            error = null;
            if (pid <= 0)
            {
                error = "invalid target PID";
                return false;
            }

            string mappingName = GetMappingName(pid);
            IntPtr mapping = IntPtr.Zero;
            IntPtr view = IntPtr.Zero;
            try
            {
                mapping = OpenFileMappingW(FileMapRead, false, mappingName);
                if (mapping == IntPtr.Zero)
                {
                    int openError = Marshal.GetLastWin32Error();
                    // Normal for a process which has not loaded/presented through our Vulkan layer.
                    if (openError == ErrorFileNotFound)
                        return true;

                    error = $"OpenFileMapping failed (Win32 error {openError})";
                    return false;
                }

                view = MapViewOfFile(mapping, FileMapRead, 0, 0,
                    new UIntPtr((uint)StateSize));
                if (view == IntPtr.Zero)
                {
                    error = $"MapViewOfFile failed (Win32 error {Marshal.GetLastWin32Error()})";
                    return false;
                }

                int version = Marshal.ReadInt32(view, VersionOffset);
                if (version != StateVersion)
                {
                    error = $"renderer state version {version}, expected {StateVersion}";
                    return false;
                }

                int packedResolution = Marshal.ReadInt32(view, ResolutionPackedOffset);
                snapshot = new VulkanActivitySnapshot
                {
                    IsLayerLoaded = true,
                    LastVulkanPresentTickMs = Marshal.ReadInt64(
                        view, LastVulkanPresentOffset),
                    PreferredBackend = Marshal.ReadInt32(view, PreferredBackendOffset),
                    ResolutionX = packedResolution & 0xFFFF,
                    ResolutionY = (packedResolution >> 16) & 0xFFFF
                };
                return true;
            }
            catch (Exception ex) when (ex is ArgumentException ||
                                       ex is NotSupportedException)
            {
                error = $"{ex.GetType().Name}: {ex.Message}";
                return false;
            }
            finally
            {
                if (view != IntPtr.Zero)
                    UnmapViewOfFile(view);
                if (mapping != IntPtr.Zero)
                    CloseHandle(mapping);
            }
        }

        internal static string GetMappingName(int pid)
            => $"Local\\CfxOsdRendererStateV2_{pid}";

        internal static bool IsRecent(ulong then, ulong now, ulong windowMs)
            => now < then || now - then <= windowMs;
    }
}
